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
        private ScreenInputZoneController screenInputZoneController;

        [SerializeField]
        private TimerManager idleTimer;

        [SerializeField]
        private float idleCountdownDuration;

        public UnityEvent<float> onIdleTimerTick = new UnityEvent<float>();

        public UnityEvent<string> onIdleCountdownDone;

        void Start()
        {
            Reset();

            isComponentActive = true;
            screenInputZoneController.isComponentActive = true;

            idleTimer.onTimerDone.AddListener(OnIdleTimerDoneEmitter);
            idleTimer.SetComponentActive(true);
            idleTimer.SetTimerDuration(idleCountdownDuration);
            idleTimer.ResetTimer();

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
        public void EnableTimeout()
        {
            Debug.Log("[IdleTimeoutManager] Timeout enabled");
            idleTimer.ResetTimer();
            idleTimer.StartTimer();
        }

        public void DisableTimeout()
        {
            Debug.Log("[IdleTimeoutManager] Timeout disabled");
            idleTimer.ResetTimer();
        }
        public void Reset()
        {
            Debug.Log("[IdleTimeoutManager]: Resetting");
            idleTimer.ResetTimer();
        }

        public void OnBackgroundScreenClick(string str)
        {
            Debug.Log("[IdleTimeoutManager]: Background Screen Click");
            Reset();
            idleTimer.StartTimer();
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

        private void KeypressSimulation()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {

                EnableTimeout();

            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Reset();
            }

        }

        #endregion

     

    }

}