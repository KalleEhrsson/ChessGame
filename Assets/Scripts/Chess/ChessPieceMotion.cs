using System.Threading.Tasks;
using UnityEngine;
using System;

using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class ChessPieceMotion : MonoBehaviour
{
    #region Variables

    [SerializeField] float pickupDelay = 1f;
    [SerializeField] float normalMoveArcHeight = 4f;
   
    [Header("Capture")]
    [SerializeField, Range(0.5f, 0.99f)] float captureImpactNormalizedTime = 0.9f;
    [SerializeField, Min(0.01f)] float grabDuration = 0.35f;
    [SerializeField, Min(0f)] float grabHoldDuration = 0.12f;
    [SerializeField, Min(0.01f)] float pullUpDuration = 0.55f;
    [SerializeField, Min(0.01f)] float slamDuration = 0.3f;
    [SerializeField, Min(0f)] float minLiftHeight = 0.75f;
    [SerializeField, Min(0f)] float abovePiecePadding = 0.15f;
    [SerializeField, Min(0f)] float fallbackCapturedPieceHeight = 1f;
    
    [Header("Regular Move")]
    [SerializeField] float moveDuration = 1f;
    [SerializeField] float captureMoveDuration = 1.05f;
    [SerializeField] float dropDuration = 0.5f;
    [SerializeField] float settleOvershoot = 0.05f;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip pickupWhoosh;
    [SerializeField] AudioClip dropThud;

    Quaternion baseRotation;
    bool isAnimating;

    static int activeAnimations;

    public static event Action<bool> AnyMotionStateChanged;

    #endregion

    #region Properties

    public bool IsAnimating => isAnimating;
    public static bool IsAnyAnimating => activeAnimations > 0;

    #endregion

    #region Unity

    void Awake()
    {
        EnsureAudioSource();
    }

    void OnDisable()
    {
        if (!isAnimating)
        {
            return;
        }

        isAnimating = false;
        activeAnimations = Mathf.Max(0, activeAnimations - 1);
        if (activeAnimations == 0)
        {
            AnyMotionStateChanged?.Invoke(false);
        }
    }

    #endregion

    #region API

    public async Task PlayMoveAsync(Vector3 startPos, Vector3 endPos, bool isCapture, ChessPiece capturedPiece = null, ChessTile fromTile = null, ChessTile toTile = null, bool debugLogs = false, Action<float> onCaptureImpact = null)
    {
        Vector3 fallbackPosition = transform.position;
        startPos = SanitizePosition(startPos, fallbackPosition);
        endPos = SanitizePosition(endPos, startPos);

        if (!isActiveAndEnabled || isAnimating)
        {
            transform.position = endPos;
            return;
        }

        isAnimating = true;
        activeAnimations++;
        AnyMotionStateChanged?.Invoke(true);
        baseRotation = transform.rotation;

        try
        {
            transform.position = startPos;

            if (pickupDelay > 0f)
            {
                await AwaitSeconds(pickupDelay);
            }

            PlayOneShotRandomized(pickupWhoosh, 0.85f, 1f, 0.95f, 1.05f);

            if (isCapture)
            {
                await PlayCaptureArcMotionAsync(startPos, endPos, Mathf.Max(0.01f, captureMoveDuration), capturedPiece, fromTile, toTile, debugLogs, onCaptureImpact);
            }
            else
            {
                await PlayArcMotionAsync(startPos, endPos, Mathf.Max(0.01f, moveDuration));
            }

            PlayOneShotRandomized(dropThud, 0.95f, 1.1f, 0.94f, 1.02f);
            await PlayDropSettleAsync(endPos, Mathf.Max(0.01f, dropDuration), isCapture);
        }
        finally
        {
            transform.position = endPos;
            transform.rotation = baseRotation;
            isAnimating = false;
            activeAnimations = Mathf.Max(0, activeAnimations - 1);
            if (activeAnimations == 0)
            {
                AnyMotionStateChanged?.Invoke(false);
            }
        }
    }

    #endregion

    #region Motion

    async Task PlayArcMotionAsync(Vector3 startPos, Vector3 endPos, float duration)
    {
        startPos = SanitizePosition(startPos, transform.position);
        endPos = SanitizePosition(endPos, startPos);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            float arc = Mathf.Sin(t * Mathf.PI);
            float shapedArc = Mathf.Pow(Mathf.Max(0f, arc), 0.85f);
            pos.y += shapedArc * normalMoveArcHeight;

            pos = SanitizePosition(pos, endPos);
            transform.position = pos;
            transform.rotation = baseRotation * BuildTilt(t);

            await AwaitNextFrame();
        }

        transform.position = endPos;
    }

    async Task PlayCaptureArcMotionAsync(Vector3 startPos, Vector3 endPos, float duration, ChessPiece capturedPiece, ChessTile fromTile, ChessTile toTile, bool debugLogs, Action<float> onCaptureImpact)
    {
        startPos = SanitizePosition(startPos, transform.position);
        endPos = SanitizePosition(endPos, startPos);

        Bounds capturedBounds = ResolveWorldBounds(capturedPiece != null ? capturedPiece.gameObject : null, out bool hasBounds);
        float capturedPieceHeight = hasBounds
            ? Mathf.Max(0.01f, capturedBounds.size.y)
            : Mathf.Max(0.01f, fallbackCapturedPieceHeight);

        if (!hasBounds)
        {
            Debug.LogWarning($"[ChessPieceMotion] Could not resolve bounds for captured piece. Using fallback height={fallbackCapturedPieceHeight:0.###}.", this);
        }

        Bounds attackerBounds = ResolveWorldBounds(gameObject, out bool hasAttackerBounds);
        float attackerHeight = hasAttackerBounds ? Mathf.Max(0.01f, attackerBounds.size.y) : fallbackCapturedPieceHeight;
        float attackerHalfHeight = attackerHeight * 0.5f;
        float minimumLift = Mathf.Max(minLiftHeight, capturedPieceHeight * 0.1f);
        float safeYFromCaptured = hasBounds
            ? capturedBounds.max.y + attackerHalfHeight + abovePiecePadding
            : endPos.y + capturedPieceHeight + attackerHalfHeight + abovePiecePadding;
        float safeYFromStart = startPos.y + minimumLift;
        float safeY = Mathf.Max(safeYFromCaptured, safeYFromStart);

        Vector3 targetBottom = hasBounds
            ? new Vector3(endPos.x, capturedBounds.min.y, endPos.z)
            : endPos;
        Vector3 safeStartAbove = new Vector3(startPos.x, safeY, startPos.z);
        Vector3 safeTargetAbove = new Vector3(endPos.x, safeY, endPos.z);

        safeStartAbove = SanitizePosition(safeStartAbove, startPos);
        safeTargetAbove = SanitizePosition(safeTargetAbove, safeStartAbove);
        targetBottom = SanitizePosition(targetBottom, endPos);

        if (!IsFinite(safeStartAbove) || !IsFinite(safeTargetAbove) || !IsFinite(targetBottom))
        {
            Debug.LogWarning("[ChessPieceMotion] Invalid capture motion positions detected. Falling back to standard move.", this);
            await PlayArcMotionAsync(startPos, endPos, duration);
            return;
        }

        if (debugLogs)
        {
            Debug.Log($"[ChessPieceMotion] Capture motion start. Piece={name}, SafeY={safeY:0.###}, ImpactThreshold={captureImpactNormalizedTime:0.###}", this);
        }

        await PlaySegmentAsync(startPos, safeStartAbove, grabDuration);
        transform.rotation = baseRotation;
        if (grabHoldDuration > 0f)
        {
            await AwaitSeconds(grabHoldDuration);
        }

        await PlaySegmentAsync(safeStartAbove, safeTargetAbove, pullUpDuration);

        if (debugLogs)
        {
            Debug.Log($"[ChessPieceMotion] Slam phase started. Piece={name}, From={fromTile?.TileName}, To={toTile?.TileName}, Capture=True", this);
        }

        await PlayCaptureSlamAsync(safeTargetAbove, targetBottom, slamDuration, onCaptureImpact, debugLogs);

        transform.position = targetBottom;
    }


    async Task PlayCaptureSlamAsync(Vector3 from, Vector3 to, float duration, Action<float> onCaptureImpact, bool debugLogs)
    {
        float elapsed = 0f;
        bool impactTriggered = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - t, 4f);
            Vector3 pos = Vector3.Lerp(from, to, eased);
            if (!impactTriggered && t >= captureImpactNormalizedTime)
            {
                impactTriggered = true;
                onCaptureImpact?.Invoke(t);
                if (debugLogs)
                {
                    Debug.Log($"[ChessPieceMotion] Capture impact triggered at t={t:0.###}. Piece={name}", this);
                }
            }

            transform.position = SanitizePosition(pos, to);
            transform.rotation = baseRotation;
            await AwaitNextFrame();
        }

        if (!impactTriggered)
        {
            onCaptureImpact?.Invoke(1f);
        }

        transform.position = to;
    }

    async Task PlayDropSettleAsync(Vector3 endPos, float duration, bool isCapture)
    {
        float overshoot = isCapture ? settleOvershoot * 1.1f : settleOvershoot;
        Vector3 downPos = endPos + Vector3.down * overshoot;

        float downDuration = duration * 0.55f;
        float upDuration = duration - downDuration;

        await PlaySegmentAsync(endPos, downPos, Mathf.Max(0.01f, downDuration));
        await PlaySegmentAsync(downPos, endPos, Mathf.Max(0.01f, upDuration));
    }

    async Task PlaySegmentAsync(Vector3 from, Vector3 to, float duration)
    {
        from = SanitizePosition(from, transform.position);
        to = SanitizePosition(to, from);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            Vector3 pos = Vector3.Lerp(from, to, eased);
            pos = SanitizePosition(pos, to);

            transform.position = pos;
            transform.rotation = baseRotation;

            await AwaitNextFrame();
        }

        transform.position = to;
    }

    Quaternion BuildTilt(float t)
    {
        return Quaternion.Euler(
            Mathf.Sin(t * Mathf.PI) * 5f,
            0f,
            Mathf.Cos(t * Mathf.PI) * 3f);
    }

    #endregion

    #region Helpers

    void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    void PlayOneShotRandomized(AudioClip clip, float volumeMin, float volumeMax, float pitchMin, float pitchMax)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        float volume = Random.Range(volumeMin, volumeMax);
        float pitch = Random.Range(pitchMin, pitchMax);
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
        audioSource.pitch = 1f;
    }

    static async Task AwaitNextFrame()
    {
        await Task.Yield();
    }

    static async Task AwaitSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            await AwaitNextFrame();
        }
    }


    static Bounds ResolveWorldBounds(GameObject root, out bool hasBounds)
    {
        hasBounds = false;
        Bounds combined = default;
        if (root == null)
        {
            return combined;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (TryCombineBounds(renderers, out combined))
        {
            hasBounds = true;
            return combined;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (TryCombineBounds(colliders, out combined))
        {
            hasBounds = true;
        }

        return combined;
    }

    static bool TryCombineBounds<T>(T[] components, out Bounds bounds) where T : Component
    {
        bounds = default;
        bool initialized = false;
        if (components == null || components.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                continue;
            }

            Bounds componentBounds = components[i] switch
            {
                Renderer renderer => renderer.bounds,
                Collider collider => collider.bounds,
                _ => default
            };

            if (!initialized)
            {
                bounds = componentBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(componentBounds);
            }
        }

        return initialized;
    }

    static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    static Vector3 SanitizePosition(Vector3 candidate, Vector3 fallback)
    {
        if (float.IsFinite(candidate.x) && float.IsFinite(candidate.y) && float.IsFinite(candidate.z))
        {
            return candidate;
        }

        return float.IsFinite(fallback.x) && float.IsFinite(fallback.y) && float.IsFinite(fallback.z)
            ? fallback
            : Vector3.zero;
    }

    #endregion
}
