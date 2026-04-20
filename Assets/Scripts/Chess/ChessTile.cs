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
    LineRenderer hoverOutline;
    static readonly int[] OutlineCornerOrder = { 0, 1, 2, 3, 0 };

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

        if (isHovered)
        {
            SetHoverOutlineActive(true, hoverColor);
        }
        else
        {
            SetHoverOutlineActive(false, hoverColor);
        }

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
        if (hoverOutline == null)
        {
            return;
        }

        hoverOutline.enabled = active;
        if (!active)
        {
            return;
        }

        hoverOutline.startColor = color;
        hoverOutline.endColor = color;
        UpdateHoverOutlinePositions();
    }

    void EnsureHoverOutline()
    {
        if (hoverOutline != null)
        {
            return;
        }

        Transform existing = transform.Find("HoverOutline");
        if (existing != null)
        {
            hoverOutline = existing.GetComponent<LineRenderer>();
        }

        if (hoverOutline == null)
        {
            GameObject outlineObject = new("HoverOutline");
            outlineObject.transform.SetParent(transform, false);
            hoverOutline = outlineObject.AddComponent<LineRenderer>();
        }

        hoverOutline.loop = false;
        hoverOutline.useWorldSpace = false;
        hoverOutline.positionCount = 5;
        hoverOutline.startWidth = 0.05f;
        hoverOutline.endWidth = 0.05f;
        hoverOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hoverOutline.receiveShadows = false;
        hoverOutline.alignment = LineAlignment.TransformZ;
        hoverOutline.textureMode = LineTextureMode.Stretch;
        hoverOutline.numCornerVertices = 2;
        hoverOutline.numCapVertices = 2;
        hoverOutline.enabled = false;

        Material outlineMaterial = new(Shader.Find("Sprites/Default"));
        hoverOutline.material = outlineMaterial;
        UpdateHoverOutlinePositions();
    }

    void UpdateHoverOutlinePositions()
    {
        if (hoverOutline == null)
        {
            return;
        }

        Bounds bounds = cachedRenderer != null ? cachedRenderer.localBounds : new Bounds(Vector3.zero, Vector3.one);
        float yOffset = bounds.max.y + 0.01f;
        Vector3 extents = bounds.extents;
        Vector3[] corners =
        {
            new(-extents.x, yOffset, -extents.z),
            new(-extents.x, yOffset, extents.z),
            new(extents.x, yOffset, extents.z),
            new(extents.x, yOffset, -extents.z)
        };

        for (int i = 0; i < OutlineCornerOrder.Length; i++)
        {
            hoverOutline.SetPosition(i, corners[OutlineCornerOrder[i]]);
        }
    }
    
    #endregion
}
