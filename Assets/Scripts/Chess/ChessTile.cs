using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ChessTile : MonoBehaviour
{
    #region Properties

    public int X { get; private set; } = -1;
    public int Y { get; private set; } = -1;
    public string TileName { get; private set; } = string.Empty;

    #endregion

    #region Variables

    [SerializeField] bool autoCreateMissingComponents = true;

    Renderer cachedRenderer;
    Color originalColor;
    bool hasOriginalColor;

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
    }

    void Awake()
    {
        EnsureRequiredComponents();
        CacheRenderer();
    }

    #endregion

    #region Setup

    public void SetCoordinates(int x, int y)
    {
        X = x;
        Y = y;
        TileName = BuildTileName(x, y);
        gameObject.name = TileName;
    }

    static string BuildTileName(int x, int y)
    {
        char file = (char)('A' + x);
        int rank = y + 1;
        return $"{file}{rank}";
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
            return;
        }

        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.size = EstimateColliderSize();
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
            if (cachedRenderer != null)
            {
                originalColor = cachedRenderer.sharedMaterial != null
                    ? cachedRenderer.sharedMaterial.color
                    : Color.white;
                hasOriginalColor = true;
            }
        }
    }

    #endregion

    #region Visuals

    public void Highlight(Color color)
    {
        CacheRenderer();
        if (cachedRenderer == null)
        {
            return;
        }

        cachedRenderer.material.color = color;
    }

    public void ResetColor()
    {
        CacheRenderer();
        if (cachedRenderer == null || !hasOriginalColor)
        {
            return;
        }

        cachedRenderer.material.color = originalColor;
    }

    #endregion
}
