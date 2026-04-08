using UnityEngine;
using UnityEngine.UI;

public class ImageSwipeController : MonoBehaviour
{
    [Header("=== Win Screen ===")]
    public WinScreenManager winScreenManager;

    [Header("=== Image Panels ===")]
    public RectTransform imageContainer;
    public RectTransform imagePanel1;
    public RectTransform imagePanel2;
    public RectTransform image1Rect;
    public RectTransform image2Rect;

    [Header("=== HUD - Top Bar ===")]
    public Text scoreText;
    public Text timerText;
    public Text itemsFoundText;

    [Header("=== HUD - Bottom Bar ===")]
    public Text imageLabel;

    [Header("=== Game Settings ===")]
    public int   totalItems      = 21;
    public bool  timerCountsDown = true;
    public float startTime       = 60f;

    [Header("=== Swipe Settings ===")]
    public float swipeThreshold = 60f;
    public float snapSpeed      = 10f;

    [Header("=== Zoom Settings ===")]
    public float minZoom          = 1f;
    public float maxZoom          = 5f;
    public float pinchSensitivity = 200f;   // lower = faster zoom
    public float scrollSensitivity = 2f;

    [HideInInspector] public bool gameActive = true;

    // ── Private state ─────────────────────────────────────────────
    private int   currentPage = 0;
    private float score       = 0f;
    private int   itemsFound  = 0;
    private float elapsedTime = 0f;
    private float panelWidth;
    private float panelHeight;
    private float targetX;
    private float currentZoom    = 1f;
    private Canvas rootCanvas;

    // Swipe
    private Vector2 touchStartPos;
    private bool    isSwiping    = false;
    private bool    mouseSwiping = false;

    // Pinch
    private bool  isPinching    = false;
    private float lastPinchDist = 0f;

    // Pan
    private Vector2 panStartCanvas;   // pan start in canvas space
    private Vector2 imageStartPos;    // image anchoredPosition when pan started

    // ── Lifecycle ─────────────────────────────────────────────────
    void Start()
    {
        // Get the root canvas so we can convert screen → canvas coords
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = FindObjectOfType<Canvas>();

        Canvas.ForceUpdateCanvases();

        panelWidth  = imagePanel1.rect.width;
        panelHeight = imagePanel1.rect.height;

        imagePanel1.anchoredPosition = new Vector2(0f, 0f);
        imagePanel2.anchoredPosition = new Vector2(panelWidth, 0f);

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
        int m   = (int)(s / 60);
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
        score     += points;
        UpdateHUD();
    }

    public void ResetGame()
    {
        score = 0; itemsFound = 0; elapsedTime = 0f; gameActive = true;
        currentPage = 0; targetX = 0f;
        SetContainerX(0f);
        ResetZoom();
        UpdateHUD();
    }

    public int   GetScore()       { return (int)score; }
    public float GetElapsedTime() { return elapsedTime; }

    // ── HUD ───────────────────────────────────────────────────────
    void UpdateHUD()
    {
        if (scoreText)      scoreText.text      = "Score: " + (int)score;
        if (itemsFoundText) itemsFoundText.text = "Found: " + itemsFound + "/" + totalItems;
        if (imageLabel)     imageLabel.text      = currentPage == 0
                                                    ? "Original  (swipe)"
                                                    : "Altered  (swipe)";
    }

