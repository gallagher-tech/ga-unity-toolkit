using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace GAToolkit
{
    /// <summary>
    /// Two-stage inactivity timeout for kiosks.
    ///
    ///   Stage 1 - the idle countdown. Runs while the user is doing nothing. ANY input restarts
    ///             it, so it only ever completes if the screen has genuinely been left alone.
    ///   Stage 2 - the warning. When stage 1 completes, onIdleCountdownDone fires (show your
    ///             overlay) and a second, shorter countdown starts. onWarningTick reports 1 down
    ///             to 0 every frame, which is what a radial fill is driven from. Input during
    ///             this stage dismisses the overlay and sends the user back to stage 1.
    ///
    /// If stage 2 completes, onTimedOut fires and the manager stops. It does not decide what
    /// happens next - reloading the scene or returning to an attract loop is the consumer's call.
    ///
    /// Input comes from a ScreenInputZoneManager, which observes presses WITHOUT consuming them,
    /// so the countdown resets even while the user is pressing real buttons.
    ///
    /// Needs two separate TimerManagers, one per stage.
    /// </summary>
    [AddComponentMenu("GA Toolkit/Idle Timeout Manager")]
    public class IdleTimeoutManager : MonoBehaviour
    {
        #region Inspector

        [Header("Setup")]

        [SerializeField, Tooltip("Log each stage change.")]
        private bool showLogs = false;

        [SerializeField, FormerlySerializedAs("screenInputZoneController"),
         Tooltip("Turned on by StartTimeout and off by StopTimeout. This reference does NOT wire the reset - connect its On Input Anywhere to NotifyUserInput in the inspector.")]
        private ScreenInputZoneManager screenInputZoneManager;

        [Header("Stage 1 - Idle Countdown")]

        [SerializeField, Tooltip("Timer for the idle countdown. Must be a different TimerManager from the warning one.")]
        private TimerManager idleTimer;

        [SerializeField, Tooltip("Seconds of no input before the warning overlay appears.")]
        private float idleCountdownDuration = 60f;

        [Header("Stage 2 - Warning Overlay")]

        [SerializeField, Tooltip("Timer for the warning countdown. Must be a different TimerManager from the idle one.")]
        private TimerManager warningTimer;

        [SerializeField, Tooltip("Seconds the overlay stays up before the session times out.")]
        private float warningCountdownDuration = 10f;

        [Header("Events")]

        [Tooltip("Seconds remaining on the idle countdown, every frame it runs. For a number on screen.")]
        public UnityEvent<float> onIdleTick;

        [Tooltip("Idle countdown progress, 1 down to 0. Wire straight to an Image's fillAmount for a radial.")]
        public UnityEvent<float> onIdleProgress;

        [Tooltip("Stage 1 finished - show the warning overlay.")]
        public UnityEvent<string> onIdleCountdownDone;

        [Tooltip("Seconds remaining on the warning countdown, every frame it runs. For a number on screen.")]
        public UnityEvent<float> onWarningTick;

        [Tooltip("Warning progress, 1 down to 0. Wire straight to an Image's fillAmount for a radial.")]
        public UnityEvent<float> onWarningProgress;

        [Tooltip("The user touched the screen during the warning - hide the overlay. The idle countdown restarts.")]
        public UnityEvent<string> onWarningDismissed;

        [Tooltip("Stage 2 finished with no input. The manager stops here; what happens next is up to you.")]
        public UnityEvent<string> onTimedOut;

        #endregion

        #region State

        /// <summary>True while the countdowns are allowed to run.</summary>
        public bool isComponentActive { get; private set; }

        /// <summary>True between the overlay appearing and it being dismissed or timing out.</summary>
        public bool IsWarning { get; private set; }

        private bool subscribed;

        #endregion

        #region Life Cycle

        void Start()
        {
            if (idleTimer != null && idleTimer == warningTimer)
            {
                Debug.LogError($"[{nameof(IdleTimeoutManager)}] idleTimer and warningTimer are the same " +
                               $"TimerManager. Each stage needs its own or they fight over one countdown.", this);
            }

            // The timers tick themselves in their own Update, gated on isRunning. This component
            // must never call RunTimer as well, or every countdown runs at double speed.
            PrepareTimer(idleTimer, idleCountdownDuration);
            PrepareTimer(warningTimer, warningCountdownDuration);

            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the idle countdown from full and activates the manager - the only call a start
        /// button needs.
        /// </summary>
        public void StartTimeout()
        {
            isComponentActive = true;
            IsWarning = false;

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

        /// <summary>Stops both countdowns and leaves the manager inactive.</summary>
        public void StopTimeout(bool alsoStopWatchingInput = true)
        {
            isComponentActive = false;
            IsWarning = false;

            if (idleTimer != null)
            {
                idleTimer.ResetTimer();
            }

            if (warningTimer != null)
            {
                warningTimer.ResetTimer();
            }

            if (alsoStopWatchingInput && screenInputZoneManager != null)
            {
                screenInputZoneManager.SetComponentActive(false);
            }

            Log("Stopped.");
        }

        /// <summary>
        /// Sends the manager back to stage 1 from wherever it is. Does not hide the overlay on its
        /// own - NotifyUserInput does that, so dismissal and restart stay one action.
        /// </summary>
        public void RestartIdleCountdown()
        {
            if (idleTimer == null)
            {
                return;
            }

            idleTimer.SetTimerDuration(idleCountdownDuration);
            idleTimer.StartTimer();
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
        /// Any input at all, wherever it landed. During the warning this doubles as the dismiss,
        /// which is why the overlay closes on a tap anywhere rather than needing a button.
        ///
        /// Wire a ScreenInputZoneManager's On Input Anywhere straight to this in the inspector.
        /// Anything else that counts as activity - a keyboard, a scanner, a sensor - can call it too.
        /// </summary>
        public void NotifyUserInput(string source)
        {
            if (!isComponentActive)
            {
                return;
            }

            if (IsWarning)
            {
                IsWarning = false;

                if (warningTimer != null)
                {
                    warningTimer.ResetTimer();
                }

                onWarningDismissed?.Invoke(source);
                Log("Warning dismissed by input - back to the idle countdown.");
            }

            RestartIdleCountdown();
        }

        private void OnIdleTimerDone()
        {
            if (!isComponentActive || IsWarning)
            {
                return;
            }

            IsWarning = true;

            if (warningTimer != null)
            {
                warningTimer.SetTimerDuration(warningCountdownDuration);
                warningTimer.StartTimer();
            }

            onIdleCountdownDone?.Invoke(nameof(onIdleCountdownDone));
            Log("Idle countdown done - warning overlay up.");
        }

        private void OnWarningTimerDone()
        {
            if (!isComponentActive || !IsWarning)
            {
                return;
            }

            IsWarning = false;
            onTimedOut?.Invoke(nameof(onTimedOut));
            Log("Timed out.");

            // Last, deliberately: the manager stops and the consumer decides what happens next.
            StopTimeout();
        }

        private void OnIdleTimerTick(float secondsRemaining)
        {
            if (!isComponentActive || IsWarning)
            {
                return;
            }

            onIdleTick?.Invoke(Mathf.Max(0f, secondsRemaining));
            onIdleProgress?.Invoke(Normalize(secondsRemaining, idleCountdownDuration));
        }

        private void OnWarningTimerTick(float secondsRemaining)
        {
            if (!isComponentActive || !IsWarning)
            {
                return;
            }

            onWarningTick?.Invoke(Mathf.Max(0f, secondsRemaining));
            onWarningProgress?.Invoke(Normalize(secondsRemaining, warningCountdownDuration));
        }

        /// <summary>
        /// Seconds remaining as 1 down to 0, which is what Image.fillAmount takes - so a radial
        /// can be driven from the event with no code in between.
        /// </summary>
        private static float Normalize(float secondsRemaining, float duration)
        {
            return duration > 0f ? Mathf.Clamp01(secondsRemaining / duration) : 0f;
        }

        #endregion

        #region Helpers

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
            if (subscribed)
            {
                return;
            }

            subscribed = true;

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
            if (!subscribed)
            {
                return;
            }

            subscribed = false;

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
