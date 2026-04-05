using UnityEngine;

/// <summary>
/// Attach this to your Canvas root GameObject.
/// It automatically adjusts the layout to respect the iPhone notch
/// at the top and the home indicator bar at the bottom.
/// </summary>
public class SafeAreaHandler : MonoBehaviour
{
    [Header("=== HUD Bars ===")]
    public RectTransform topBar;
    public RectTransform bottomBar;
    public RectTransform imageContainer;

    [Header("=== Base Heights ===")]
    public float baseTopBarHeight    = 120f;
    public float baseBottomBarHeight = 80f;

    private Rect lastSafeArea = Rect.zero;

    void Start()  { ApplySafeArea(); }
    void Update() { if (Screen.safeArea != lastSafeArea) ApplySafeArea(); }

    void ApplySafeArea()
    {
        lastSafeArea = Screen.safeArea;

        float screenH = Screen.height;

        // How many pixels are eaten by the notch at top and home bar at bottom
        float topInset    = screenH - Screen.safeArea.yMax;
        float bottomInset = Screen.safeArea.yMin;

        // Convert to Canvas units
        Canvas canvas = GetComponent<Canvas>();
        float scale   = canvas != null ? canvas.scaleFactor : 1f;

        float topInsetUnits    = topInset    / scale;
        float bottomInsetUnits = bottomInset / scale;

        // Grow the HUD bars to cover the insets
        if (topBar)
        {
            float newTopHeight = baseTopBarHeight + topInsetUnits;
            topBar.sizeDelta   = new Vector2(topBar.sizeDelta.x, newTopHeight);
        }

        if (bottomBar)
        {
            float newBottomHeight    = baseBottomBarHeight + bottomInsetUnits;
            bottomBar.sizeDelta      = new Vector2(bottomBar.sizeDelta.x, newBottomHeight);
        }

        // Shrink the image area to match
        if (imageContainer)
        {
            float usedHeight         = baseTopBarHeight + topInsetUnits
                                     + baseBottomBarHeight + bottomInsetUnits;
            float availableH         = 926f - usedHeight;
            imageContainer.sizeDelta = new Vector2(imageContainer.sizeDelta.x, availableH);
        }

        Debug.Log($"Safe area applied — top inset: {topInsetUnits:F0}pt, bottom inset: {bottomInsetUnits:F0}pt");
    }
}