    // ── Coordinate conversion ─────────────────────────────────────
    // Converts a screen-space point (pixels) to canvas local space.
    // This is the key fix — touch positions are always in screen pixels
    // but RectTransform anchoredPosition is in canvas units.
    // Without this conversion, zoom pivot is wrong on real devices.
    Vector2 ScreenToCanvasPoint(Vector2 screenPoint)
    {
        Vector2 canvasPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            screenPoint,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out canvasPoint
        );
        return canvasPoint;
    }

    // ── Input ─────────────────────────────────────────────────────
    void HandleInput()
    {
        HandleScrollWheel();
        HandleTouchInput();
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseDrag();
#endif
    }

    // ── Scroll wheel ──────────────────────────────────────────────
    void HandleScrollWheel()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            // Zoom toward mouse cursor position
            Vector2 pivot = ScreenToCanvasPoint(Input.mousePosition);
            ApplyZoomAtPoint(currentZoom + scroll * scrollSensitivity, pivot);
        }
    }

    // ── Touch ─────────────────────────────────────────────────────
    void HandleTouchInput()
    {
        int touchCount = Input.touchCount;

        if (touchCount == 2)
        {
            isPinching = true;
            isSwiping  = false;
            HandlePinchZoom();
            return;
        }

        if (touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            if (isPinching)
            {
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    isPinching = false;
                return;
            }

            if (currentZoom > 1.01f)
                HandlePanTouch(t);
            else
                HandleSwipeTouch(t);
        }
        else if (touchCount == 0)
        {
            isPinching = false;
        }
    }

    void HandlePinchZoom()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        float currentDist = Vector2.Distance(t0.position, t1.position);

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        {
            lastPinchDist = currentDist;
            return;
        }

        float deltaDist = currentDist - lastPinchDist;
        lastPinchDist   = currentDist;

        float zoomDelta = deltaDist / pinchSensitivity;

        // Zoom toward the midpoint between the two fingers
        Vector2 midpointScreen = (t0.position + t1.position) * 0.5f;
        Vector2 midpointCanvas = ScreenToCanvasPoint(midpointScreen);

        ApplyZoomAtPoint(currentZoom + zoomDelta, midpointCanvas);
    }

    void HandleSwipeTouch(Touch t)
    {
        if (t.phase == TouchPhase.Began)
        {
            touchStartPos = t.position;
            isSwiping     = true;
        }
        else if (t.phase == TouchPhase.Moved)
        {
            isSwiping = Vector2.Distance(t.position, touchStartPos) >= 15f;
        }
        else if (t.phase == TouchPhase.Ended && isSwiping)
        {
            TryChangePage(t.position.x - touchStartPos.x);
            isSwiping = false;
        }
    }

    void HandlePanTouch(Touch t)
    {
        if (t.phase == TouchPhase.Began)
        {
            // Convert touch start to canvas space immediately
            panStartCanvas = ScreenToCanvasPoint(t.position);
            imageStartPos  = ActiveImageRect().anchoredPosition;
        }
        else if (t.phase == TouchPhase.Moved)
        {
            Vector2 currentCanvas = ScreenToCanvasPoint(t.position);
            Vector2 delta         = currentCanvas - panStartCanvas;
            ApplyPan(imageStartPos + delta);
        }
    }

    // ── Mouse drag (editor) ───────────────────────────────────────
    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            touchStartPos  = Input.mousePosition;
            panStartCanvas = ScreenToCanvasPoint(Input.mousePosition);
            imageStartPos  = ActiveImageRect().anchoredPosition;
            mouseSwiping   = true;
        }
        else if (Input.GetMouseButton(0) && currentZoom > 1.01f)
        {
            Vector2 currentCanvas = ScreenToCanvasPoint(Input.mousePosition);
            Vector2 delta         = currentCanvas - panStartCanvas;
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

    // ── Page swiping ──────────────────────────────────────────────
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

    // ── Zoom toward a canvas-space point ──────────────────────────
    // Instead of scaling from the center, we shift the image so the
    // point under the fingers stays fixed as the image grows.
    void ApplyZoomAtPoint(float newZoom, Vector2 canvasPivot)
    {
        float clampedZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
        if (clampedZoom <= minZoom + 0.01f)
        {
            ResetZoom();
            return;
        }

        RectTransform img  = ActiveImageRect();
        float         oldZoom  = currentZoom;
        Vector2       oldPos   = img.anchoredPosition;

        // How much the image will grow
        float zoomRatio = clampedZoom / oldZoom;

        // Shift the image so the pivot point stays under the fingers
        // Formula: newPos = pivot + (oldPos - pivot) * zoomRatio
        Vector2 newPos = canvasPivot + (oldPos - canvasPivot) * zoomRatio;

        currentZoom        = clampedZoom;
        img.localScale     = Vector3.one * currentZoom;

        // Clamp pan so we can't go outside image bounds
        float maxX = panelWidth  * (currentZoom - 1f) * 0.5f;
        float maxY = panelHeight * (currentZoom - 1f) * 0.5f;
        newPos.x   = Mathf.Clamp(newPos.x, -maxX, maxX);
        newPos.y   = Mathf.Clamp(newPos.y, -maxY, maxY);

        img.anchoredPosition = newPos;
    }

    // ── Zoom (simple, no pivot) ───────────────────────────────────
    void ApplyZoom(float newZoom)
    {
        ApplyZoomAtPoint(newZoom, Vector2.zero);
    }

    void ResetZoom()
    {
        currentZoom = 1f;
        if (image1Rect) { image1Rect.localScale = Vector3.one; image1Rect.anchoredPosition = Vector2.zero; }
        if (image2Rect) { image2Rect.localScale = Vector3.one; image2Rect.anchoredPosition = Vector2.zero; }
    }

    // ── Pan ───────────────────────────────────────────────────────
    void ApplyPan(Vector2 desiredPos)
    {
        float maxX = panelWidth  * (currentZoom - 1f) * 0.5f;
        float maxY = panelHeight * (currentZoom - 1f) * 0.5f;
        desiredPos.x = Mathf.Clamp(desiredPos.x, -maxX, maxX);
        desiredPos.y = Mathf.Clamp(desiredPos.y, -maxY, maxY);
        ActiveImageRect().anchoredPosition = desiredPos;
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