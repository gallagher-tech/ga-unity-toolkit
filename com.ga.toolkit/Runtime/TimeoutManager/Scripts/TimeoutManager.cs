using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using GAToolkit;

namespace GAToolkit
{
    public class TimeoutManager : MonoBehaviour
    {
        public bool isComponentActive { get; set; }

        [SerializeField]
        private ScreenInputZoneController screenInputZoneController;

        #region Idle Countdown

        [SerializeField]
        private TimerManager idleTimer;

        [SerializeField]
        private float idleCountdownDuration;

        #endregion

        #region Prompt Countdown

        [SerializeField]
        private TimerManager promptTimer;

        [SerializeField]
        private float promptCountdownDuration;

        public UnityEvent<string> onPromptCountdownDone;

        public UnityEvent<float> OnPromptTimerTick = new UnityEvent<float>();

        #endregion

        void Start()
        {
            Reset();

            isComponentActive = true;
            screenInputZoneController.isComponentActive = true;

            idleTimer.SetComponentActive(true);
            idleTimer.SetTimerDuration(idleCountdownDuration);
            idleTimer.ResetTimer();
            idleTimer.onTimerDone.AddListener(OnIdleTimerDone);
           
            promptTimer.SetComponentActive(true);
            promptTimer.SetTimerDuration(promptCountdownDuration);
            promptTimer.ResetTimer();

            promptTimer.onTimerTick.AddListener(OnPromptTimerTickEmitter);
        }

        void Update()
        {
            KeypressSimulation();

            if (!isComponentActive)
            {
                return;
            }

            idleTimer.RunTimer();
            promptTimer.RunTimer();
        }

        public void EnableTimeout()
        {
            Debug.Log("[TimeoutManager] Timeout enabled");
            idleTimer.ResetTimer();
            idleTimer.StartTimer();
        }

        public void DisableTimeout()
        {
            Debug.Log("[TimeoutManager] Timeout disabled");
            idleTimer.ResetTimer();
        }

        private void OnIdleTimerDone(string str) {

            Debug.Log("Idle Timer done");

            if (promptTimer.isComponentActive)
            {
                promptTimer.StartTimer();
            }
        }

        private void OnPromptTimerTickEmitter(float runtimeValue)
        {
            OnPromptTimerTick.Invoke(runtimeValue);
        }

        public void OnPromptTimerDone(string str)
        {
            Debug.Log("Prompt Timer done");
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

        public void ButtonOnClick()
        {
            Debug.Log("Button Click");
        }

        public void OnBackgroundScreenClick(string str)
        {
            Debug.Log("TimeoutManager: Background Screen Click");
            Reset();
            idleTimer.StartTimer();
        }
        public void Reset()
        {
            Debug.Log("TimeoutManager: Resetting");
            idleTimer.ResetTimer();
            promptTimer.ResetTimer();
        }
     
    }

}


