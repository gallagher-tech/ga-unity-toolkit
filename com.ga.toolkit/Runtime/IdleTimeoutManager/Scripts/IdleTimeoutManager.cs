using GAToolkit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace GAToolkit
{
    public class IdleTimeoutManager : MonoBehaviour
    {
        public bool isComponentActive { get; set; }

        public bool showLogs = false;

        [SerializeField]
        [FormerlySerializedAs("screenInputZoneController")]
        private ScreenInputZoneManager screenInputZoneManager;

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
            screenInputZoneManager.SetComponentActive(isActive);
            idleTimer.SetComponentActive(isActive);
            Reset();
        }

        public void StartTimeout(bool shouldActivateInputZones = true)
        {
            Reset();
            idleTimer.StartTimer();
            screenInputZoneManager.SetComponentActive(shouldActivateInputZones);

        }
        public void StopTimeout(bool shouldDeactivateInputZones = false)
        {
            idleTimer.ResetTimer();
            screenInputZoneManager.SetComponentActive(shouldDeactivateInputZones);
        }

        public void Reset()
        {
            idleTimer.ResetTimer();
        }

        public void SetTimerDuration(float duration)
        {
            if (!idleTimer.isComponentActive || idleTimer.isRunning)
            {
                return;
            }
            idleCountdownDuration = duration;
            idleTimer.SetTimerDuration(idleCountdownDuration);
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