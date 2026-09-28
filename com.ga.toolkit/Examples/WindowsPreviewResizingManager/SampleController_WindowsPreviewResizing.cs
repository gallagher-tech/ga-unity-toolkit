using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sample-only controller for the Windows Preview Resizing demo scene. Wires the Landscape and
/// Portrait buttons to the manager, forces the starting orientation, and keeps an optional
/// readout label showing the window's live size and aspect.
///
/// Not library code - this belongs with the sample scene, not with the runtime component.
///
/// WindowsPreviewResizingManager only acts in a Windows player: in the Editor the buttons still
/// respond and the readout still updates, but the window itself will not move or resize. Build for
/// Windows with Player Settings > Resolution and Presentation > Resizable Window enabled.
/// </summary>
public class SampleController_WindowsPreviewResizing : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("The manager to drive. Left empty, the scene is searched on Awake.")]
    private WindowsPreviewResizingManager manager;

    [Header("Buttons")]
    [SerializeField] private Button landscapeButton;
    [SerializeField] private Button portraitButton;

    [Header("Orientations")]
    [SerializeField] private Vector2Int landscapeResolution = new Vector2Int(3840, 2160);
    [SerializeField] private Vector2Int portraitResolution = new Vector2Int(2160, 3840);

    [SerializeField, Tooltip("Applies the landscape resolution on Start, so the scene always opens the same way.")]
    private bool startInLandscape = true;

    [Header("Readout (optional)")]
    [SerializeField, Tooltip("Label updated every frame with the window's size, aspect and status.")]
    private TMP_Text readoutLabel;

    [Header("Responsive layout")]
    [SerializeField, Tooltip("The Screen Orientation panel. Sits left of the description in landscape, below it in portrait.")]
    private RectTransform orientationPanel;

    [SerializeField, Tooltip("The Description panel. Sits right of the orientation panel in landscape, above it in portrait.")]
    private RectTransform descriptionPanel;

    [SerializeField, Tooltip("Distance from the canvas edges.")]
    private Vector2 margin = new Vector2(48f, 48f);

    [SerializeField, Tooltip("Space between the two panels.")]
    private float gap = 32f;

    [SerializeField, Tooltip("Size of the orientation panel in landscape. Its width is ignored in portrait.")]
    private Vector2 orientationPanelSize = new Vector2(720f, 300f);

    [SerializeField, Tooltip("Height of the description panel when it is stacked on top in portrait.")]
    private float descriptionHeightPortrait = 360f;

    private bool layoutApplied;
    private bool lastWasPortrait;
    private float lastCanvasWidth;
    private TMP_Text descriptionText;

    void Awake()
    {
        if (manager == null)
            manager = FindAnyObjectByType<WindowsPreviewResizingManager>();

        if (descriptionPanel != null)
            descriptionText = descriptionPanel.GetComponent<TMP_Text>();

        if (landscapeButton != null)
            landscapeButton.onClick.AddListener(ApplyLandscape);

        if (portraitButton != null)
            portraitButton.onClick.AddListener(ApplyPortrait);
    }

    void Start()
    {
        if (startInLandscape)
            ApplyLandscape();
    }

    void OnDestroy()
    {
        if (landscapeButton != null)
            landscapeButton.onClick.RemoveListener(ApplyLandscape);

        if (portraitButton != null)
            portraitButton.onClick.RemoveListener(ApplyPortrait);
    }

    void Update()
    {
        if (readoutLabel != null)
            readoutLabel.text = Readout();
    }

    #region Orientation

    public void ApplyLandscape() => Apply(landscapeResolution);

    public void ApplyPortrait() => Apply(portraitResolution);

    private void Apply(Vector2Int resolution)
    {
        if (manager == null)
        {
            Debug.LogWarning("[SampleController_WindowsPreviewResizing] No WindowsPreviewResizingManager in the scene.");
            return;
        }

        manager.SetDesignResolution(resolution);
    }

    #endregion

    #region Readout

    private string Readout()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        string target = "-";
        string status = "no manager in scene";

        if (manager != null)
        {
            Vector2Int design = manager.DesignResolution;
            float designAspect = (float)design.x / Mathf.Max(1, design.y);
            target = $"{design.x} x {design.y}  ({designAspect:0.0000})";
            status = manager.IsManagingWindow
                ? "managing the OS window"
                : "inactive - Editor or non-Windows build";
        }

        return
            $"window   {Screen.width} x {Screen.height}\n" +
            $"aspect   {aspect:0.0000}  ({(aspect < 1f ? "portrait" : "landscape")})\n" +
            $"target   {target}\n" +
            $"status   {status}";
    }

    #endregion

    #region Responsive layout

    void LateUpdate()
    {
        RectTransform canvasRect = orientationPanel != null
            ? orientationPanel.parent as RectTransform
            : null;

        if (canvasRect == null)
            return;

        bool portrait = Screen.height > Screen.width;
        float canvasWidth = canvasRect.rect.width;

        // The canvas is resized by CanvasScaler after this runs on the frame the window changes,
        // so react to the width settling as well as to the orientation flipping.
        if (layoutApplied
            && portrait == lastWasPortrait
            && Mathf.Approximately(canvasWidth, lastCanvasWidth))
            return;

        lastWasPortrait = portrait;
        lastCanvasWidth = canvasWidth;
        layoutApplied = true;

        ApplyLayout(portrait, canvasWidth);
    }

    /// <summary>
    /// Landscape puts the two panels side by side; portrait stacks the description above the
    /// orientation panel. The description is sized to the height its text actually needs, because
    /// its TMP overflow mode lets text spill outside the rect and over whatever sits below it.
    /// </summary>
    private void ApplyLayout(bool portrait, float canvasWidth)
    {
        if (orientationPanel == null || descriptionPanel == null)
            return;

        AnchorTopLeft(orientationPanel);
        AnchorTopLeft(descriptionPanel);

        if (portrait)
        {
            float contentWidth = canvasWidth - margin.x * 2f;
            float descriptionHeight = Mathf.Max(descriptionHeightPortrait,
                                                PreferredHeight(contentWidth));

            descriptionPanel.anchoredPosition = new Vector2(margin.x, -margin.y);
            descriptionPanel.sizeDelta = new Vector2(contentWidth, descriptionHeight);

            orientationPanel.anchoredPosition =
                new Vector2(margin.x, -(margin.y + descriptionHeight + gap));
            orientationPanel.sizeDelta = new Vector2(contentWidth, orientationPanelSize.y);
        }
        else
        {
            orientationPanel.anchoredPosition = new Vector2(margin.x, -margin.y);
            orientationPanel.sizeDelta = orientationPanelSize;

            float descriptionX = margin.x + orientationPanelSize.x + gap;
            float descriptionWidth = Mathf.Max(0f, canvasWidth - descriptionX - margin.x);

            descriptionPanel.anchoredPosition = new Vector2(descriptionX, -margin.y);
            descriptionPanel.sizeDelta = new Vector2(
                descriptionWidth,
                Mathf.Max(orientationPanelSize.y, PreferredHeight(descriptionWidth)));
        }
    }

    /// <summary>Height the description's text needs at the given width, or 0 with no text.</summary>
    private float PreferredHeight(float width)
    {
        if (descriptionText == null || width <= 0f)
            return 0f;

        return descriptionText.GetPreferredValues(descriptionText.text, width, 0f).y;
    }

    private static void AnchorTopLeft(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
    }

    #endregion
}
