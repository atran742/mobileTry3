using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class HotspotManager : MonoBehaviour
{
    [Header("=== References ===")]
    public ImageSwipeController imageSwipeController;
    public WinScreenManager winScreenManager;
    public Canvas canvas;

    [Header("=== Feedback ===")]
    public AudioClip correctSound;

    [Header("=== Hotspot Visual (Editor only) ===")]
    [Tooltip("Show hotspot zones while positioning. Uncheck before final build.")]
    public bool showHotspots = true;

    [System.Serializable]
    public class Difference
    {
        public string label;
        public RectTransform hotspot;
        [HideInInspector] public bool found = false;
    }

    [Header("=== Differences ===")]
    public List<Difference> differences = new List<Difference>();

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        InitZones();
    }

    void InitZones()
    {
        foreach (Difference diff in differences)
        {
            if (diff.hotspot == null) continue;
            HotspotZone zone = diff.hotspot.GetComponent<HotspotZone>();
            if (zone != null)
                zone.parentDifference = diff;
            SetHotspotAlpha(diff.hotspot, showHotspots ? 0.25f : 0f);
        }
    }

    void Update()
    {
        HandleTapInput();
    }

    void HandleTapInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
            CheckTapAt(Input.mousePosition);
#else
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
            CheckTapAt(Input.GetTouch(0).position);
#endif
    }

    void CheckTapAt(Vector2 screenPos)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            HotspotZone zone = result.gameObject.GetComponent<HotspotZone>();
            if (zone != null)
            {
                ProcessTap(zone);
                return;
            }
        }
    }

    void ProcessTap(HotspotZone zone)
    {
        Difference diff = zone.parentDifference;
        if (diff == null) return;

        if (diff.found)
        {
            StartCoroutine(PulseRoutine(diff.hotspot));
            return;
        }

        diff.found = true;
        ShowFound(diff.hotspot);
        PlaySound(correctSound);
        imageSwipeController.OnItemFound(100f);
        CheckAllFound();
    }

    void ShowFound(RectTransform hotspot)
    {
        if (hotspot == null) return;
        Image img = hotspot.GetComponent<Image>();
        if (img == null) return;
        img.color = new Color(0.2f, 0.9f, 0.4f, 0.85f);
    }

    IEnumerator PulseRoutine(RectTransform hotspot)
    {
        if (hotspot == null) yield break;
        Image img = hotspot.GetComponent<Image>();
        if (img == null) yield break;
        img.color = new Color(0.2f, 0.9f, 0.4f, 1f);
        yield return new WaitForSeconds(0.2f);
        img.color = new Color(0.2f, 0.9f, 0.4f, 0.85f);
    }

    void CheckAllFound()
    {
        foreach (Difference diff in differences)
            if (!diff.found) return;

        // All found — show win screen
        imageSwipeController.gameActive = false;

        if (winScreenManager != null)
            winScreenManager.ShowWinScreen(
                imageSwipeController.GetScore(),
                imageSwipeController.GetElapsedTime(),
                differences.Count,
                differences.Count
            );
    }

    // Called by WinScreenManager when restart button is pressed
    public void ResetHotspots()
    {
        foreach (Difference diff in differences)
        {
            diff.found = false;
            SetHotspotAlpha(diff.hotspot, showHotspots ? 0.25f : 0f);
        }
    }

    void SetHotspotAlpha(RectTransform hotspot, float alpha)
    {
        if (hotspot == null) return;
        Image img = hotspot.GetComponent<Image>();
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}