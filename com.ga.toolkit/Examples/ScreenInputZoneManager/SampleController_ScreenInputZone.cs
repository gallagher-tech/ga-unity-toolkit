using System.Collections.Generic;
using GAToolkit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sample-only controller for the Screen Input Zones demo scene. Each zone calls its own method
/// here, which flashes that zone's indicator square green and appends a line to the readout.
///
/// Not library code - this belongs with the sample scene, not with the runtime component.
///
/// The scene wires two zones, both Respond To: Tap, differing only in Detect Outside:
///
///   TAP INSIDE   zone = the grey box,  Detect Outside off -> fires when you tap the box
///   TAP OUTSIDE  zone = the grey box,  Detect Outside ON  -> fires when you tap anything else
///
/// A zone covers the whole screen, not its half of the layout, so tapping the LEFT box also
/// fires the right indicator - the left box IS outside the right box. That is correct.
///
/// A zone has no name of its own, so which method gets called is how you tell zones apart.
/// </summary>
public class SampleController_ScreenInputZone : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField, Tooltip("The manager driving this scene. Only read for the readout's idle text.")]
    private ScreenInputZoneManager screenInputZoneManager;

    [Header("Readout")]
    [SerializeField, Tooltip("Label the most recent zone events are printed to.")]
    private TMP_Text readoutLabel;

    [SerializeField, Tooltip("How many events to keep on screen.")]
    private int linesToKeep = 5;

    [Header("Indicators")]
    [SerializeField, Tooltip("Flashes when the Detect Outside OFF zone fires.")]
    private Image tapInsideIndicator;

    [SerializeField, Tooltip("Flashes when the Detect Outside ON zone fires.")]
    private Image tapOutsideIndicator;

    [SerializeField, Tooltip("Flashes when the Drag / Detect Outside OFF zone fires.")]
    private Image dragInsideIndicator;

    [SerializeField, Tooltip("Flashes when the Drag / Detect Outside ON zone fires.")]
    private Image dragOutsideIndicator;

    [SerializeField] private Color idleColor = new Color(0.224f, 0.224f, 0.224f, 1f);
    [SerializeField] private Color firedColor = new Color(0f, 0.85f, 0.10f, 1f);

    [SerializeField, Tooltip("Seconds for an indicator to fade from fired back to idle.")]
    private float flashDuration = 0.6f;

    private readonly List<string> lines = new List<string>();

    // Remaining flash time per indicator. A fresh hit restarts it, so repeated taps keep
    // re-triggering rather than being swallowed by the previous fade.
    private float insideFlash;
    private float outsideFlash;
    private float dragInsideFlash;
    private float dragOutsideFlash;

    void Start()
    {
        Apply(tapInsideIndicator, 0f);
        Apply(tapOutsideIndicator, 0f);
        Apply(dragInsideIndicator, 0f);
        Apply(dragOutsideIndicator, 0f);
        Render();
    }

    void Update()
    {
        insideFlash = Mathf.Max(0f, insideFlash - Time.unscaledDeltaTime);
        outsideFlash = Mathf.Max(0f, outsideFlash - Time.unscaledDeltaTime);

        Apply(tapInsideIndicator, insideFlash);
        Apply(tapOutsideIndicator, outsideFlash);

        dragInsideFlash = Mathf.Max(0f, dragInsideFlash - Time.unscaledDeltaTime);
        dragOutsideFlash = Mathf.Max(0f, dragOutsideFlash - Time.unscaledDeltaTime);
        Apply(dragInsideIndicator, dragInsideFlash);
        Apply(dragOutsideIndicator, dragOutsideFlash);
    }

    /// <summary>Zone 1 - the grey box, Detect Outside off.</summary>
    public void OnTapInside(string hitObjectName)
    {
        insideFlash = flashDuration;
        Log("Inside", hitObjectName);
    }

    /// <summary>Zone 2 - the same shape of zone, Detect Outside ON.</summary>
    public void OnTapOutside(string hitObjectName)
    {
        outsideFlash = flashDuration;
        Log("Outside", hitObjectName);
    }

    /// <summary>Zone 3 - the two drag boxes, Detect Outside off.</summary>
    public void OnDragInside(string hitObjectName)
    {
        dragInsideFlash = flashDuration;
        Log("Drag inside", hitObjectName);
    }

    /// <summary>Zone 4 - the two drag boxes, Detect Outside ON.</summary>
    public void OnDragOutside(string hitObjectName)
    {
        dragOutsideFlash = flashDuration;
        Log("Drag outside", hitObjectName);
    }

    private void Apply(Image indicator, float remaining)
    {
        if (indicator == null)
        {
            return;
        }

        float t = flashDuration > 0f ? Mathf.Clamp01(remaining / flashDuration) : 0f;
        indicator.color = Color.Lerp(idleColor, firedColor, t);
    }

    private void Log(string which, string hitObjectName)
    {
        string hit = string.IsNullOrEmpty(hitObjectName) ? "nothing" : hitObjectName;
        lines.Add($"<b>{which}</b>  ->  {hit}");

        while (lines.Count > Mathf.Max(1, linesToKeep))
        {
            lines.RemoveAt(0);
        }

        Render();
    }

    private void Render()
    {
        if (readoutLabel == null)
        {
            return;
        }

        readoutLabel.text = lines.Count == 0
            ? (screenInputZoneManager != null ? "Waiting for a tap." : "No manager assigned.")
            : string.Join("\n", lines);
    }
}
