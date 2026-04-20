using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ChessTile : MonoBehaviour
{
    public enum HighlightState
    {
        None,
        Move,
        Capture
    }

    #region Properties

    [field: SerializeField] public int X { get; private set; } = -1;
    [field: SerializeField] public int Y { get; private set; } = -1;
    [field: SerializeField] public string TileName { get; private set; } = string.Empty;
    public ChessPiece CurrentPiece => currentPiece;

    #endregion

    #region Variables

    [SerializeField] bool autoCreateMissingComponents = true;
    [SerializeField] ChessPiece currentPiece;

    Renderer cachedRenderer;
    MaterialPropertyBlock propertyBlock;
    Color originalColor;
    bool hasOriginalColor;

    HighlightState currentHighlightState;
    Color currentHighlightColor;
    bool isHovered;
    Color hoverColor;

    Transform hoverOutlineRoot;
    MeshRenderer[] hoverOutlineRenderers;
    Material hoverOutlineMaterial;

    const float HoverOutlineThicknessRatio = 0.02f;
    const float HoverOutlineMinThickness = 0f;
    const float HoverOutlineMinYOffset = 0.02f;

    #endregion

    #region Unity

    void Reset()
    {
        EnsureRequiredComponents();
        CacheRenderer();
    }

    void OnValidate()
    {
        EnsureRequiredComponents();
        CacheRenderer();
        ApplyVisualState();
    }

    void Awake()
    {
        EnsureRequiredComponents();
        CacheRenderer();
        ApplyVisualState();
    }

    #endregion

    #region Setup

    public void SetCurrentPiece(ChessPiece piece)
    {
        currentPiece = piece;
    }

    public void SetCoordinates(int x, int y)
    {
        X = x;
        Y = y;
        TileName = BuildTileName(x, y);
        RenameTileObject();
    }

    static string BuildTileName(int x, int y)
    {
        char file = (char)('A' + x);
        int rank = y + 1;
        return $"{file}{rank}";
    }

    void RenameTileObject()
    {
        if (string.IsNullOrEmpty(TileName))
        {
            return;
        }

        gameObject.name = TileName;
    }

    void EnsureRequiredComponents()
    {
        if (!autoCreateMissingComponents)
        {
            return;
        }

        EnsureRenderer();
        EnsureCollider();
    }

    void EnsureRenderer()
    {
        Renderer existingRenderer = GetComponent<Renderer>();
        if (existingRenderer != null)
        {
            return;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = CreateQuadMesh();
        }

        if (meshRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                meshRenderer.sharedMaterial = new Material(shader);
            }
        }
    }

    void EnsureCollider()
    {
        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider != null)
        {
            existingCollider.enabled = true;
            return;
        }

        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.size = EstimateColliderSize();
        boxCollider.enabled = true;
    }

    public void EnsureInteractionCollider()
    {
        EnsureCollider();
    }

    Vector3 EstimateColliderSize()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Vector3 size = renderer.bounds.size;
            if (size.x > 0f && size.y > 0f && size.z > 0f)
            {
                return transform.InverseTransformVector(size);
            }
        }

        return new Vector3(1f, 0.1f, 1f);
    }

    static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh { name = "ChessTileQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f,  0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3( 0.5f, 0f, -0.5f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void CacheRenderer()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer != null && TryGetRendererColor(cachedRenderer, out Color color))
            {
                originalColor = color;
                hasOriginalColor = true;
            }
        }
    }

    #endregion

    #region Visuals

    public void Highlight(Color color)
    {
        SetHighlightState(HighlightState.Move, color);
    }

    public void ResetColor()
    {
        ClearHighlightState();
    }

    public void SetHighlightState(HighlightState state, Color color)
    {
        currentHighlightState = state;
        currentHighlightColor = color;
        ApplyVisualState();
    }

    public void ClearHighlightState()
    {
        currentHighlightState = HighlightState.None;
        currentHighlightColor = default;
        ApplyVisualState();
    }

    public void SetHoverState(bool hovered, Color color)
    {
        isHovered = hovered;
        hoverColor = color;
        ApplyVisualState();
    }

    void ApplyVisualState()
    {
        CacheRenderer();
        if (cachedRenderer == null)
        {
            return;
        }

        SetHoverOutlineActive(isHovered, hoverColor);

        if (currentHighlightState != HighlightState.None)
        {
            SetRendererColor(cachedRenderer, currentHighlightColor);
            return;
        }

        if (hasOriginalColor)
        {
            SetRendererColor(cachedRenderer, originalColor);
        }
    }

    static bool TryGetRendererColor(Renderer renderer, out Color color)
    {
        color = Color.white;
        Material material = renderer.sharedMaterial;
        if (material == null)
        {
            return false;
        }

        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            color = material.color;
            return true;
        }

        return false;
    }

    void SetRendererColor(Renderer renderer, Color color)
    {
        Material material = renderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        renderer.GetPropertyBlock(propertyBlock);
        if (material.HasProperty("_BaseColor"))
        {
            propertyBlock.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            propertyBlock.SetColor("_Color", color);
        }
        else
        {
            return;
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

    void SetHoverOutlineActive(bool active, Color color)
    {
        EnsureHoverOutline();
        if (hoverOutlineRoot == null || hoverOutlineRenderers == null)
        {
            return;
        }

        hoverOutlineRoot.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        UpdateHoverOutlineTransforms();
        ApplyHoverOutlineColor(color);
    }

    void EnsureHoverOutline()
    {
        if (hoverOutlineRoot == null)
        {
            Transform existing = transform.Find("HoverOutline");
            if (existing != null)
            {
                hoverOutlineRoot = existing;
            }
        }

        if (hoverOutlineRoot == null)
        {
            GameObject rootObject = new("HoverOutline");
            rootObject.transform.SetParent(transform, false);
            hoverOutlineRoot = rootObject.transform;
        }

        if (hoverOutlineRenderers == null || hoverOutlineRenderers.Length != 4)
        {
            hoverOutlineRenderers = new MeshRenderer[4];
        }

        EnsureHoverOutlineMaterial();

        for (int i = 0; i < 4; i++)
        {
            string edgeName = $"Edge_{i}";
            Transform edgeTransform = hoverOutlineRoot.Find(edgeName);

            if (edgeTransform == null)
            {
                GameObject edgeObject = new(edgeName);
                edgeObject.transform.SetParent(hoverOutlineRoot, false);

                MeshFilter meshFilter = edgeObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = CreateQuadMesh();

                MeshRenderer meshRenderer = edgeObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = hoverOutlineMaterial;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                edgeTransform = edgeObject.transform;
            }

            MeshFilter existingFilter = edgeTransform.GetComponent<MeshFilter>();
            if (existingFilter == null)
            {
                existingFilter = edgeTransform.gameObject.AddComponent<MeshFilter>();
            }

            if (existingFilter.sharedMesh == null)
            {
                existingFilter.sharedMesh = CreateQuadMesh();
            }

            MeshRenderer existingRenderer = edgeTransform.GetComponent<MeshRenderer>();
            if (existingRenderer == null)
            {
                existingRenderer = edgeTransform.gameObject.AddComponent<MeshRenderer>();
            }

            existingRenderer.sharedMaterial = hoverOutlineMaterial;
            existingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            existingRenderer.receiveShadows = false;

            hoverOutlineRenderers[i] = existingRenderer;
        }

        hoverOutlineRoot.gameObject.SetActive(false);
        UpdateHoverOutlineTransforms();
    }

    void EnsureHoverOutlineMaterial()
    {
        if (hoverOutlineMaterial != null)
        {
            return;
        }

        Shader outlineShader = Shader.Find("Sprites/Default");
        if (outlineShader == null)
        {
            outlineShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (outlineShader == null)
        {
            return;
        }

        hoverOutlineMaterial = new Material(outlineShader);
        hoverOutlineMaterial.renderQueue = 4000;
    }

    void ApplyHoverOutlineColor(Color color)
    {
        if (hoverOutlineRenderers == null)
        {
            return;
        }

        for (int i = 0; i < hoverOutlineRenderers.Length; i++)
        {
            MeshRenderer outlineRenderer = hoverOutlineRenderers[i];
            if (outlineRenderer == null)
            {
                continue;
            }

            Material sharedMaterial = outlineRenderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                continue;
            }

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                sharedMaterial.SetColor("_BaseColor", color);
            }
            else if (sharedMaterial.HasProperty("_Color"))
            {
                sharedMaterial.SetColor("_Color", color);
            }
        }
    }

    void UpdateHoverOutlineTransforms()
    {
        if (hoverOutlineRoot == null || hoverOutlineRenderers == null)
        {
            return;
        }

        Bounds bounds = cachedRenderer != null ? cachedRenderer.localBounds : new Bounds(Vector3.zero, Vector3.one);

        float width = bounds.size.x;
        float depth = bounds.size.z;
        float thickness = Mathf.Max(HoverOutlineMinThickness, Mathf.Min(width, depth) * HoverOutlineThicknessRatio);
        float yOffset = bounds.max.y + Mathf.Max(HoverOutlineMinYOffset, bounds.size.y * 0.1f);

        Vector3 northPos = new(0f, yOffset, (depth * 0.5f) - (thickness * 0.5f));
        Vector3 southPos = new(0f, yOffset, (-depth * 0.5f) + (thickness * 0.5f));
        Vector3 eastPos = new((width * 0.5f) - (thickness * 0.5f), yOffset, 0f);
        Vector3 westPos = new((-width * 0.5f) + (thickness * 0.5f), yOffset, 0f);

        float overlap = 0.01f;
        SetOutlineEdgeTransform(0, northPos, new Vector3(width + overlap, 1f, thickness + overlap));
        SetOutlineEdgeTransform(1, southPos, new Vector3(width + overlap, 1f, thickness + overlap));
        SetOutlineEdgeTransform(2, eastPos, new Vector3(thickness + overlap, 1f, depth + overlap));
        SetOutlineEdgeTransform(3, westPos, new Vector3(thickness + overlap, 1f, depth + overlap));
    }

    void SetOutlineEdgeTransform(int index, Vector3 localPosition, Vector3 localScale)
    {
        if (hoverOutlineRenderers == null || index < 0 || index >= hoverOutlineRenderers.Length)
        {
            return;
        }

        MeshRenderer outlineRenderer = hoverOutlineRenderers[index];
        if (outlineRenderer == null)
        {
            return;
        }

        Transform edgeTransform = outlineRenderer.transform;
        edgeTransform.localPosition = localPosition;
        edgeTransform.localRotation = Quaternion.identity;
        edgeTransform.localScale = localScale;
    }

    #endregion
}