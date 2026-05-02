using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BrokenPieceCleanupEffect : MonoBehaviour
{
    [SerializeField, Min(0f)] float scatterLifetime = 0.75f;
    [SerializeField, Min(0.01f)] float shrinkFadeDuration = 0.45f;
    [SerializeField] AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] ParticleSystem impactParticlesPrefab;
    [SerializeField] bool destroyRootAfterCleanup = true;

    readonly List<Transform> fragments = new(32);
    readonly List<Vector3> originalScales = new(32);
    readonly List<Rigidbody> rigidbodies = new(32);
    readonly List<RendererFadeData> renderers = new(16);

    MaterialPropertyBlock propertyBlock;
    bool started;
    float fadeStartTime;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    struct RendererFadeData
    {
        public Renderer Renderer;
        public bool HasColor;
        public bool HasBaseColor;
        public Color[] OriginalColors;
    }

    #region Setup

    public void Initialize(float scatterDelay, float fadeDuration, AnimationCurve shrink, AnimationCurve fade, ParticleSystem particlesPrefab, bool destroyRoot, Vector3 impactPosition)
    {
        scatterLifetime = Mathf.Max(0f, scatterDelay);
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
        SpawnImpactParticles(impactPosition);
        BeginIfReady();
    }

    void Awake()
    {
        CacheTargets();
    }

    void OnEnable()
    {
        BeginIfReady();
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

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer rendererTarget = allRenderers[i];
            if (rendererTarget == null)
            {
                continue;
            }

            Material[] materials = rendererTarget.sharedMaterials;
            Color[] colors = new Color[materials.Length];
            bool hasColor = false;
            bool hasBaseColor = false;

            for (int matIndex = 0; matIndex < materials.Length; matIndex++)
            {
                Material material = materials[matIndex];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty(ColorId))
                {
                    colors[matIndex] = material.GetColor(ColorId);
                    hasColor = true;
                }
                else if (material.HasProperty(BaseColorId))
                {
                    colors[matIndex] = material.GetColor(BaseColorId);
                    hasBaseColor = true;
                }
                else
                {
                    colors[matIndex] = Color.white;
                }
            }

            renderers.Add(new RendererFadeData
            {
                Renderer = rendererTarget,
                HasColor = hasColor,
                HasBaseColor = hasBaseColor,
                OriginalColors = colors
            });
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    void BeginIfReady()
    {
        if (started || !Application.isPlaying)
        {
            return;
        }

        started = true;
        fadeStartTime = Time.time + scatterLifetime;
    }

    #endregion

    #region Runtime

    void Update()
    {
        if (!started)
        {
            return;
        }

        if (Time.time < fadeStartTime)
        {
            return;
        }

        float t = Mathf.Clamp01((Time.time - fadeStartTime) / shrinkFadeDuration);
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

        for (int i = 0; i < rigidbodies.Count; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null)
            {
                continue;
            }

            body.linearVelocity *= 0.9f;
            body.angularVelocity *= 0.92f;
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
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            int submeshCount = data.OriginalColors.Length;
            for (int matIndex = 0; matIndex < submeshCount; matIndex++)
            {
                Color color = data.OriginalColors[matIndex];
                color.a *= fadeFactor;
                if (data.HasBaseColor)
                {
                    propertyBlock.SetColor(BaseColorId, color);
                }
                else if (data.HasColor)
                {
                    propertyBlock.SetColor(ColorId, color);
                }
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
