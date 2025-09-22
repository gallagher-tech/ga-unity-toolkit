using GAToolkit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GAToolkit
{
    public class IdleTimeoutManager : MonoBehaviour
    {
        public bool isComponentActive { get; set; }

        [SerializeField]
        private bool shouldRestartIdleTimerOnScreenHit = false;

        [SerializeField]
        private ScreenInputZoneController screenInputZoneController;

        [SerializeField]
        private TimerManager idleTimer;

        [SerializeField]
        private float idleCountdownDuration;

        public UnityEvent<float> onIdleTimerTick = new UnityEvent<float>();

        public UnityEvent<string> onIdleCountdownDone;

        void Start()
        {

            isComponentActive = true;
            screenInputZoneController.isComponentActive = true;

            idleTimer.onTimerDone.AddListener(OnIdleTimerDoneEmitter);
            idleTimer.SetComponentActive(true);
            idleTimer.SetTimerDuration(idleCountdownDuration);
            Reset();

        }

        void Update()
        {

            if (!isComponentActive)
            {
                return;
            }

            idleTimer.RunTimer();
        }


        #region Public API
        public void StartTimeout()
        {
            Debug.Log("[IdleTimeoutManager] Timeout started");
            Reset();
            idleTimer.StartTimer();
        }
        public void StopTimeout()
        {
            Debug.Log("[IdleTimeoutManager] Timeout stopped");
            idleTimer.ResetTimer();
        }
        public void Reset()
        {
            Debug.Log("[IdleTimeoutManager]: Resetting");
            idleTimer.ResetTimer();
        }

        #endregion

        #region Helpers
        private void OnIdleTimerDoneEmitter(string str)
        {
            Debug.Log("Idle Timer done");
            onIdleCountdownDone?.Invoke(str);
        }

        private void OnIdleTimerTickEmitter(float time)
        {
            onIdleTimerTick.Invoke(time);
        }

        #endregion

     

    }

}