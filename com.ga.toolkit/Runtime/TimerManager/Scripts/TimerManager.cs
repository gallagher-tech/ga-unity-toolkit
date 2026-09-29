using UnityEngine;
using UnityEngine.Events;

namespace GAToolkit
{
    /// <summary>
    /// A countdown. Give it a duration, start it, and it reports the seconds remaining every
    /// frame until it reaches zero.
    ///
    /// One flag says whether it is counting: isRunning. There is deliberately no separate
    /// active/inactive switch - to stop the countdown use PauseTimer or ResetTimer, and to stop
    /// the component entirely use Unity's own enabled checkbox, which already does that and is
    /// visible in the inspector.
    ///
    /// StartTimer always begins from the full duration. ResumeTimer is what continues a paused
    /// countdown from where it left off.
    ///
    /// Counts scaled time by default, so Time.timeScale affects it. Turn on Use Unscaled Time
    /// for anything that should measure real seconds regardless of whether the app paused itself.
    /// </summary>
    [AddComponentMenu("GA Toolkit/Timer Manager")]
    [DisallowMultipleComponent]
    public class TimerManager : MonoBehaviour
    {
        #region Inspector

        [SerializeField, Tooltip("How long the countdown runs for, in seconds.")]
        private float countdownDuration = 10f;

        [SerializeField, Tooltip("Off: the countdown obeys Time.timeScale, so setting timeScale to 0 freezes it and 0.5 halves its speed. On: it counts real seconds regardless. Turn this on for anything measuring how long a person has been standing there, which should not care whether the app paused itself.")]
        private bool useUnscaledTime = false;

        [Tooltip("Seconds remaining, every frame the countdown runs. Never negative, and always reaches exactly 0 before onTimerDone.")]
        public UnityEvent<float> onTimerTick;

        [Tooltip("Fired once, when the countdown reaches zero.")]
        public UnityEvent onTimerDone;

        #endregion

        #region State

        /// <summary>True while the countdown is ticking.</summary>
        public bool isRunning { get; private set; }

        /// <summary>Seconds left on the clock.</summary>
        public float TimeRemaining => Mathf.Max(0f, time);

        /// <summary>How long a full countdown lasts.</summary>
        public float Duration => countdownDuration;

        /// <summary>1 at the start, 0 at the end. Wire straight to an Image's fillAmount.</summary>
        public float Progress => countdownDuration > 0f ? Mathf.Clamp01(time / countdownDuration) : 0f;

        private float time;

        #endregion

        #region Life Cycle

        void Awake()
        {
            // Armed on load so the clock is never sitting on a stale or zero value before the
            // first StartTimer. Without this, ResumeTimer on a fresh timer would finish instantly.
            time = countdownDuration;
        }

        void Update()
        {
            RunTimer();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets how long a full countdown lasts. Does NOT move the clock: a running timer
        /// finishes its current countdown and a paused one keeps its position, so pausing is
        /// never destroyed by a settings change. The new length applies at the next StartTimer
        /// or ResetTimer.
        /// </summary>
        public void SetTimerDuration(float seconds)
        {
            countdownDuration = Mathf.Max(0f, seconds);
        }

        /// <summary>
        /// Starts a fresh countdown from the full duration. Use ResumeTimer to continue a paused
        /// one - this always rewinds first.
        /// </summary>
        public void StartTimer()
        {
            time = countdownDuration;
            isRunning = true;
        }

        /// <summary>Stops ticking but keeps the position, so ResumeTimer picks up where it left off.</summary>
        public void PauseTimer()
        {
            isRunning = false;
        }

        /// <summary>
        /// Continues a paused countdown from where it stopped. Does nothing on a finished timer -
        /// without that guard, resuming one with no time left would run it straight past zero and
        /// fire onTimerDone a second time. Use StartTimer to run it again from the top.
        /// </summary>
        public void ResumeTimer()
        {
            if (time <= 0f)
            {
                return;
            }

            isRunning = true;
        }

        /// <summary>Stops and rewinds to the full duration, without starting.</summary>
        public void ResetTimer()
        {
            isRunning = false;
            time = countdownDuration;
        }

        #endregion

        /// <summary>
        /// Private on purpose. Update is the only thing that drives the clock; this used to be
        /// public AND called from Update, so anything that pumped it as well counted down at
        /// double speed with no indication.
        /// </summary>
        private void RunTimer()
        {
            if (!isRunning)
            {
                return;
            }

            time -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (time <= 0f)
            {
                // Land exactly on zero and stop before announcing it, so listeners never see a
                // negative value and never get a tick after done.
                time = 0f;
                isRunning = false;
                onTimerTick?.Invoke(0f);
                onTimerDone?.Invoke();
                return;
            }

            onTimerTick?.Invoke(time);
        }
    }
}
