using UnityEngine;

[DisallowMultipleComponent]
public class ChessAudio : MonoBehaviour
{
    #region Singleton

    public static ChessAudio Instance { get; private set; }

    public static ChessAudio GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessAudio existing = FindFirstObjectByType<ChessAudio>();
        if (existing != null)
        {
            Instance = existing;
            Instance.EnsureModules();
            return Instance;
        }

        GameObject root = GameObject.Find("Chess_Audio");
        if (root == null)
        {
            root = new GameObject("Chess_Audio");
        }

        Instance = root.GetComponent<ChessAudio>();
        if (Instance == null)
        {
            Instance = root.AddComponent<ChessAudio>();
        }

        Instance.EnsureModules();
        return Instance;
    }

    #endregion

    #region Unity

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        GetOrCreate();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (gameObject.name != "Chess_Audio")
        {
            gameObject.name = "Chess_Audio";
        }

        EnsureModules();
    }

    #endregion

    #region API

    public ChessAmbientAudio EnsureAmbientAudio()
    {
        ChessAmbientAudio ambient = GetComponent<ChessAmbientAudio>();
        if (ambient == null)
        {
            ambient = gameObject.AddComponent<ChessAmbientAudio>();
        }

        return ambient;
    }

    public ChessUIAudio EnsureUiAudio()
    {
        ChessUIAudio uiAudio = GetComponent<ChessUIAudio>();
        if (uiAudio == null)
        {
            uiAudio = gameObject.AddComponent<ChessUIAudio>();
        }

        return uiAudio;
    }

    #endregion

    #region Helpers

    void EnsureModules()
    {
        EnsureAmbientAudio();
        EnsureUiAudio();
    }

    #endregion
}
