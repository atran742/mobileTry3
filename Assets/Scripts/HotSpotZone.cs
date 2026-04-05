using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to every hotspot GameObject inside ImagePanel2.
/// HotspotManager assigns parentDifference at Start via InitZones().
/// </summary>
public class HotspotZone : MonoBehaviour
{
    [HideInInspector]
    public HotspotManager.Difference parentDifference;

    void Awake()
    {
        // Ensure Image component exists and is set up for raycasting
        Image img = GetComponent<Image>();
        if (img == null)
            img = gameObject.AddComponent<Image>();

        img.color         = new Color(1f, 1f, 1f, 0f); // start invisible
        img.raycastTarget = true;                        // must be true to receive taps
    }
}