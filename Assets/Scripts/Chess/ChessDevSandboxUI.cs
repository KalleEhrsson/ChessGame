using UnityEngine;

[DisallowMultipleComponent]
public class ChessDevSandboxUI : MonoBehaviour
{
    ChessDevSandboxController controller;
    Vector2 scroll;
    Rect windowRect = new(20f, 20f, 420f, 520f);

    #region Unity

    void Awake()
    {
        controller = GetComponent<ChessDevSandboxController>();
    }

    void OnGUI()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif
        if (controller == null)
        {
            controller = GetComponent<ChessDevSandboxController>();
            return;
        }

        if (!controller.IsOpen)
        {
            return;
        }

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Chess Dev Sandbox");
    }

    #endregion

    #region UI

    void DrawWindow(int _)
    {
        GUILayout.BeginVertical();
        scroll = GUILayout.BeginScrollView(scroll, false, true);

        DrawRuntimeActions();
        GUILayout.Space(8f);
        DrawModeSection();
        GUILayout.Space(8f);
        DrawPresetSection();
        GUILayout.Space(8f);
        DrawFenSection();

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0f, 0f, 4000f, 22f));
    }

    void DrawRuntimeActions()
    {
        GUILayout.Label("Board Actions");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Board"))
        {
            controller.ResetBoard();
        }

        if (GUILayout.Button("Clear Board"))
        {
            controller.ClearBoard();
        }

        if (GUILayout.Button("Export FEN"))
        {
            controller.ExportFen();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Turn: White"))
        {
            controller.SetSideToMove(PieceTeam.White);
        }

        if (GUILayout.Button("Turn: Black"))
        {
            controller.SetSideToMove(PieceTeam.Black);
        }

        bool aiEnabled = GUILayout.Toggle(controller.AiEnabled, "AI Enabled (Both Teams)");
        if (aiEnabled != controller.AiEnabled)
        {
            controller.ToggleAi(aiEnabled);
        }

        GUILayout.EndHorizontal();
    }

    void DrawModeSection()
    {
        GUILayout.Label("Click Modes");
        GUILayout.BeginHorizontal();
        DrawModeButton("Off", ChessDevSandboxController.SandboxMode.None);
        DrawModeButton("Place", ChessDevSandboxController.SandboxMode.Place);
        DrawModeButton("Remove", ChessDevSandboxController.SandboxMode.Remove);
        DrawModeButton("Move", ChessDevSandboxController.SandboxMode.Move);
        GUILayout.EndHorizontal();

        if (controller.Mode == ChessDevSandboxController.SandboxMode.Place)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Side: {controller.SelectedTeam}"))
            {
                controller.SetSelectedTeam(controller.SelectedTeam == PieceTeam.White ? PieceTeam.Black : PieceTeam.White);
            }

            if (GUILayout.Button($"Piece: {controller.SelectedPieceType}"))
            {
                int next = ((int)controller.SelectedPieceType + 1) % System.Enum.GetValues(typeof(PieceType)).Length;
                controller.SetSelectedPieceType((PieceType)next);
            }

            GUILayout.EndHorizontal();
        }
    }

    void DrawPresetSection()
    {
        GUILayout.Label("Presets");
        var presets = controller.Presets;
        if (presets.Count == 0)
        {
            GUILayout.Label("No presets available.");
            return;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(28f)))
        {
            controller.SetPresetIndex(controller.PresetIndex - 1);
        }

        GUILayout.Label(presets[controller.PresetIndex].Name, GUILayout.ExpandWidth(true));

        if (GUILayout.Button(">", GUILayout.Width(28f)))
        {
            controller.SetPresetIndex(controller.PresetIndex + 1);
        }

        if (GUILayout.Button("Load", GUILayout.Width(72f)))
        {
            controller.LoadSelectedPreset();
        }

        GUILayout.EndHorizontal();
    }

    void DrawFenSection()
    {
        GUILayout.Label("FEN");
        string newFen = GUILayout.TextArea(controller.FenBuffer, GUILayout.MinHeight(72f));
        if (newFen != controller.FenBuffer)
        {
            controller.SetFenBuffer(newFen);
        }

        if (GUILayout.Button("Import FEN"))
        {
            controller.ImportFen();
        }
    }

    void DrawModeButton(string label, ChessDevSandboxController.SandboxMode mode)
    {
        bool isActive = controller.Mode == mode;
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = isActive ? new Color(0.5f, 0.8f, 1f) : oldColor;
        if (GUILayout.Button(label))
        {
            controller.SetMode(mode);
        }

        GUI.backgroundColor = oldColor;
    }

    #endregion
}
