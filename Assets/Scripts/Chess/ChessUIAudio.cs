using UnityEngine;

[DisallowMultipleComponent]
public class ChessUIAudio : MonoBehaviour
{
    #region Singleton

    public static ChessUIAudio Instance { get; private set; }

    public static ChessUIAudio GetOrCreate()
    {
        ChessAudio audioRoot = ChessAudio.GetOrCreate();
        return audioRoot != null ? audioRoot.EnsureUiAudio() : null;
    }

    #endregion

    #region Variables

    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clickSound;
    [SerializeField] AudioClip invalidSound;

    #endregion

    #region Unity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureAudioSource();
    }

    #endregion

    #region API

    public void PlaySelectionClick()
    {
        PlayClick(0.28f, 0.98f, 1.03f);
    }

    public void PlayValidMoveClick()
    {
        PlayClick(0.36f, 0.97f, 1.04f);
    }

    public void PlayTileTap()
    {
        PlayClick(0.2f, 0.98f, 1.02f);
    }

    public void PlayInvalid()
    {
        if (audioSource == null || invalidSound == null)
        {
            return;
        }

        float pitch = Random.Range(0.9f, 0.97f);
        float volume = Random.Range(0.18f, 0.26f);
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(invalidSound, volume);
        audioSource.pitch = 1f;
    }

    #endregion

    #region Helpers

    void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source != null && !source.loop)
                {
                    audioSource = source;
                    break;
                }
            }
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    void PlayClick(float volume, float pitchMin, float pitchMax)
    {
        if (audioSource == null || clickSound == null)
        {
            return;
        }

        float pitch = Random.Range(pitchMin, pitchMax);
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clickSound, volume);
        audioSource.pitch = 1f;
    }

    #endregion
}
