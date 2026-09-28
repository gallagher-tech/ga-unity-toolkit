using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Drop-in client preview for kiosk apps. When enabled, a build opens as a window at the app's
/// aspect ratio instead of fullscreen, stays at that ratio when resized, and can be nudged a few
/// pixels at a time with - / = (Shift for bigger steps) - so it can be measured against a real
/// screen, e.g. sized on an 86" display to match a 43" one.
///
/// The window always fits inside the part of the display the taskbar leaves free,
/// and can have its title bar hidden (and toggled back with a key). The window is locked in place
/// while the title bar is hidden, and can only be moved (by the title bar) and resized (from its
/// corners, or with - / =) while it is showing.
///
/// Windows builds only: it talks to the Windows window directly (falling back to Unity's own
/// Screen API, without title bar hiding or taskbar awareness, if that fails). Does nothing on other
/// platforms or in the Editor. Needs Player Settings > Resolution and Presentation > Resizable
/// Window on.
/// Self-contained: put it on its own GameObject (or prefab) in the first scene.
/// </summary>
public class WindowsPreviewResizingManager : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("previewMode"), Tooltip("On: builds open as a resizable window at the aspect ratio below. Off for the kiosk build.")]
    private bool enablePreviewResizeMode = false;

    [SerializeField, Tooltip("The app's design resolution, used only for its aspect ratio. e.g. 2160 x 3840 for 4K portrait.")]
    private Vector2Int designResolution = new Vector2Int(2160, 3840);

    [SerializeField, Range(0.2f, 1f), Tooltip("How much of the usable display height (the part the taskbar leaves free) the window opens at.")]
    private float heightFill = 0.9f;

    [SerializeField, Tooltip("Open with the window's title bar hidden. While hidden the window cannot be moved or resized.")]
    private bool hideTitleBar = true;

    [SerializeField, Tooltip("Shows / hides the title bar. It is added above the content, so the content keeps its size and place. None to disable.")]
    private Key toggleTitleBarKey = Key.F11;

    [SerializeField, Tooltip("Pixels per - / = press. Only while the title bar is showing.")]
    private int nudgeStep = 4;

    [SerializeField, Tooltip("Pixels per - / = press while Shift is held.")]
    private int nudgeStepLarge = 40;

    [SerializeField, Tooltip("Keep the window alive across scene loads. Turn off if every scene has its own copy.")]
    private bool persistAcrossScenes = true;

    private static WindowsPreviewResizingManager instance;

    /// <summary>The OS window being managed, or null until setup has finished.</summary>
    private PlatformWindow window;

    private float Aspect => (float)designResolution.x / designResolution.y;

    #region Life Cycle

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        if (!enablePreviewResizeMode || Application.platform != RuntimePlatform.WindowsPlayer)
        {
            enabled = false;
            return;
        }

        StartCoroutine(SetUp());
    }

    void Update()
    {
        if (window == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Resizing, like moving, only while the title bar is showing.
            if (!hideTitleBar)
            {
                int step = keyboard.shiftKey.isPressed ? nudgeStepLarge : nudgeStep;
                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                    window.Nudge(step);
                else if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                    window.Nudge(-step);
            }

            if (toggleTitleBarKey != Key.None && keyboard[toggleTitleBarKey].wasPressedThisFrame)
            {
                hideTitleBar = !hideTitleBar;
                window.SetTitleBarHidden(hideTitleBar);
            }
        }

        window.Tick();
    }

    void OnDestroy()
    {
        window?.Detach();
        if (instance == this)
            instance = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Changes the aspect the window is held at. Takes effect immediately on a live window;
    /// in the Editor or off Windows it only updates the stored value.
    /// </summary>
    public void SetDesignResolution(Vector2Int resolution)
    {
        if (resolution.x <= 0 || resolution.y <= 0)
        {
            Debug.LogWarning("[WindowsPreviewResizingManager] Design resolution must be positive on both axes.");
            return;
        }

        designResolution = resolution;

        if (window == null)
            return;

        window.Aspect = Aspect;
        window.Fit(heightFill);
    }

    /// <summary>Swaps width and height, flipping the window between portrait and landscape.</summary>
    public void ToggleOrientation() =>
        SetDesignResolution(new Vector2Int(designResolution.y, designResolution.x));

    /// <summary>The design resolution the window is currently held at.</summary>
    public Vector2Int DesignResolution => designResolution;

    /// <summary>True once the component has taken over an OS window. False in the Editor.</summary>
    public bool IsManagingWindow => window != null;

    #endregion

    #region Setup

    private IEnumerator SetUp()
    {
        // Out of fullscreen first, with Unity's own call - skipped when Unity has already opened
        // windowed (it remembers the last session's window), since every change here is a jump
        // the viewer sees. Unity applies it at the end of the frame.
        if (Screen.fullScreenMode != FullScreenMode.Windowed)
        {
            int height = Mathf.RoundToInt(Screen.currentResolution.height * heightFill);
            Screen.SetResolution(Mathf.RoundToInt(height * Aspect), height, FullScreenMode.Windowed);
            yield return null;
            yield return null;
        }


        // Windows only - Awake has already switched this off anywhere else.
        PlatformWindow native = new WindowsWindow();

        if (native != null)
        {
            native.Aspect = Aspect;
            try
            {
                if (!native.Attach(hideTitleBar))
                    native = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WindowsPreviewResizingManager] Could not reach the OS window, falling back to Unity's Screen API: " + e.Message);
                native = null;
            }
        }

        PlatformWindow chosen = native ?? new ScreenWindow { Aspect = Aspect };
        chosen.Fit(heightFill);
        window = chosen;
    }

    #endregion

    #region Platform Windows

    /// <summary>One way of sizing and moving the app's window.</summary>
    private abstract class PlatformWindow
    {
        /// <summary>Width / height the content is kept at.</summary>
        public float Aspect;

        /// <summary>Finds the window and applies its style. False if it could not be found.</summary>
        public virtual bool Attach(bool hideTitleBar) => true;

        /// <summary>Undoes anything Attach hooked into the OS window.</summary>
        public virtual void Detach() { }

        /// <summary>Sizes the window to a fraction of the usable display height and centres it there.</summary>
        public abstract void Fit(float heightFill);

        /// <summary>Grows (or with a negative step, shrinks) the window's content height.</summary>
        public abstract void Nudge(int step);

        /// <summary>Called every frame: keeps the aspect after a drag.</summary>
        public virtual void Tick() { }

        /// <summary>
        /// Shows or hides the title bar, growing or shrinking the window from the top so the
        /// content stays the same size in the same place (moved back inside the usable area if
        /// there is no room above it).
        /// </summary>
        public virtual void SetTitleBarHidden(bool hidden) { }

        /// <summary>
        /// The content height to snap to after a drag. A side edge drives from the width, a top or
        /// bottom edge from the height, and a corner (both changed) from the long side of the
        /// design - height for portrait, width for landscape - with the other side following.
        /// </summary>
        protected int SnappedHeight(int width, int height, int previousWidth, int previousHeight)
        {
            bool widthChanged = width != previousWidth;
            bool heightChanged = height != previousHeight;
            bool fromWidth = widthChanged && heightChanged ? Aspect > 1f : widthChanged;
            return fromWidth ? Mathf.RoundToInt(width / Aspect) : height;
        }
    }

    /// <summary>Fallback for platforms without a native path: Unity's Screen API only.</summary>
    private class ScreenWindow : PlatformWindow
    {
        private int width;
        private int height;

        public override void Fit(float heightFill) =>
            SetHeight(Mathf.RoundToInt(Screen.currentResolution.height * heightFill));

        public override void Nudge(int step) => SetHeight(height + step);

        public override void Tick()
        {
            // Leave a drag alone until the mouse is let go, then snap once.
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                return;
            if (Screen.width == width && Screen.height == height)
                return;

            SetHeight(SnappedHeight(Screen.width, Screen.height, width, height));
        }

        private void SetHeight(int newHeight)
        {
            height = Mathf.Clamp(newHeight, 200, Screen.currentResolution.height);
            width = Mathf.RoundToInt(height * Aspect);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }
    }

    /// <summary>
    /// Windows builds, through user32. Takes the resize border away along with the title bar and
    /// fits inside the monitor's work area (the part the taskbar leaves free).
    ///
    /// Resizing is corners only and holds the aspect during the drag: the window's messages are
    /// routed through WindowProc here first, which turns the edges into plain border (WM_NCHITTEST)
    /// and reshapes every step of a corner drag before Windows draws it (WM_SIZING) - height drives
    /// for a portrait design, width for landscape, and the opposite corner stays put. Tick's snap
    /// is only a safety net for anything that resizes the window some other way.
    /// </summary>
    private class WindowsWindow : PlatformWindow
    {
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int SM_SWAPBUTTON = 23;
        private const uint WS_MAXIMIZEBOX = 0x00010000;
        private const int GWLP_WNDPROC = -4;
        private const uint WM_NCHITTEST = 0x0084;
        private const uint WM_SIZING = 0x0214;
        private const uint WM_MOVE = 0x0003;
        private const uint WM_SIZE = 0x0005;
        private const uint WM_WINDOWPOSCHANGED = 0x0047;
        private const uint WM_STYLECHANGING = 0x007C;
        private const uint WM_STYLECHANGED = 0x007D;
        private const int SIZE_RESTORED = 0;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17, HTBORDER = 18;
        private const int WMSZ_LEFT = 1, WMSZ_TOP = 3, WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5, WMSZ_BOTTOMLEFT = 7;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")] private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref RECT rect, uint style, bool menu, uint exStyle);
        [DllImport("user32.dll")] private static extern bool AdjustWindowRectExForDpi(ref RECT rect, uint style, bool menu, uint exStyle, uint dpi);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")] private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Static so Windows' pointer to WindowProc never outlives it, and so WindowProc (which has
        // to be static for IL2CPP) can find its way back to the window being managed.
        private static readonly WndProcDelegate windowProc = WindowProc;
        private static WindowsWindow hooked;
        private static IntPtr previousWindowProc;

        /// <summary>
        /// True while a style change is under way: Unity is kept from seeing the window's size
        /// messages until the change is complete (see Place).
        /// </summary>
        private static bool holdSizeMessages;

        private IntPtr hwnd;
        private int clientWidth;
        private int clientHeight;
        private bool titleBarHidden;
        private int unexpectedResizeLogs;

        public override bool Attach(bool hideTitleBar)
        {
            hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero)
                hwnd = FindWindow("UnityWndClass", null);
            if (hwnd == IntPtr.Zero)
                return false;

            // Applied by Fit, together with the size, in one go.
            titleBarHidden = hideTitleBar;

            // 64-bit builds only: SetWindowLongPtrW does not exist in 32-bit user32. Without the
            // hook, edges still resize and Tick snaps the aspect once the mouse is let go.
            if (IntPtr.Size == 8 && hooked == null)
            {
                hooked = this;
                previousWindowProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(windowProc));
                if (previousWindowProc == IntPtr.Zero)
                    hooked = null;
            }
            return true;
        }

        public override void Detach()
        {
            if (hooked != this)
                return;
            SetWindowLongPtr(hwnd, GWLP_WNDPROC, previousWindowProc);
            hooked = null;
        }

        [AOT.MonoPInvokeCallback(typeof(WndProcDelegate))]
        private static IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // Mid style change: swallowed before Unity (or Windows' default handling, which is what
            // turns WM_WINDOWPOSCHANGED into WM_SIZE / WM_MOVE) sees them. Returning 0 lets the
            // style change itself go ahead.
            if (holdSizeMessages && (msg == WM_WINDOWPOSCHANGED || msg == WM_SIZE || msg == WM_MOVE
                                     || msg == WM_STYLECHANGING || msg == WM_STYLECHANGED))
                return IntPtr.Zero;

            IntPtr result = CallWindowProc(previousWindowProc, hwnd, msg, wParam, lParam);
            WindowsWindow window = hooked;
            if (window == null)
                return result;

            try
            {
                if (msg == WM_NCHITTEST)
                {
                    // Edges become plain border - no resize cursor, no resizing. Corners stay.
                    int hit = result.ToInt32();
                    if (hit == HTLEFT || hit == HTRIGHT || hit == HTTOP || hit == HTBOTTOM)
                        return (IntPtr)HTBORDER;
                }
                else if (msg == WM_SIZING)
                {
                    var rect = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT));
                    window.HoldAspectWhileSizing(ref rect, wParam.ToInt32());
                    Marshal.StructureToPtr(rect, lParam, false);
                    return (IntPtr)1;
                }
            }
            catch (Exception e)
            {
                // Never let an exception escape into Windows' message loop.
                Debug.LogWarning("[WindowsPreviewResizingManager] " + e.Message);
            }
            return result;
        }

        /// <summary>
        /// Reshapes one step of a corner drag: the content goes to the aspect (height driving for
        /// portrait, width for landscape), clamped to the work area, and the corner opposite the
        /// one being dragged stays where it is.
        /// </summary>
        private void HoldAspectWhileSizing(ref RECT rect, int corner)
        {
            RECT border = Borders();
            int borderWidth = border.Left + border.Right;
            int borderHeight = border.Top + border.Bottom;

            bool movesLeft = corner == WMSZ_LEFT || corner == WMSZ_TOPLEFT || corner == WMSZ_BOTTOMLEFT;
            bool movesTop = corner == WMSZ_TOP || corner == WMSZ_TOPLEFT || corner == WMSZ_TOPRIGHT;

            // Room between the fixed corner and the edge of the work area the drag is heading for.
            RECT work = WorkArea();
            int roomWidth = (movesLeft ? rect.Right - work.Left : work.Right - rect.Left) - borderWidth;
            int roomHeight = (movesTop ? rect.Bottom - work.Top : work.Bottom - rect.Top) - borderHeight;
            int maxHeight = Mathf.Min(roomHeight, Mathf.FloorToInt(roomWidth / Aspect));

            int draggedWidth = rect.Right - rect.Left - borderWidth;
            int draggedHeight = rect.Bottom - rect.Top - borderHeight;
            int height = Aspect > 1f ? Mathf.RoundToInt(draggedWidth / Aspect) : draggedHeight;
            height = Mathf.Clamp(height, 200, Mathf.Max(200, maxHeight));
            int width = Mathf.RoundToInt(height * Aspect);

            int windowWidth = width + borderWidth;
            int windowHeight = height + borderHeight;
            if (movesLeft)
                rect.Left = rect.Right - windowWidth;
            else
                rect.Right = rect.Left + windowWidth;
            if (movesTop)
                rect.Top = rect.Bottom - windowHeight;
            else
                rect.Bottom = rect.Top + windowHeight;

            clientWidth = width;
            clientHeight = height;
        }

        public override void SetTitleBarHidden(bool hidden)
        {
            // Where the content sits on screen now, and its exact size, before the frame changes.
            GetClientRect(hwnd, out RECT client);
            int width = client.Right;
            int height = client.Bottom;
            var contentTopLeft = new POINT();
            ClientToScreen(hwnd, ref contentTopLeft);

            // Wrap the new frame around exactly the same content - same size, same place.
            titleBarHidden = hidden;
            uint style = StyleFor(hidden);
            RECT border = FrameFor(style);
            unexpectedResizeLogs = 0;
            Place(width, height, border, contentTopLeft.X - border.Left, contentTopLeft.Y - border.Top, false, style);

            GetClientRect(hwnd, out RECT after);
            Debug.Log("[WindowsPreviewResizingManager] Title bar " + (hidden ? "hidden" : "shown") + ": content " +
                      width + "x" + height + " -> " + after.Right + "x" + after.Bottom +
                      " (frame predicted L" + border.Left + " T" + border.Top + " R" + border.Right + " B" + border.Bottom + ").");
        }

        /// <summary>
        /// How far the window's outer edge sits from its content on each side, measured from the
        /// window as it is rather than worked out from its style, so it is right at any display
        /// scaling (and includes the invisible resize margins Windows 10/11 add).
        /// </summary>
        private RECT Borders()
        {
            GetWindowRect(hwnd, out RECT outer);
            GetClientRect(hwnd, out RECT client);
            var contentTopLeft = new POINT();
            ClientToScreen(hwnd, ref contentTopLeft);
            return new RECT
            {
                Left = contentTopLeft.X - outer.Left,
                Top = contentTopLeft.Y - outer.Top,
                Right = outer.Right - (contentTopLeft.X + client.Right),
                Bottom = outer.Bottom - (contentTopLeft.Y + client.Bottom),
            };
        }

        /// <summary>
        /// Sends Unity the WM_MOVE / WM_SIZE it would have had from a normal resize, with the
        /// window's content position and size as they are now.
        /// </summary>
        private void TellUnityWindowSize()
        {
            GetClientRect(hwnd, out RECT client);
            var contentTopLeft = new POINT();
            ClientToScreen(hwnd, ref contentTopLeft);
            SendMessage(hwnd, WM_MOVE, IntPtr.Zero, (IntPtr)(((contentTopLeft.Y & 0xFFFF) << 16) | (contentTopLeft.X & 0xFFFF)));
            SendMessage(hwnd, WM_SIZE, (IntPtr)SIZE_RESTORED, (IntPtr)(((client.Bottom & 0xFFFF) << 16) | (client.Right & 0xFFFF)));
        }

        /// <summary>True while the primary mouse button is held, even mid-drag on the window's edge.</summary>
        private static bool PrimaryButtonHeld()
        {
            int button = GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON;
            return (GetAsyncKeyState(button) & 0x8000) != 0;
        }

        /// <summary>
        /// The window's style with the title bar hidden (no title bar and no resize border, so it
        /// cannot be moved or resized) or showing (both back). Never maximisable: that would throw
        /// the window off the aspect, and double-clicking the title bar would do it by accident.
        /// </summary>
        private uint StyleFor(bool hideTitleBar)
        {
            uint style = (uint)GetWindowLong(hwnd, GWL_STYLE);
            style = hideTitleBar ? style & ~(WS_CAPTION | WS_THICKFRAME) : style | WS_CAPTION | WS_THICKFRAME;
            return style & ~WS_MAXIMIZEBOX;
        }

        /// <summary>
        /// How far the outer edge will sit from the content on each side with the given style -
        /// worked out ahead of applying it, at the window's own display scaling, so the style and
        /// the size can change together.
        /// </summary>
        private RECT FrameFor(uint style)
        {
            uint exStyle = (uint)GetWindowLong(hwnd, GWL_EXSTYLE);
            var rect = new RECT();
            try
            {
                AdjustWindowRectExForDpi(ref rect, style, false, exStyle, GetDpiForWindow(hwnd));
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 10 before 1607.
                AdjustWindowRectEx(ref rect, style, false, exStyle);
            }
            return new RECT { Left = -rect.Left, Top = -rect.Top, Right = rect.Right, Bottom = rect.Bottom };
        }

        public override void Fit(float heightFill)
        {
            uint style = StyleFor(titleBarHidden);
            RECT work = WorkArea();
            int height = Mathf.RoundToInt((work.Bottom - work.Top) * heightFill);
            Place(Mathf.RoundToInt(height * Aspect), height, FrameFor(style), 0, 0, true, style);
        }

        public override void Nudge(int step)
        {
            GetClientRect(hwnd, out RECT client);
            SetClientHeight(client.Bottom + step, false);
        }

        public override void Tick()
        {
            if (IsIconic(hwnd) || PrimaryButtonHeld())
                return;

            GetClientRect(hwnd, out RECT client);
            int width = client.Right;
            int height = client.Bottom;
            if (width == clientWidth && height == clientHeight)
                return;

            if (hooked == this)
            {
                // Every size the viewer chooses is recorded as it happens - corner drags in
                // HoldAspectWhileSizing, nudges and title bar toggles in Place - so a content size
                // that differs from the record came from somewhere else (e.g. the window reacting
                // late to a title bar toggle). Put the recorded size back rather than adopting it,
                // keeping the content where it is on screen.
                if (unexpectedResizeLogs < 5)
                {
                    unexpectedResizeLogs++;
                    Debug.Log("[WindowsPreviewResizingManager] Content changed to " + width + "x" + height +
                              " without a drag; restoring " + clientWidth + "x" + clientHeight + ".");
                }
                var contentTopLeft = new POINT();
                ClientToScreen(hwnd, ref contentTopLeft);
                RECT border = Borders();
                Place(clientWidth, clientHeight, border, contentTopLeft.X - border.Left, contentTopLeft.Y - border.Top, false, null);
                return;
            }

            // No hook (32-bit build): edges resize freely, so snap to the aspect once let go.
            SetClientHeight(SnappedHeight(width, height, clientWidth, clientHeight), false);
        }

        private RECT WorkArea()
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info);
            return info.rcWork;
        }

        /// <summary>
        /// Sizes the window so its content is the given height at the aspect, clamped to fit the
        /// work area, kept where it is (nudged back inside if needed).
        /// </summary>
        private void SetClientHeight(int height, bool centre)
        {
            GetWindowRect(hwnd, out RECT current);
            Place(Mathf.RoundToInt(height * Aspect), height, Borders(), current.Left, current.Top, centre, null);
        }

        /// <summary>
        /// Puts the window's content at exactly width x height, with the outer corner at (x, y) or
        /// centred in the work area, moved back inside the work area if it pokes out. Only resized
        /// (shrunk, at the aspect) if it cannot fit there at all.
        ///
        /// With a new style (border then being that style's frame), the style and the size change
        /// together without Unity seeing the in-between steps - otherwise it puts one of them back
        /// a moment later and the content grows or shrinks by the title bar.
        /// </summary>
        private void Place(int width, int height, RECT border, int x, int y, bool centre, uint? newStyle)
        {
            int borderWidth = border.Left + border.Right;
            int borderHeight = border.Top + border.Bottom;

            RECT work = WorkArea();
            int workWidth = work.Right - work.Left;
            int workHeight = work.Bottom - work.Top;
            int maxHeight = Mathf.Max(200, Mathf.Min(workHeight - borderHeight, Mathf.FloorToInt((workWidth - borderWidth) / Aspect)));
            if (height > maxHeight || height < 200)
            {
                height = Mathf.Clamp(height, 200, maxHeight);
                width = Mathf.RoundToInt(height * Aspect);
            }

            int windowWidth = width + borderWidth;
            int windowHeight = height + borderHeight;
            if (centre)
            {
                x = work.Left + (workWidth - windowWidth) / 2;
                y = work.Top + (workHeight - windowHeight) / 2;
            }
            x = Mathf.Clamp(x, work.Left, Mathf.Max(work.Left, work.Right - windowWidth));
            y = Mathf.Clamp(y, work.Top, Mathf.Max(work.Top, work.Bottom - windowHeight));

            if (newStyle.HasValue && hooked == this)
            {
                // Unity records a size from the window's messages part-way through a style change
                // and puts it back a moment later - whichever order the style and the size are
                // changed in. So Unity sees none of them: the style and the final size are applied
                // in one call with its size messages held back, then Unity is told the final size
                // once, by itself.
                holdSizeMessages = true;
                try
                {
                    SetWindowLong(hwnd, GWL_STYLE, unchecked((int)newStyle.Value));
                    SetWindowPos(hwnd, IntPtr.Zero, x, y, windowWidth, windowHeight, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                }
                finally
                {
                    holdSizeMessages = false;
                }
                TellUnityWindowSize();
            }
            else if (newStyle.HasValue)
            {
                // No hook (32-bit build): final outline first, then the style keeping it.
                SetWindowPos(hwnd, IntPtr.Zero, x, y, windowWidth, windowHeight, SWP_NOZORDER | SWP_NOACTIVATE);
                SetWindowLong(hwnd, GWL_STYLE, unchecked((int)newStyle.Value));
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            else
            {
                SetWindowPos(hwnd, IntPtr.Zero, x, y, windowWidth, windowHeight, SWP_NOZORDER | SWP_NOACTIVATE);
            }
            clientWidth = width;
            clientHeight = height;

            // If the frame came out a pixel or two different from what FrameFor predicted, correct
            // it now against the frame as it actually is.
            GetClientRect(hwnd, out RECT client);
            if (client.Right != width || client.Bottom != height)
            {
                RECT actual = Borders();
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width + actual.Left + actual.Right, height + actual.Top + actual.Bottom,
                    SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }
    }

    #endregion
}
