using GAToolkit;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sample-only controller for the Idle Timeout demo scene. Puts the seconds number inside each
/// radial, keeps the status line up to date, and maps P and R to pause and resume.
///
/// Not library code - this belongs with the sample scene, not with the runtime component.
///
/// Most of the scene is driven WITHOUT code, straight from IdleTimeoutManager's events:
///
///   onIdleProgress    -> idle radial    Image.fillAmount
///   onWarningProgress -> warning radial Image.fillAmount
///   onWarningStarted  -> Overlay.SetActive(true)
///   onWarningEnded    -> Overlay.SetActive(false)
///
/// Progress reports 1 down to 0, which is exactly what fillAmount takes, so no maths is needed
/// in between.
///
/// Pause and resume are on the KEYBOARD, and only during the idle countdown.
///
/// Keys rather than on-screen buttons because pressing a button is user activity: the zone
/// manager would report it and the countdown would restart a frame before the pause landed,
/// making pause look like it reset the timer. That is correct behaviour, not a bug - in a
/// real app you call PauseTimeout from code when something like a video starts, with no
/// user input involved.
/// </summary>
public class SampleController_IdleTimeout : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField, Tooltip("The IdleTimeoutManager prefab instance in this scene.")]
    private IdleTimeoutManager idleTimeoutManager;

    [Header("Labels")]
    [SerializeField, Tooltip("The number inside the idle radial.")]
    private TMP_Text idleSecondsLabel;

    [SerializeField, Tooltip("The number inside the warning radial, on the overlay.")]
    private TMP_Text warningSecondsLabel;

    [SerializeField, Tooltip("The line of prose beside the idle radial.")]
    private TMP_Text statusLabel;

    void Start()
    {
        Status("Press START to begin.");
        SetNumber(idleSecondsLabel, 0f);
    }

    void Update()
    {
        if (idleTimeoutManager == null)
        {
            return;
        }

        // Stage 1 only. Pausing the warning would hold the overlay on screen with a
        // frozen radial, which is not a state worth demonstrating - and resuming it
        // is the fiddlier path that a sample should not be the first to exercise.
        if (idleTimeoutManager.Stage != IdleTimeoutStage.IdleCountdown)
        {
            return;
        }

        if (PauseKeyPressed())
        {
            idleTimeoutManager.PauseTimeout();
            Status($"<b>PAUSED</b> at {Mathf.CeilToInt(idleTimeoutManager.IdleTimeRemaining)}s.\nInput is ignored while paused. Press R to resume.");
        }
        else if (ResumeKeyPressed())
        {
            idleTimeoutManager.ResumeTimeout();
            Status("Resumed from where it left off.");
        }
    }

    /// <summary>Stage 1 ticking - seconds remaining on the idle countdown.</summary>
    public void OnIdleTick(float secondsRemaining)
    {
        SetNumber(idleSecondsLabel, secondsRemaining);

        if (!idleTimeoutManager.IsPaused)
        {
            Status("Idle countdown running.\nTap, click or drag anywhere to reset it.\nPress P to pause, R to resume.");
        }
    }

    /// <summary>The warning began. The scene also wires this to the overlay's SetActive(true).</summary>
    public void OnWarningStarted()
    {
        Status("No input for the full countdown.\nWarning up - touch anywhere to continue.");
    }

    /// <summary>Stage 2 ticking - seconds remaining on the warning countdown.</summary>
    public void OnWarningTick(float secondsRemaining)
    {
        SetNumber(warningSecondsLabel, secondsRemaining);
    }

    /// <summary>The user touched the screen during the warning, so the idle countdown restarted.</summary>
    public void OnWarningInterrupted()
    {
        Status("Interrupted - back to the idle countdown.");
    }

    /// <summary>The warning ran out. The manager has stopped and is waiting for START again.</summary>
    public void OnWarningTimedOut()
    {
        SetNumber(idleSecondsLabel, 0f);
        Status("<b>TIMED OUT.</b>\nThe manager stopped. A real app would return to its attract screen here.\nPress START to run it again.");
    }


    // Compiles against whichever input backend the project has active, the same way
    // ScreenInputZoneManager does.
    private static bool PauseKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.pKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.P);
#else
        return false;
#endif
    }

    private static bool ResumeKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.rKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }

    private static void SetNumber(TMP_Text label, float seconds)
    {
        if (label != null)
        {
            label.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();
        }
    }

    private void Status(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }
}
