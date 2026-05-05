using UnityEngine;
[DisallowMultipleComponent]
public class ChessPauseInput : MonoBehaviour
{
    void Awake()
    {
        // Pause input is handled centrally by ChessPauseManager so it can run on an always-active object.
        enabled = false;
    }
}
