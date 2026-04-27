using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessPieceMotion : MonoBehaviour
{
    #region Variables

    [SerializeField] float pickupDelay = 1f;
    [SerializeField] float liftHeight = 4f;
    [SerializeField] float moveDuration = 1f;
    [SerializeField] float dropDuration = 0.5f;
    [SerializeField] float settleOvershoot = 0.05f;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip pickupWhoosh;
    [SerializeField] AudioClip dropThud;

    Quaternion baseRotation;
    bool isAnimating;

    static int activeAnimations;

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
        isAnimating = false;
    }

    #endregion

    #region API

    public async Task PlayMoveAsync(Vector3 startPos, Vector3 endPos, bool isCapture)
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
        baseRotation = transform.rotation;

        try
        {
            transform.position = startPos;

            if (pickupDelay > 0f)
            {
                await AwaitSeconds(pickupDelay);
            }

            PlayOneShotRandomized(pickupWhoosh, 0.85f, 1f, 0.95f, 1.05f);
            await PlayArcMotionAsync(startPos, endPos, Mathf.Max(0.01f, moveDuration));

            PlayOneShotRandomized(dropThud, 0.95f, 1.1f, 0.94f, 1.02f);
            await PlayDropSettleAsync(endPos, Mathf.Max(0.01f, dropDuration), isCapture);
        }
        finally
        {
            transform.position = endPos;
            transform.rotation = baseRotation;
            isAnimating = false;
            activeAnimations = Mathf.Max(0, activeAnimations - 1);
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
            pos.y += shapedArc * liftHeight;

            pos = SanitizePosition(pos, endPos);

            transform.position = pos;
            transform.rotation = baseRotation * BuildTilt(t);

            await AwaitNextFrame();
        }

        transform.position = endPos;
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

    static Vector3 SanitizePosition(Vector3 candidate, Vector3 fallback)
    {
        if (!IsFinite(candidate))
        {
            return IsFinite(fallback) ? fallback : Vector3.zero;
        }

        return candidate;
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    
    #endregion
}
