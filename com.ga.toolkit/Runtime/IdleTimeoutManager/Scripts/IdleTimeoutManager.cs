using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace GAToolkit
{
    /// <summary>
    /// Which stage an IdleTimeoutManager is in. One value at a time, so the nonsense
    /// combinations two separate booleans allowed - "warning up but not running" - cannot
    /// be represented. Pause is tracked separately, because it applies to either stage.
    /// </summary>
    public enum IdleTimeoutStage
    {
        /// <summary>No session. Nothing counts down and input is ignored.</summary>
        Inactive,

        /// <summary>Stage 1. Counting down to the warning; any input restarts it.</summary>
        IdleCountdown,

        /// <summary>Stage 2. The warning is up and counting down to the timeout.</summary>
        WarningCountdown,
    }

    /// <summary>
    /// Two-stage inactivity timeout for kiosks.
    ///
    ///   Stage 1 - the idle countdown. Runs while the user is doing nothing. ANY input restarts
    ///             it, so it only completes if the screen has genuinely been left alone.
    ///   Stage 2 - the warning. When stage 1 completes, onWarningStarted fires (show your
    ///             overlay) and a second, shorter countdown starts. Input during this stage
    ///             dismisses the overlay and sends the user back to stage 1.
    ///
    /// If stage 2 completes, onWarningTimedOut fires and the manager stops. It does not decide
    /// what happens next - reloading the scene or returning to an attract loop is the consumer's.
    ///
    /// This component is purely functional. It owns no visuals: the overlay lives in the scene
    /// and is shown by onWarningStarted and hidden by onWarningEnded. The countdown appearance -
    /// radial, bar, number - is whatever the consumer wires the Tick and Progress events to.
    ///
    /// Input comes from a ScreenInputZoneManager, which observes presses WITHOUT consuming them,
    /// so the countdown resets even while the user is pressing real buttons. Wire that component's
    /// On Input Anywhere to OnUserInput.
    ///
    /// Needs two separate TimerManagers, one per stage.
    /// </summary>
    [AddComponentMenu("GA Toolkit/Idle Timeout Manager")]
    [DisallowMultipleComponent]
    public class IdleTimeoutManager : MonoBehaviour
    {
        #region Inspector

        [Header("Setup")]

        [SerializeField, Tooltip("Log each stage change.")]
        private bool showLogs = false;

        [SerializeField, Tooltip("Start the idle countdown as soon as the scene loads. Usually off: an app's attract state runs first, and the controller calls StartTimeout when a session actually begins.")]
        private bool startOnAwake = false;

        [SerializeField, FormerlySerializedAs("screenInputZoneController"),
         Tooltip("Turned on by StartTimeout and off by StopTimeout. This reference does NOT wire the reset - connect its On Input Anywhere to OnUserInput in the inspector.")]
        private ScreenInputZoneManager screenInputZoneManager;

        [Header("Stage 1 - Idle Countdown")]

        [SerializeField, Tooltip("Timer for the idle countdown. Must be a different TimerManager from the warning one.")]
        private TimerManager idleTimer;

        [SerializeField, Tooltip("Seconds of no input before the warning appears.")]
        private float idleCountdownDuration = 60f;

        [Header("Stage 2 - Warning")]

        [SerializeField, Tooltip("Timer for the warning countdown. Must be a different TimerManager from the idle one.")]
        private TimerManager warningTimer;

        [SerializeField, Tooltip("Seconds the warning lasts before the session times out.")]
        private float warningCountdownDuration = 10f;

        [Header("Events")]

        [Tooltip("Seconds remaining on the idle countdown, every frame it runs. For a number on screen.")]
        public UnityEvent<float> onIdleTick;

        [Tooltip("Idle countdown progress, 1 down to 0. Wire straight to an Image's fillAmount.")]
        public UnityEvent<float> onIdleProgress;

        [Tooltip("True when the warning begins, false when it ends - whether dismissed or timed out. Wire straight to your overlay's SetActive: one connection that cannot get out of sync.")]
        public UnityEvent onWarningStarted;

        [Tooltip("Seconds remaining on the warning countdown, every frame it runs. For a number on screen.")]
        public UnityEvent<float> onWarningTick;

        [Tooltip("Warning progress, 1 down to 0. Wire straight to an Image's fillAmount.")]
        public UnityEvent<float> onWarningProgress;

        [Tooltip("The warning is over, whichever way it ended. Wire your overlay's SetActive(false) here - one hide, so it cannot be left up.")]
        public UnityEvent onWarningEnded;

        [Tooltip("The warning ended because the user touched the screen. The idle countdown has restarted. onWarningEnded fires too; this one is for anything that cares WHY.")]
        public UnityEvent onWarningInterrupted;

        [Tooltip("The warning ended because it ran out with no input. The manager has stopped. onWarningEnded fires too; this one is for anything that cares WHY.")]
        public UnityEvent onWarningTimedOut;

        #endregion

        #region State

        /// <summary>Which stage the component is in. The single source of truth for its state.</summary>
        public IdleTimeoutStage Stage { get; private set; } = IdleTimeoutStage.Inactive;

        /// <summary>
        /// True while suspended by PauseTimeout. Tracked separately from Stage because pausing
        /// applies to either countdown. Input is ignored until ResumeTimeout.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>Seconds left on the idle countdown, or 0 if no idle timer is assigned.</summary>
        public float IdleTimeRemaining => idleTimer != null ? idleTimer.TimeRemaining : 0f;

        /// <summary>Idle countdown progress, 1 down to 0.</summary>
        public float IdleProgress => idleTimer != null ? idleTimer.Progress : 0f;

        /// <summary>How long a full idle countdown lasts.</summary>
        public float IdleDuration => idleCountdownDuration;

        /// <summary>Seconds left on the warning countdown, or 0 if no warning timer is assigned.</summary>
        public float WarningTimeRemaining => warningTimer != null ? warningTimer.TimeRemaining : 0f;

        /// <summary>Warning countdown progress, 1 down to 0.</summary>
        public float WarningProgress => warningTimer != null ? warningTimer.Progress : 0f;

        /// <summary>How long a full warning countdown lasts.</summary>
        public float WarningDuration => warningCountdownDuration;

        #endregion

        #region Life Cycle

        void Start()
        {
            ValidateTimers();

            // The timers tick themselves in their own Update. This component must never call
            // RunTimer as well, or every countdown would run at double speed.
            PrepareTimer(idleTimer, idleCountdownDuration);
            PrepareTimer(warningTimer, warningCountdownDuration);

            Subscribe();

            if (startOnAwake)
            {
                StartTimeout();
            }
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Begins a session: activates the manager and starts the idle countdown from full.
        /// The only call a start button or a controller leaving its attract state needs.
        /// </summary>
        public void StartTimeout()
        {
            IsPaused = false;
            SetStage(IdleTimeoutStage.IdleCountdown);

            if (screenInputZoneManager != null)
            {
                screenInputZoneManager.SetComponentActive(true);
            }

            if (warningTimer != null)
            {
                warningTimer.ResetTimer();
            }

            RestartIdleCountdown();
            Log("Started - idle countdown running.");
        }

        /// <summary>Ends the session. Both countdowns stop and the manager goes inactive.</summary>
        public void StopTimeout()
        {
            IsPaused = false;
            SetStage(IdleTimeoutStage.Inactive);

            if (idleTimer != null)
            {
                idleTimer.ResetTimer();
            }

            if (warningTimer != null)
            {
                warningTimer.ResetTimer();
            }

            if (screenInputZoneManager != null)
            {
                screenInputZoneManager.SetComponentActive(false);
            }

            Log("Stopped.");
        }

        /// <summary>
        /// Suspends whichever countdown is live, keeping its position - for a video or anything
        /// else that should not count as idling. Input is ignored until ResumeTimeout, so a stray
        /// tap cannot restart a deliberately suspended session.
        /// </summary>
        public void PauseTimeout()
        {
            if (Stage == IdleTimeoutStage.Inactive || IsPaused)
            {
                return;
            }

            IsPaused = true;

            if (Stage == IdleTimeoutStage.WarningCountdown)
            {
                if (warningTimer != null) warningTimer.PauseTimer();
            }
            else
            {
                if (idleTimer != null) idleTimer.PauseTimer();
            }

            Log("Paused.");
        }

        /// <summary>Continues the suspended countdown from where PauseTimeout left it.</summary>
        public void ResumeTimeout()
        {
            if (Stage == IdleTimeoutStage.Inactive || !IsPaused)
            {
                return;
            }

            IsPaused = false;

            // Only the stage that was live gets resumed. Resuming both would start the warning
            // countdown during stage 1, because its clock is sitting at full.
            if (Stage == IdleTimeoutStage.WarningCountdown)
            {
                if (warningTimer != null) warningTimer.ResumeTimer();
            }
            else
            {
                if (idleTimer != null) idleTimer.ResumeTimer();
            }

            Log("Resumed.");
        }

        /// <summary>
        /// Something counted as activity. During the warning this also dismisses it, which is why
        /// the overlay closes on a tap anywhere rather than needing a button.
        ///
        /// Wire a ScreenInputZoneManager's On Input Anywhere straight to this. Anything else that
        /// counts as activity - a keyboard, a scanner, a sensor - can call it too.
        /// </summary>
        public void OnUserInput()
        {
            if (Stage == IdleTimeoutStage.Inactive || IsPaused)
            {
                return;
            }

            if (Stage == IdleTimeoutStage.WarningCountdown)
            {
                if (warningTimer != null)
                {
                    warningTimer.ResetTimer();
                }

                SetStage(IdleTimeoutStage.IdleCountdown);
                onWarningInterrupted?.Invoke();
                Log("Warning interrupted by input - back to the idle countdown.");
            }

            RestartIdleCountdown();
        }

        public void SetIdleCountdownDuration(float seconds)
        {
            idleCountdownDuration = Mathf.Max(0f, seconds);

            if (idleTimer != null)
            {
                idleTimer.SetTimerDuration(idleCountdownDuration);
            }
        }

        public void SetWarningCountdownDuration(float seconds)
        {
            warningCountdownDuration = Mathf.Max(0f, seconds);

            if (warningTimer != null)
            {
                warningTimer.SetTimerDuration(warningCountdownDuration);
            }
        }

        #endregion

        #region Stage Transitions

        /// <summary>
        /// Private on purpose. Restarting stage 1 without also clearing the warning would leave
        /// both countdowns running - the idle one reporting nothing, the warning one still able
        /// to time out. OnUserInput is the public door, because it does both as one action.
        /// </summary>
        private void RestartIdleCountdown()
        {
            if (idleTimer == null)
            {
                return;
            }

            idleTimer.SetTimerDuration(idleCountdownDuration);
            idleTimer.StartTimer();
        }

        private void OnIdleTimerDone()
        {
            if (Stage != IdleTimeoutStage.IdleCountdown)
            {
                return;
            }

            SetStage(IdleTimeoutStage.WarningCountdown);

            if (warningTimer != null)
            {
                warningTimer.SetTimerDuration(warningCountdownDuration);
                warningTimer.StartTimer();
            }

            Log("Idle countdown done - warning up.");
        }

        private void OnWarningTimerDone()
        {
            if (Stage != IdleTimeoutStage.WarningCountdown)
            {
                return;
            }

            // Stop BEFORE announcing. The manager lands on Inactive - which also hides the overlay
            // via onWarningEnded - so a handler is free to call StartTimeout right back, without
            // this method then tearing down the session it just started.
            StopTimeout();

            onWarningTimedOut?.Invoke();
            Log("Timed out.");
        }

        private void OnIdleTimerTick(float secondsRemaining)
        {
            if (Stage != IdleTimeoutStage.IdleCountdown)
            {
                return;
            }

            onIdleTick?.Invoke(Mathf.Max(0f, secondsRemaining));
            onIdleProgress?.Invoke(Normalize(secondsRemaining, idleCountdownDuration));
        }

        private void OnWarningTimerTick(float secondsRemaining)
        {
            if (Stage != IdleTimeoutStage.WarningCountdown)
            {
                return;
            }

            onWarningTick?.Invoke(Mathf.Max(0f, secondsRemaining));
            onWarningProgress?.Invoke(Normalize(secondsRemaining, warningCountdownDuration));
        }

        /// <summary>
        /// The one place Stage changes, so onWarningStarted and onWarningEnded always mirror
        /// it, and neither can fire twice for the same transition.
        /// </summary>
        private void SetStage(IdleTimeoutStage next)
        {
            if (Stage == next)
            {
                return;
            }

            IdleTimeoutStage previous = Stage;
            Stage = next;

            // Edge detection, not state: the warning events fire on crossing the boundary, which
            // is what makes onWarningEnded fire exactly once whichever way the warning ends -
            // interrupted (back to IdleCountdown) or timed out (on to Inactive).
            bool enteringWarning = Stage == IdleTimeoutStage.WarningCountdown
                                && previous != IdleTimeoutStage.WarningCountdown;

            bool leavingWarning = previous == IdleTimeoutStage.WarningCountdown
                               && Stage != IdleTimeoutStage.WarningCountdown;

            if (enteringWarning)
            {
                onWarningStarted?.Invoke();
            }
            else if (leavingWarning)
            {
                onWarningEnded?.Invoke();
            }
        }

        /// <summary>Seconds remaining as 1 down to 0, which is what Image.fillAmount takes.</summary>
        private static float Normalize(float secondsRemaining, float duration)
        {
            return duration > 0f ? Mathf.Clamp01(secondsRemaining / duration) : 0f;
        }

        #endregion

        #region Helpers

        private void ValidateTimers()
        {
            if (idleTimer == null)
            {
                Debug.LogError($"[{nameof(IdleTimeoutManager)}] No idleTimer assigned. StartTimeout will do nothing.", this);
            }

            if (warningTimer == null)
            {
                Debug.LogError($"[{nameof(IdleTimeoutManager)}] No warningTimer assigned. The warning would appear and " +
                               $"never count down, leaving it up until someone touches the screen.", this);
            }

            if (idleTimer != null && idleTimer == warningTimer)
            {
                Debug.LogError($"[{nameof(IdleTimeoutManager)}] idleTimer and warningTimer are the same TimerManager. " +
                               $"Each stage needs its own or they fight over one countdown.", this);
            }
        }

        private static void PrepareTimer(TimerManager timer, float duration)
        {
            if (timer == null)
            {
                return;
            }

            timer.SetTimerDuration(duration);
            timer.ResetTimer();
        }

        private void Subscribe()
        {
            if (idleTimer != null)
            {
                idleTimer.onTimerDone.AddListener(OnIdleTimerDone);
                idleTimer.onTimerTick.AddListener(OnIdleTimerTick);
            }

            if (warningTimer != null)
            {
                warningTimer.onTimerDone.AddListener(OnWarningTimerDone);
                warningTimer.onTimerTick.AddListener(OnWarningTimerTick);
            }
        }

        private void Unsubscribe()
        {
            if (idleTimer != null)
            {
                idleTimer.onTimerDone.RemoveListener(OnIdleTimerDone);
                idleTimer.onTimerTick.RemoveListener(OnIdleTimerTick);
            }

            if (warningTimer != null)
            {
                warningTimer.onTimerDone.RemoveListener(OnWarningTimerDone);
                warningTimer.onTimerTick.RemoveListener(OnWarningTimerTick);
            }
        }

        private void Log(string message)
        {
            if (showLogs)
            {
                Debug.Log($"[{nameof(IdleTimeoutManager)}] {message}", this);
            }
        }

        #endregion
    }
}
