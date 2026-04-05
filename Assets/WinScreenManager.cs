using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Attach to a new GameObject called "WinScreenManager".
/// Shows a win panel when all differences are found or time runs out,
/// with a restart button that resets the whole game.
/// </summary>
public class WinScreenManager : MonoBehaviour
{
    [Header("=== References ===")]
    public ImageSwipeController imageSwipeController;
    public HotspotManager hotspotManager;

    [Header("=== Win Screen UI ===")]
    public GameObject winPanel;           // The panel that appears on win
    public Text winTitleText;             // "You found them all!" or "Time's up!"
    public Text winScoreText;             // Shows final score
    public Text winTimeText;              // Shows time taken
    public Text winFoundText;             // Shows how many found
    public Button restartButton;          // The restart button

    [Header("=== Time Up Screen UI ===")]
    public GameObject timeUpPanel;        // Separate panel for time up (optional)
    public Text timeUpFoundText;          // Shows how many found before time ran out

    void Start()
    {
        // Make sure panels are hidden at start
        if (winPanel)    winPanel.SetActive(false);
        if (timeUpPanel) timeUpPanel.SetActive(false);

        // Wire up restart button
        if (restartButton)
            restartButton.onClick.AddListener(RestartGame);
    }

    // Call this from HotspotManager when all found
    public void ShowWinScreen(int score, float timeElapsed, int found, int total)
    {
        Debug.Log("ShowWinScreen called");
        if (winPanel) winPanel.SetActive(true);
        Debug.Log("WinPanel set active");


        if (winTitleText)  winTitleText.text  = "You found them all!";
        if (winScoreText)  winScoreText.text  = "Score: " + score;
        if (winTimeText)   winTimeText.text   = "Time: " + FormatTime(timeElapsed);
        if (winFoundText)  winFoundText.text  = "Found: " + found + "/" + total;
    }

    // Call this from ImageSwipeController when timer hits zero
    public void ShowTimeUpScreen(int found, int total)
    {
        Debug.Log("ShowTimeUpScreen called");

        GameObject panel = timeUpPanel != null ? timeUpPanel : winPanel;

        if (panel == null)
        {
            Debug.LogError("Neither timeUpPanel nor winPanel is assigned!");
            return;
        }

        panel.SetActive(true);

        // Override all text fields for time up context
        if (winTitleText)    winTitleText.text    = "Time's Up!";
        if (winScoreText)    winScoreText.text    = "";
        if (winTimeText)     winTimeText.text     = "";
        if (winFoundText)    winFoundText.text    = "You found " + found + " of " + total;
        if (timeUpFoundText) timeUpFoundText.text = "You found " + found + " of " + total;
    }

    void RestartGame()
    {
        // Hide panels
        if (winPanel)    winPanel.SetActive(false);
        if (timeUpPanel) timeUpPanel.SetActive(false);

        // Reset game logic
        if (imageSwipeController) imageSwipeController.ResetGame();
        if (hotspotManager)       hotspotManager.ResetHotspots();
    }

    string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return string.Format("{0:00}:{1:00}", m, s);
    }
}