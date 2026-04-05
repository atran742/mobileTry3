using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles swipe between two images, pinch-to-zoom, pan, and HUD updates.
/// Fixes: zoom now scales the Image directly (not the panel), so hotspots
/// stay correctly layered and visible. Images are centered properly.
/// </summary>
public class ImageSwipeController : MonoBehaviour
{
    [Header("=== Image Panels ===")]
    public RectTransform imageContainer;
    public RectTransform imagePanel1;
    public RectTransform imagePanel2;
    public RectTransform image1Rect;    // RectTransform on Image1 (child of ImagePanel1)
    public RectTransform image2Rect;    // RectTransform on Image2 (child of ImagePanel2)

    [Header("=== HUD - Top Bar ===")]
    public Text scoreText;
    public Text timerText;
    public Text itemsFoundText;

    [Header("=== HUD - Bottom Bar ===")]
    public Text imageLabel;
    [Header("=== Win Screen ===")]
    public WinScreenManager winScreenManager;

    [Header("=== Game Settings ===")]
    public int totalItems = 21;
    public bool timerCountsDown = false;
    public float startTime = 60f;

    [Header("=== Swipe Settings ===")]
    public float swipeThreshold = 60f;
    public float snapSpeed = 10f;

    [Header("=== Zoom Settings ===")]
    public float minZoom = 1f;
    public float maxZoom = 5f;
    public float zoomSpeed = 0.02f;

    // Public so HotspotManager can stop the timer on win
    [HideInInspector] public bool gameActive = true;

    // ── Private state ─────────────────────────────────────────────
    private int   currentPage  = 0;
    private float score        = 0f;
    private int   itemsFound   = 0;
    private float elapsedTime  = 0f;
    private float panelWidth;
    private float panelHeight;
    private float targetX;

    // Swipe
    private Vector2 touchStartPos;
    private bool    isSwiping      = false;
    private bool    mouseSwiping   = false;

    // Zoom & pan — applied to the image RectTransform directly
    private float   currentZoom    = 1f;
    private Vector2 imageOffset    = Vector2.zero; // pan offset on the active image
    private bool    isPinching     = false;
    private float   lastPinchDist  = 0f;
    private Vector2 panStart;
    private Vector2 imageStartPos;

    // ── Lifecycle ─────────────────────────────────────────────────
    void Start()
    {
        // Must run after layout so rect sizes are calculated
        Canvas.ForceUpdateCanvases();

        panelWidth  = imagePanel1.rect.width;
        panelHeight = imagePanel1.rect.height;

        // Force both panels to their correct positions
        imagePanel1.anchoredPosition = new Vector2(0f, 0f);
        imagePanel2.anchoredPosition = new Vector2(panelWidth, 0f);

        // Start showing Image 1
        currentPage = 0;
        targetX     = 0f;
        SetContainerX(0f);

        if (image1Rect) image1Rect.anchoredPosition = Vector2.zero;
        if (image2Rect) image2Rect.anchoredPosition = Vector2.zero;

        UpdateHUD();
    }

    void Update()
    {
        if (gameActive) UpdateTimer();
        HandleInput();
        SmoothSnapToPage();
        UpdateHUD();
    }

    // ── Timer ─────────────────────────────────────────────────────
    void UpdateTimer()
    {
        elapsedTime += Time.deltaTime;
        if (timerCountsDown)
        {
            float remaining = Mathf.Max(0f, startTime - elapsedTime);
            if (timerText) timerText.text = FormatTime(remaining);
            if (remaining <= 0f) OnTimeUp();
        }
        else
        {
            if (timerText) timerText.text = FormatTime(elapsedTime);
        }
    }

    string FormatTime(float s)
    {
        int m = (int)(s / 60);
        int sec = (int)(s % 60);
        return string.Format("{0:00}:{1:00}", m, sec);
    }

    void OnTimeUp()
    {
        gameActive = false;
        if (winScreenManager != null)
            winScreenManager.ShowTimeUpScreen(itemsFound, totalItems);
    }

    // ── Public API ────────────────────────────────────────────────
    public void OnItemFound(float points = 100f)
    {
        itemsFound = Mathf.Min(itemsFound + 1, totalItems);
        score += points;
        UpdateHUD();
    }

    public void ResetGame()
    {
        score = 0; itemsFound = 0; elapsedTime = 0f; gameActive = true;
        UpdateHUD();
    }

    // ── HUD ───────────────────────────────────────────────────────
    void UpdateHUD()
    {
        if (scoreText)      scoreText.text      = "Score: " + (int)score;
        if (itemsFoundText) itemsFoundText.text = "Found: " + itemsFound + "/" + totalItems;
        if (imageLabel)     imageLabel.text      = currentPage == 0 ? "Original  (swipe)" : "Altered  (swipe)";
    }

