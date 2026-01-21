using UnityEngine;
using UnityEngine.UI;
using TMPro; // Ensure TextMeshPro is installed

public class HostUIManager : MonoBehaviour
{
    [Header("UI Components")]
    public Button actionBtn;           // Main Action Button (Start/Pause/Resume)
    public TextMeshProUGUI btnText;    // Text on button

    [Header("Settings")]
    public Color startColor = Color.green;
    public Color pauseColor = Color.yellow;
    public Color resumeColor = Color.cyan;

    // Game States
    private enum GameState { Ready, Playing, Paused }
    private GameState currentState = GameState.Ready;

    void Start()
    {
        if (actionBtn != null)
        {
            // Assign Click event
            actionBtn.onClick.AddListener(HandleButtonClick);

            // Init button state
            UpdateBtnUI("START", startColor);
        }
    }

    private void HandleButtonClick()
    {
        switch (currentState)
        {
            case GameState.Ready:
                // --- STATE: READY TO START ---
                SendHostAction("START_GAME");
                UpdateBtnUI("PAUSE", pauseColor);
                currentState = GameState.Playing;
                Debug.Log("<color=green>[HOST]</color> Game Started!");
                break;

            case GameState.Playing:
                // --- STATE: PLAYING -> CLICK TO PAUSE ---
                SendHostAction("PAUSE_GAME");
                UpdateBtnUI("RESUME", resumeColor);
                currentState = GameState.Paused;
                Debug.Log("<color=yellow>[HOST]</color> Game Paused!");
                break;

            case GameState.Paused:
                // --- STATE: PAUSED -> CLICK TO RESUME ---
                SendHostAction("RESUME_GAME");
                UpdateBtnUI("PAUSE", pauseColor);
                currentState = GameState.Playing;
                Debug.Log("<color=cyan>[HOST]</color> Game Resumed!");
                break;
        }
    }

    // Update Button UI
    private void UpdateBtnUI(string text, Color color)
    {
        if (btnText != null) btnText.text = text;

        // Change button visual for clarity
        Image btnImg = actionBtn.GetComponent<Image>();
        if (btnImg != null) btnImg.color = color;
    }

    // Send packet to Server
    private void SendHostAction(string actionName)
    {
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.Send(new Packet
            {
                type = "HOST_ACTION",
                payload = actionName
            });
        }
        else
        {
            Debug.LogError("SocketClient not initialized!");
        }
    }
}