using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BrokenPieceCleanupEffect : MonoBehaviour
{
    [Header("Settle Detection")]
    [SerializeField, Min(0f)] float linearVelocityThreshold = 0.08f;
    [SerializeField, Min(0f)] float angularVelocityThreshold = 0.15f;
    [SerializeField, Min(0f)] float requiredSettledTime = 0.35f;

    [Header("Board Bounds")]
    [SerializeField, Min(0f)] float boardOutOfBoundsMargin = 1.5f;
    [SerializeField] bool cleanupWhenAllFragmentsOutOfBounds = true;

    [Header("Cleanup")]
    [SerializeField, Min(0.01f)] float shrinkFadeDuration = 0.45f;
    [SerializeField, Min(0f)] float noRigidbodiesSafetyDelay = 0.35f;
    [SerializeField] AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] ParticleSystem impactParticlesPrefab;
    [SerializeField] bool destroyRootAfterCleanup = true;

    readonly List<Transform> fragments = new(32);
    readonly List<Vector3> originalScales = new(32);
    readonly List<Rigidbody> rigidbodies = new(32);
    readonly List<RendererFadeData> renderers = new(16);

    MaterialPropertyBlock propertyBlock;
    Bounds boardBounds;
    bool hasBoardBounds;
    bool cleanupStarted;
    bool initialized;
    bool hasWarnedMissingBoardBounds;
    bool hasWarnedMissingRigidbodies;
    float settledTimer;
    float cleanupStartTime;
    float noRigidbodiesStartTime;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    struct RendererFadeData
    {
        public Renderer Renderer;
        public bool HasColor;
        public bool HasBaseColor;
        public Color OriginalColor;
    }

    #region Setup

    public void Initialize(
        float fadeDuration,
        AnimationCurve shrink,
        AnimationCurve fade,
        ParticleSystem particlesPrefab,
        bool destroyRoot,
        Vector3 impactPosition)
    {
        shrinkFadeDuration = Mathf.Max(0.01f, fadeDuration);
        if (shrink != null && shrink.length > 0)
        {
            shrinkCurve = shrink;
        }

        if (fade != null && fade.length > 0)
        {
            fadeCurve = fade;
        }

        impactParticlesPrefab = particlesPrefab;
        destroyRootAfterCleanup = destroyRoot;

        CacheTargets();
        TryResolveBoardBounds();
        SpawnImpactParticles(impactPosition);

        initialized = true;
        noRigidbodiesStartTime = Time.time;
    }

    void Awake()
    {
        CacheTargets();
        TryResolveBoardBounds();
    }

    void OnEnable()
    {
        if (!initialized)
        {
            noRigidbodiesStartTime = Time.time;
        }
    }

    void CacheTargets()
    {
        fragments.Clear();
        originalScales.Clear();
        rigidbodies.Clear();
        renderers.Clear();

        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform fragment = allTransforms[i];
            if (fragment == null || fragment == transform)
            {
                continue;
            }

            fragments.Add(fragment);
            originalScales.Add(fragment.localScale);
        }

        Rigidbody[] allBodies = GetComponentsInChildren<Rigidbody>(true);
        rigidbodies.AddRange(allBodies);
        if (rigidbodies.Count == 0 && !hasWarnedMissingRigidbodies)
        {
            hasWarnedMissingRigidbodies = true;
            Debug.LogWarning("[BrokenPieceCleanupEffect] No fragment rigidbodies were found. Falling back to safety-delay cleanup.", this);
        }

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer rendererTarget = allRenderers[i];
            if (rendererTarget == null)
            {
                continue;
            }

            Material sharedMaterial = rendererTarget.sharedMaterial;
            bool hasBaseColor = sharedMaterial != null && sharedMaterial.HasProperty(BaseColorId);
            bool hasColor = !hasBaseColor && sharedMaterial != null && sharedMaterial.HasProperty(ColorId);
            Color color = hasBaseColor
                ? sharedMaterial.GetColor(BaseColorId)
                : hasColor
                    ? sharedMaterial.GetColor(ColorId)
                    : Color.white;

            renderers.Add(new RendererFadeData
            {
                Renderer = rendererTarget,
                HasColor = hasColor,
                HasBaseColor = hasBaseColor,
                OriginalColor = color
            });
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    void TryResolveBoardBounds()
    {
        ChessBoard board = ChessBoard.Instance;
        ChessTile[] tiles = board != null ? board.GetAllTiles() : null;
        if (tiles == null || tiles.Length == 0)
        {
            hasBoardBounds = false;
            if (!hasWarnedMissingBoardBounds)
            {
                hasWarnedMissingBoardBounds = true;
                Debug.LogWarning("[BrokenPieceCleanupEffect] Board bounds could not be resolved from ChessBoard tiles.", this);
            }
            return;
        }

        bool hasAnyTile = false;
        Bounds computedBounds = default;

        for (int i = 0; i < tiles.Length; i++)
        {
            ChessTile tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            if (!hasAnyTile)
            {
                computedBounds = new Bounds(tile.transform.position, Vector3.zero);
                hasAnyTile = true;
            }
            else
            {
                computedBounds.Encapsulate(tile.transform.position);
            }

            if (tile.TryGetComponent<Renderer>(out Renderer tileRenderer))
            {
                computedBounds.Encapsulate(tileRenderer.bounds);
            }
            else if (tile.TryGetComponent<Collider>(out Collider tileCollider))
            {
                computedBounds.Encapsulate(tileCollider.bounds);
            }
        }

        if (!hasAnyTile)
        {
            hasBoardBounds = false;
            if (!hasWarnedMissingBoardBounds)
            {
                hasWarnedMissingBoardBounds = true;
                Debug.LogWarning("[BrokenPieceCleanupEffect] Board bounds could not be calculated due to missing tile data.", this);
            }
            return;
        }

        computedBounds.Expand(new Vector3(boardOutOfBoundsMargin * 2f, boardOutOfBoundsMargin * 2f, boardOutOfBoundsMargin * 2f));
        boardBounds = computedBounds;
        hasBoardBounds = true;
    }

    #endregion

    #region Runtime

    void Update()
    {
        if (!Application.isPlaying || cleanupStarted)
        {
            if (cleanupStarted)
            {
                RunCleanupFade();
            }

            return;
        }

        if (ShouldStartCleanup())
        {
            StartCleanup();
        }
    }

    bool ShouldStartCleanup()
    {
        if (rigidbodies.Count == 0)
        {
            return Time.time - noRigidbodiesStartTime >= noRigidbodiesSafetyDelay;
        }

        if (AreFragmentsOutOfBounds())
        {
            return true;
        }

        if (AreAllFragmentsSettled())
        {
            settledTimer += Time.deltaTime;
            return settledTimer >= requiredSettledTime;
        }

        settledTimer = 0f;
        return false;
    }

    bool AreAllFragmentsSettled()
    {
        for (int i = 0; i < rigidbodies.Count; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null)
            {
                continue;
            }

            if (body.linearVelocity.sqrMagnitude > linearVelocityThreshold * linearVelocityThreshold)
            {
                return false;
            }

            if (body.angularVelocity.sqrMagnitude > angularVelocityThreshold * angularVelocityThreshold)
            {
                return false;
            }
        }

        return true;
    }

    bool AreFragmentsOutOfBounds()
    {
        if (!hasBoardBounds)
        {
            return false;
        }

        if (!cleanupWhenAllFragmentsOutOfBounds)
        {
            return !boardBounds.Contains(transform.position);
        }

        int activeCount = 0;
        int outOfBoundsCount = 0;
        for (int i = 0; i < rigidbodies.Count; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null)
            {
                continue;
            }

            activeCount++;
            if (!boardBounds.Contains(body.position))
            {
                outOfBoundsCount++;
            }
        }

        if (activeCount == 0)
        {
            return !boardBounds.Contains(transform.position);
        }

        return outOfBoundsCount >= activeCount;
    }

    void StartCleanup()
    {
        cleanupStarted = true;
        cleanupStartTime = Time.time;
    }

    void RunCleanupFade()
    {
        float t = Mathf.Clamp01((Time.time - cleanupStartTime) / shrinkFadeDuration);
        float scaleFactor = Mathf.Clamp01(shrinkCurve.Evaluate(t));
        float fadeFactor = Mathf.Clamp01(fadeCurve.Evaluate(t));

        for (int i = 0; i < fragments.Count; i++)
        {
            Transform fragment = fragments[i];
            if (fragment == null)
            {
                continue;
            }

            fragment.localScale = originalScales[i] * scaleFactor;
        }

        ApplyFade(fadeFactor);

        if (t >= 1f)
        {
            if (destroyRootAfterCleanup)
            {
                Destroy(gameObject);
            }
            else
            {
                enabled = false;
            }
        }
    }

    void ApplyFade(float fadeFactor)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            RendererFadeData data = renderers[i];
            Renderer targetRenderer = data.Renderer;
            if (targetRenderer == null || (!data.HasBaseColor && !data.HasColor))
            {
                continue;
            }

            Color color = data.OriginalColor;
            color.a *= fadeFactor;

            targetRenderer.GetPropertyBlock(propertyBlock);
            if (data.HasBaseColor)
            {
                propertyBlock.SetColor(BaseColorId, color);
            }
            else
            {
                propertyBlock.SetColor(ColorId, color);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    void SpawnImpactParticles(Vector3 position)
    {
        if (impactParticlesPrefab == null)
        {
            return;
        }

        ParticleSystem instance = Instantiate(impactParticlesPrefab, position, Quaternion.identity);
        instance.Play();
        Destroy(instance.gameObject, instance.main.duration + instance.main.startLifetime.constantMax + 0.15f);
    }

    #endregion
}
