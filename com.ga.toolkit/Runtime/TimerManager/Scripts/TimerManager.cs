using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using GAToolkit;
using System.ComponentModel;
using Unity.Collections;

namespace GAToolkit
{

    public class TimerManager : MonoBehaviour
    {

        public bool isComponentActive { get; set; }

        private bool isRunning = false;

        [SerializeField]
        private float countdownDuration;

        [SerializeField]
        private float time;

        public UnityEvent<float> onTimerTick;

        public UnityEvent<string> onTimerDone;

        #region Life Cycle

        void Start()
        {

        }

        void Update()
        {
            if (!isComponentActive) {
                return;
            }

            RunTimer();
        }

        #endregion
        
        public void SetComponentActive(bool isActive)
        {
            isComponentActive = isActive;
        }
        public void SetTimerDuration(float duration)
        {
            countdownDuration = duration;
        }

        public void StartTimer()
        {
            isRunning = true;
        }
        public void RunTimer()
        {
            if (!isRunning) {
                return;
            }

            time -= Time.deltaTime;
            onTimerTick?.Invoke(time);

            if (time < 0)
            {
                //Debug.Log("TimerManager]: Timer done!");
                onTimerDone?.Invoke(default);
                isRunning = false;
            }

        }
        
        public void ResetTimer()
        {
            isRunning = false;
            time = countdownDuration;
        }
    }

}


