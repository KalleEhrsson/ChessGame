using UnityEngine;

public static class ChessCursorStateCoordinator
{
    static bool gameplayWantsLocked = true;
    static bool pauseWantsUnlocked;
    static bool tacticalWantsUnlocked;

    public static void SetGameplayLockPreference(bool wantsLocked)
    {
        gameplayWantsLocked = wantsLocked;
        Apply();
    }

    public static void SetPauseCursorOverride(bool wantsUnlocked)
    {
        pauseWantsUnlocked = wantsUnlocked;
        Apply();
    }

    public static void SetTacticalCursorOverride(bool wantsUnlocked)
    {
        tacticalWantsUnlocked = wantsUnlocked;
        Apply();
    }

    public static bool IsCursorUnlockedByOverride()
    {
        return tacticalWantsUnlocked || pauseWantsUnlocked;
    }

    static void Apply()
    {
        bool shouldUnlock = tacticalWantsUnlocked || pauseWantsUnlocked;
        if (shouldUnlock)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = gameplayWantsLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplayWantsLocked;
    }
}