    // ── Input routing ─────────────────────────────────────────────
    void HandleInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    // ── Touch ─────────────────────────────────────────────────────
    void HandleTouchInput()
    {
        if (Input.touchCount == 2)
        {
            isPinching = true;
            isSwiping  = false;
            HandlePinchZoom();
            return;
        }

        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            // First frame after lifting second finger — skip to avoid phantom swipe
            if (isPinching)
            {
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    isPinching = false;
                return;
            }

            if (currentZoom > 1.01f)
                HandlePanAtZoom(t.phase, t.position);
            else
                HandleSwipe(t.phase, t.position);
        }
        else
        {
            isPinching = false;
        }
    }

    void HandlePinchZoom()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        float dist = Vector2.Distance(t0.position, t1.position);

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        {
            lastPinchDist = dist;
            return;
        }

        float delta = dist - lastPinchDist;
        lastPinchDist = dist;
        ApplyZoom(currentZoom + delta * zoomSpeed);
    }

    // ── Mouse (Editor) ────────────────────────────────────────────
    void HandleMouseInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            ApplyZoom(currentZoom + scroll * 3f);

        if (Input.GetMouseButtonDown(0))
        {
            touchStartPos  = Input.mousePosition;
            panStart       = Input.mousePosition;
            imageStartPos  = ActiveImageRect().anchoredPosition;
            mouseSwiping   = true;
        }
        else if (Input.GetMouseButton(0) && currentZoom > 1.01f)
        {
            Vector2 delta = (Vector2)Input.mousePosition - panStart;
            ApplyPan(imageStartPos + delta);
            mouseSwiping = false;
        }
        else if (Input.GetMouseButtonUp(0) && mouseSwiping)
        {
            float dx = ((Vector2)Input.mousePosition - touchStartPos).x;
            if (currentZoom <= 1.01f) TryChangePage(dx);
            mouseSwiping = false;
        }
    }

    // ── Swipe ─────────────────────────────────────────────────────
    void HandleSwipe(TouchPhase phase, Vector2 pos)
    {
        if (phase == TouchPhase.Began)
        {
            touchStartPos = pos;
            isSwiping     = true;
        }
        else if (phase == TouchPhase.Ended && isSwiping)
        {
            TryChangePage(pos.x - touchStartPos.x);
            isSwiping = false;
        }
    }

    void TryChangePage(float dx)
    {
        if (dx < -swipeThreshold && currentPage == 0)
        {
            currentPage = 1;
            targetX     = -panelWidth;
            ResetZoom();
        }
        else if (dx > swipeThreshold && currentPage == 1)
        {
            currentPage = 0;
            targetX     = 0f;
            ResetZoom();
        }
    }

    void SmoothSnapToPage()
    {
        if (currentZoom > 1.01f) return;
        Vector2 pos = imageContainer.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * snapSpeed);
        imageContainer.anchoredPosition = pos;
    }

    // ── Pan while zoomed ──────────────────────────────────────────
    void HandlePanAtZoom(TouchPhase phase, Vector2 pos)
    {
        if (phase == TouchPhase.Began)
        {
            panStart      = pos;
            imageStartPos = ActiveImageRect().anchoredPosition;
        }
        else if (phase == TouchPhase.Moved)
        {
            Vector2 delta = pos - panStart;
            ApplyPan(imageStartPos + delta);
        }
    }

    void ApplyPan(Vector2 desiredPos)
    {
        // Clamp so user can't pan past the edges of the zoomed image
        float maxOffsetX = panelWidth  * (currentZoom - 1f) * 0.5f;
        float maxOffsetY = panelHeight * (currentZoom - 1f) * 0.5f;

        desiredPos.x = Mathf.Clamp(desiredPos.x, -maxOffsetX, maxOffsetX);
        desiredPos.y = Mathf.Clamp(desiredPos.y, -maxOffsetY, maxOffsetY);

        ActiveImageRect().anchoredPosition = desiredPos;
    }
    
    // Getters for WinScreenManager
    public int   GetScore()       { return (int)score; }
    public float GetElapsedTime() { return elapsedTime; }

    // ── Zoom ──────────────────────────────────────────────────────
    // Zoom scales the Image RectTransform directly — NOT the panel.
    // This keeps hotspots (siblings of the image, children of the panel)
    // in their correct screen positions so they stay tappable and visible.
    void ApplyZoom(float newZoom)
    {
        currentZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
        ActiveImageRect().localScale = Vector3.one * currentZoom;

        if (currentZoom <= minZoom + 0.01f)
            ResetZoom();
    }

    void ResetZoom()
    {
        currentZoom = 1f;
        if (image1Rect)
        {
            image1Rect.localScale        = Vector3.one;
            image1Rect.anchoredPosition  = Vector2.zero;
        }
        if (image2Rect)
        {
            image2Rect.localScale        = Vector3.one;
            image2Rect.anchoredPosition  = Vector2.zero;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────
    RectTransform ActiveImageRect()
    {
        return currentPage == 0 ? image1Rect : image2Rect;
    }

    void SetContainerX(float x)
    {
        Vector2 pos = imageContainer.anchoredPosition;
        pos.x = x;
        imageContainer.anchoredPosition = pos;
    }
}