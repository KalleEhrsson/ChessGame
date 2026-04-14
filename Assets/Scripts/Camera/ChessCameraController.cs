using UnityEngine;

public class ChessCameraController : MonoBehaviour
{
    #region Singleton

    public static ChessCameraController Instance;

    void Awake()
    {
        Instance = this;
    }

    #endregion

    #region Variables

    public Transform playerCamera;
    public Transform tacticalCameraPoint;

    public float transitionSpeed = 5f;

    private bool inTacticalView;

    #endregion

    #region Unity

    void Update()
    {
        HandleTransition();
    }

    #endregion

    #region Camera

    public void EnterTacticalView(Transform piece)
    {
        inTacticalView = true;
    }

    public void ExitTacticalView()
    {
        inTacticalView = false;
    }

    void HandleTransition()
    {
        if (inTacticalView)
        {
            playerCamera.position = Vector3.Lerp(playerCamera.position, tacticalCameraPoint.position, Time.deltaTime * transitionSpeed);
            playerCamera.rotation = Quaternion.Lerp(playerCamera.rotation, tacticalCameraPoint.rotation, Time.deltaTime * transitionSpeed);
        }
    }

    #endregion
}