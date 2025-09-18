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
        public void SetupRectTransformDimensions(float w, float h)
        {
            rt = this.gameObject.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
        }
        #endregion 
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

        public void CheckForUserInput()
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
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePresent ? (Vector2)Input.mousePosition : (Vector2)Input.GetTouch(0).position
                };
            
                PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
                eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

                List<RaycastResult> raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventDataCurrentPosition, raycastResults);

                bool hitRaycastTarget = false;


                foreach (var raycastResult in raycastResults)
                {

                    if (triggerObjToOnHitEvent.TryGetValue(raycastResult.gameObject, out var group))
                    {
                        group.onHit?.Invoke(default);
                        hitRaycastTarget = true;
                        break;
                    }
                }

                if (!hitRaycastTarget)
                {
                    onScreenHit?.Invoke(default);
                }

            }
        }

    }

}



