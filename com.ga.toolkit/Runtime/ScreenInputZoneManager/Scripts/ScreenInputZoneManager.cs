using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GAToolkit
{
    /// <summary>
    /// The kinds of interaction a zone can respond to. Every interaction ends up as exactly
    /// one of these, and each fires at the moment it becomes knowable.
    /// </summary>
    [Flags]
    public enum ScreenInputKind
    {
        None = 0,

        /// <summary>
        /// Pressed and released without moving past the drag threshold. Fires on release,
        /// because until then it could still become a drag - and because releasing is what
        /// every button on every platform waits for, so sliding off to cancel still works.
        /// </summary>
        Tap = 1 << 0,

        /// <summary>
        /// Moved past the drag threshold. Fires the instant the threshold is crossed rather
        /// than on release, so a zone reacts when a long drag starts, not a minute later when
        /// the finger finally lifts.
        /// </summary>
        Drag = 1 << 1,
    }

    /// <summary>
    /// A group of screen objects treated as one region, plus what should happen when the
    /// user interacts with it - or with everything except it.
    ///
    /// A zone has no name of its own; it is identified in the inspector by the objects in it.
    /// </summary>
    [Serializable]
    public class ScreenInputZone
    {
        [Tooltip("The objects that make up this zone. An interaction on any of them - or on any of their children - counts as inside. Each needs a Graphic with Raycast Target enabled.")]
        public List<GameObject> objects = new List<GameObject>();

        [Tooltip("Off: the event fires when the user interacts with this zone. On: it fires when the user interacts with anything except this zone (the negative space around it).")]
        public bool detectOutside = false;

        [Tooltip("Which kinds of interaction this zone reacts to. Tap fires on release; Drag fires the moment the press moves past the drag threshold. One interaction is only ever one of the two.")]
        public ScreenInputKind respondTo = ScreenInputKind.Tap | ScreenInputKind.Drag;

        [Tooltip("Fired when an interaction begins inside this zone. The payload is the name of the zone object that was hit.")]
        public UnityEvent<string> onInputInside;

        [Tooltip("Fired when an interaction begins outside this zone. The payload is the name of the object that was hit, or empty if the interaction hit nothing.")]
        public UnityEvent<string> onInputOutside;

        /// <summary>Fast lookup rebuilt from <see cref="objects"/>. See ScreenInputZoneManager.RebuildZones.</summary>
        [NonSerialized] internal readonly HashSet<GameObject> lookup = new HashSet<GameObject>();
    }

    /// <summary>
    /// Watches for pointer and touch interaction anywhere on screen and reports it two ways:
    /// that input happened at all, and which zone it landed in (or outside of).
    ///
    /// Two independent signals, so neither has to be inferred from the other:
    ///   onInputAnywhere - fires the moment any interaction begins, whatever it landed on. Use this
    ///                to reset an idle timer or break out of an attract loop.
    ///   zones      - each zone independently fires when an interaction begins inside it, or,
    ///                with Detect Outside on, when one begins anywhere except it.
    ///
    /// This component OBSERVES input and never consumes it. A tap that dismisses a popup through
    /// a zone's Detect Outside event still reaches the button underneath, which is what lets one
    /// tap both close a popup and press the thing behind it. If you need a tap to be swallowed,
    /// put a full-screen Raycast Target image behind the popup instead - that is uGUI's job.
    ///
    /// An interaction is inside or outside based on WHERE IT BEGAN, matching how uGUI routes
    /// drags. Press inside a popup, drag out, release - that is still inside, so a slider or a
    /// scrolling panel keeps working.
    ///
    /// Timing: onInputAnywhere fires immediately on press, always first. A zone's Drag fires the
    /// instant the press moves past the drag threshold, so a zone reacts when a long drag starts
    /// rather than when it ends. A zone's Tap fires on release, because until the press lifts it
    /// could still turn into a drag. One interaction is only ever one of the two.
    ///
    /// Needs an EventSystem in the scene, and zone objects must be Raycast Targets under a Canvas
    /// with a GraphicRaycaster. The inspector warns about both.
    /// </summary>
    [AddComponentMenu("GA Toolkit/Screen Input Zone Manager")]
    public class ScreenInputZoneManager : MonoBehaviour
    {
        #region Inspector

        [Header("Setup")]

        [SerializeField, Tooltip("Start watching for input as soon as the scene loads. Turn this off if another component owns activation.")]
        private bool activeOnStart = true;

        [SerializeField, Tooltip("Log every interaction and how it resolved.")]
        private bool showLogs = false;

        [Header("Input")]

        [SerializeField, Tooltip("Count right and middle mouse buttons as a press, not just left click and touch.")]
        private bool respondToSecondaryButtons = true;

        [Header("Global (fires first, on press)")]

        [Tooltip("Fires the moment any interaction begins, whatever it landed on, and always before any zone event. The payload is the name of the object that was hit, or empty if it hit nothing.")]
        public UnityEvent<string> onInputAnywhere;

        [Header("Zones (fire after On Input Anywhere)")]

        [SerializeField, Tooltip("Drag fires the moment the press passes the drag threshold; Tap fires on release. Either way it is after On Input Anywhere. Each zone is checked independently against where the press began, so one interaction can fire several of them.")]
        private List<ScreenInputZone> zones = new List<ScreenInputZone>();

        #endregion

        #region State

        /// <summary>True while this component is watching for input.</summary>
        public bool isComponentActive { get; private set; }

        /// <summary>True from the moment a press begins until it is released.</summary>
        public bool IsInteracting { get; private set; }

        /// <summary>
        /// Seconds since the last interaction began or ended. Stays at zero for the whole of a
        /// long drag, so an idle timeout can hold off while the user is still busy.
        /// </summary>
        public float TimeSinceLastInput => IsInteracting ? 0f : Time.unscaledTime - lastInputTime;

        /// <summary>The configured zones. After editing a zone's objects, call <see cref="RebuildZones"/>.</summary>
        public IReadOnlyList<ScreenInputZone> Zones => zones;

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        // Anchored at press-down and used again on release, so an interaction is judged by
        // where it began rather than where it ended.
        private readonly List<bool> anchorInside = new List<bool>();
        private readonly List<string> anchorPayload = new List<string>();
        private string anchorTopmostName = string.Empty;

        private bool wasPressed;
        private Vector2 startPosition;
        private float startTime;
        private float maxTravel;
        private float lastInputTime;

        // Set the moment the press passes the drag threshold. Once an interaction is a drag it
        // can never also be a tap, so this is what stops both firing for one press.
        private bool dragRecognized;

        private bool hasWarnedAboutEventSystem;

        #endregion

        #region Life Cycle

        void Awake()
        {
            RebuildZones();
            lastInputTime = Time.unscaledTime;
            SetComponentActive(activeOnStart);
        }

        void Update()
        {
            if (!isComponentActive)
            {
                return;
            }

            if (!ReadPointer(out Vector2 position, out bool isPressed))
            {
                return;
            }

            if (isPressed && !wasPressed)
            {
                BeginInteraction(position);
            }
            else if (isPressed)
            {
                TrackTravel(position);
            }
            else if (wasPressed)
            {
                EndInteraction(position);
            }

            wasPressed = isPressed;
        }

        #endregion

        #region Public API

        /// <summary>Starts or stops watching for input. No events fire while inactive.</summary>
        public void SetComponentActive(bool isActive)
        {
            isComponentActive = isActive;

            if (!isActive)
            {
                IsInteracting = false;
                wasPressed = false;
            }
        }

        /// <summary>
        /// Re-reads every zone's object list into its lookup. Called on Awake; call it again
        /// after changing a zone's objects at runtime.
        /// </summary>
        public void RebuildZones()
        {
            anchorInside.Clear();
            anchorPayload.Clear();

            foreach (ScreenInputZone zone in zones)
            {
                anchorInside.Add(false);
                anchorPayload.Add(string.Empty);

                if (zone == null)
                {
                    continue;
                }

                zone.lookup.Clear();

                if (zone.objects == null)
                {
                    continue;
                }

                foreach (GameObject obj in zone.objects)
                {
                    if (obj != null)
                    {
                        zone.lookup.Add(obj);
                    }
                }
            }
        }

        #endregion

        #region Interaction

        private void BeginInteraction(Vector2 position)
        {
            // Keeps the anchor arrays in step if zones were added or removed without a rebuild.
            if (anchorInside.Count != zones.Count)
            {
                RebuildZones();
            }

            IsInteracting = true;
            startPosition = position;
            startTime = Time.unscaledTime;
            maxTravel = 0f;
            dragRecognized = false;
            lastInputTime = startTime;

            Raycast(position);
            anchorTopmostName = raycastResults.Count > 0 ? raycastResults[0].gameObject.name : string.Empty;

            for (int i = 0; i < zones.Count; i++)
            {
                GameObject match = zones[i] != null ? FindMatch(zones[i]) : null;
                anchorInside[i] = match != null;
                anchorPayload[i] = match != null ? match.name : anchorTopmostName;
            }

            onInputAnywhere?.Invoke(anchorTopmostName);

            if (showLogs)
            {
                Debug.Log($"[{nameof(ScreenInputZoneManager)}] Interaction began at {position} on " +
                          $"{(string.IsNullOrEmpty(anchorTopmostName) ? "nothing" : anchorTopmostName)}.", this);
            }
        }

        /// <summary>
        /// Watches a held press for the moment it becomes a drag. Firing here rather than on
        /// release is what lets a zone react when a long drag starts instead of a minute later.
        /// </summary>
        private void TrackTravel(Vector2 position)
        {
            maxTravel = Mathf.Max(maxTravel, Vector2.Distance(startPosition, position));

            if (!dragRecognized && maxTravel > DragThreshold)
            {
                dragRecognized = true;
                FireZones(ScreenInputKind.Drag);
            }
        }

        private void EndInteraction(Vector2 position)
        {
            IsInteracting = false;
            lastInputTime = Time.unscaledTime;

            maxTravel = Mathf.Max(maxTravel, Vector2.Distance(startPosition, position));

            // A press that crossed the threshold on its very last frame never got a held frame to
            // be recognised in, so catch it here rather than mislabelling it a tap.
            if (!dragRecognized && maxTravel > DragThreshold)
            {
                dragRecognized = true;
                FireZones(ScreenInputKind.Drag);
            }
            else if (!dragRecognized)
            {
                FireZones(ScreenInputKind.Tap);
            }

            if (showLogs)
            {
                Debug.Log($"[{nameof(ScreenInputZoneManager)}] Interaction ended as " +
                          $"{(dragRecognized ? "Drag" : "Tap")} after {maxTravel:0}px in " +
                          $"{Time.unscaledTime - startTime:0.00}s.", this);
            }
        }

        /// <summary>
        /// Fires every zone that responds to this kind, using the inside/outside answer anchored
        /// at press-down rather than wherever the pointer is now.
        /// </summary>
        private void FireZones(ScreenInputKind kind)
        {
            int count = Mathf.Min(zones.Count, anchorInside.Count);

            for (int i = 0; i < count; i++)
            {
                ScreenInputZone zone = zones[i];
                if (zone == null || (zone.respondTo & kind) == 0)
                {
                    continue;
                }

                // A zone with no objects can never be hit, which would make Detect Outside fire on
                // everything - use onInputAnywhere for that instead. The inspector warns about this.
                if (zone.lookup.Count == 0)
                {
                    continue;
                }

                if (zone.detectOutside)
                {
                    if (!anchorInside[i])
                    {
                        zone.onInputOutside?.Invoke(anchorPayload[i]);
                    }
                }
                else if (anchorInside[i])
                {
                    zone.onInputInside?.Invoke(anchorPayload[i]);
                }
            }
        }

        private static float DragThreshold =>
            EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10f;

        private void Raycast(Vector2 position)
        {
            raycastResults.Clear();

            if (EventSystem.current == null)
            {
                if (!hasWarnedAboutEventSystem)
                {
                    hasWarnedAboutEventSystem = true;
                    Debug.LogWarning($"[{nameof(ScreenInputZoneManager)}] No EventSystem in the scene, so zones can " +
                                     $"never be hit. Add one via GameObject > UI > Event System.", this);
                }

                return;
            }

            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = position };
            EventSystem.current.RaycastAll(pointerData, raycastResults);
        }

        /// <summary>
        /// Returns the zone object the interaction landed on, or null if it landed outside the zone.
        /// Walks up from each hit so an interaction on a child counts as one on its listed parent.
        /// </summary>
        private GameObject FindMatch(ScreenInputZone zone)
        {
            if (zone.lookup.Count == 0)
            {
                return null;
            }

            // RaycastAll returns results sorted front to back.
            foreach (RaycastResult result in raycastResults)
            {
                Transform current = result.gameObject.transform;
                while (current != null)
                {
                    if (zone.lookup.Contains(current.gameObject))
                    {
                        return current.gameObject;
                    }

                    current = current.parent;
                }
            }

            return null;
        }

        #endregion

        #region Input Backend

        /// <summary>
        /// Reads the current pointer position and whether it is pressed, against whichever input
        /// backend the project has active (Project Settings > Player > Active Input Handling).
        /// Supports Old, New, or Both.
        /// </summary>
        private bool ReadPointer(out Vector2 position, out bool isPressed)
        {
#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                isPressed = true;
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                isPressed = mouse.leftButton.isPressed ||
                            (respondToSecondaryButtons && (mouse.rightButton.isPressed || mouse.middleButton.isPressed));
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                UnityEngine.Touch touch = Input.GetTouch(0);
                position = touch.position;

                // Qualified because UnityEngine.InputSystem also defines TouchPhase, and both
                // namespaces are in scope when Active Input Handling is set to Both.
                isPressed = touch.phase != UnityEngine.TouchPhase.Ended &&
                            touch.phase != UnityEngine.TouchPhase.Canceled;
                return true;
            }

            position = Input.mousePosition;
            isPressed = Input.GetMouseButton(0) ||
                        (respondToSecondaryButtons && (Input.GetMouseButton(1) || Input.GetMouseButton(2)));
            return true;
#else
            position = default;
            isPressed = false;
            return false;
#endif
        }

        #endregion
    }
}
