using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System;

namespace GAToolkit
{
    [Serializable]
    public class RaycastEventGroup
    {
        public List<GameObject> triggerObjs;          
        public UnityEvent<string> onHit;
    }

    public class ScreenInputZoneController : MonoBehaviour
    {
        public bool isComponentActive { get; set; }

        #region Rect Transform

        private RectTransform rt;

        #endregion

        #region Raycast Targets
        public List<RaycastEventGroup> raycastTargetGroups;

        private Dictionary<GameObject, RaycastEventGroup> triggerObjToOnHitEvent;

        #endregion

        #region General Screen Hit

        public UnityEvent<string> onScreenHit;

        #endregion 

        #region Life Cycle

        void Start()
        {
            SetupTriggerObjtoEventDictionary();
        }

        void Update()
        {
            CheckForUserInput();
        }

        #endregion

        #region Public API
        public void SetComponentActive(bool isActive)
        {
            isComponentActive = isActive;
            this.enabled = isComponentActive;
        }
        public void SetupRectTransformDimensions(float w, float h)
        {
            rt = this.gameObject.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
        }
        #endregion

        #region Helpers

        private void SetupTriggerObjtoEventDictionary()
        {
            triggerObjToOnHitEvent = new Dictionary<GameObject, RaycastEventGroup>();
            foreach (var group in raycastTargetGroups)
            {
                foreach (var gameObj in group.triggerObjs)
                {
                    if (gameObj != null && !triggerObjToOnHitEvent.ContainsKey(gameObj))
                        triggerObjToOnHitEvent.Add(gameObj, group);
                }
            }
        }

        private void CheckForUserInput()
        {

            if (!isComponentActive)
            {
                return;
            }

            if (
                Input.GetMouseButtonDown(0) ||
                Input.GetMouseButtonDown(1) ||
                Input.GetMouseButtonDown(2) ||
                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            
            )
            {
                
                Vector2 inputPosition = Input.mousePresent ? 
                    (Vector2)Input.mousePosition :
                    (Vector2)Input.GetTouch(0).position; 

                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = inputPosition
                };

                List<RaycastResult> raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, raycastResults);

                bool hasRegisteredTargets = triggerObjToOnHitEvent.Count > 0;

                foreach (var raycastResult in raycastResults)
                {

                    if (triggerObjToOnHitEvent.TryGetValue(raycastResult.gameObject, out var group))
                    {
                        group.onHit?.Invoke(default);
                        return;
                    }
                }

                if ( !hasRegisteredTargets || raycastResults.Count == 0)
                {
                    onScreenHit?.Invoke(default);
                }

            }
        }

        #endregion 
       
    }

}



