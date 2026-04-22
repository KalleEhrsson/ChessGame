using UnityEngine;

[DisallowMultipleComponent]
public class ChessAmbientAudio : MonoBehaviour
{
    #region Singleton

    public static ChessAmbientAudio Instance { get; private set; }

    public static ChessAmbientAudio GetOrCreate()
    {
        ChessAudio audioRoot = ChessAudio.GetOrCreate();
        return audioRoot != null ? audioRoot.EnsureAmbientAudio() : null;
    }

    #endregion

    #region Variables

    [SerializeField] AudioClip ambientLoop;
    [SerializeField, Range(0.1f, 0.25f)] float ambientVolume = 0.16f;

    AudioSource ambientSource;

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

    void Start()
    {
        PlayAmbientLoop();
    }

    void OnValidate()
    {
        if (ambientSource != null)
        {
            ConfigureAudioSource(ambientSource);
        }
    }

    #endregion

    #region API

    public void PlayAmbientLoop()
    {
        if (ambientSource == null)
        {
            EnsureAudioSource();
        }

        if (ambientSource == null)
        {
            return;
        }

        ambientSource.clip = ambientLoop;
        ambientSource.volume = ambientVolume;

        if (ambientSource.clip != null && !ambientSource.isPlaying)
        {
            ambientSource.Play();
        }
    }

    #endregion

    #region Helpers

    void EnsureAudioSource()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source != null && source.loop && source.playOnAwake && Mathf.Approximately(source.spatialBlend, 0f))
            {
                ambientSource = source;
                break;
            }
        }

        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(ambientSource);
    }

    void ConfigureAudioSource(AudioSource source)
    {
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f;
        source.volume = ambientVolume;
    }

    #endregion
}
