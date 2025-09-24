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

        public bool showLogs = false;

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
            idleTimer.onTimerDone.AddListener(OnIdleTimerDoneEmitter);
            idleTimer.SetTimerDuration(idleCountdownDuration);
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

        public void SetComponentActive(bool isActive)
        {
            isComponentActive = isActive;
            screenInputZoneController.SetComponentActive(isActive);
            idleTimer.SetComponentActive(isActive);
            Reset();
        }

        public void StartTimeout(bool shouldActivateScreenInputZoneController = true)
        {
            Reset();
            idleTimer.StartTimer();
            screenInputZoneController.SetComponentActive(shouldActivateScreenInputZoneController);

        }
        public void StopTimeout(bool shouldDeactivateScreenInputZoneController = false)
        {
            idleTimer.ResetTimer();
            screenInputZoneController.SetComponentActive(shouldDeactivateScreenInputZoneController);
        }

        public void Reset()
        {
            idleTimer.ResetTimer();
        }

        #endregion

        #region Helpers
        private void OnIdleTimerDoneEmitter(string str)
        {
            onIdleCountdownDone?.Invoke(str);
        }

        private void OnIdleTimerTickEmitter(float time)
        {
            onIdleTimerTick.Invoke(time);
        }

        #endregion

     

    }

}