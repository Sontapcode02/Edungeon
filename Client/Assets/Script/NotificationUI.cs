using UnityEngine;
using TMPro;
using System.Collections;
using System;

public class NotificationUI : MonoBehaviour
{
    public static NotificationUI Instance;
    public GameObject messagePanel;
    public TextMeshProUGUI messageText;

    void Awake()
    {
        Instance = this;
        if (messagePanel != null) messagePanel.SetActive(false);
    }

    // New Countdown function
    public void StartCountdown(int seconds, Action onFinished)
    {
        StartCoroutine(CountdownCoroutine(seconds, onFinished));
    }

    [Header("Audio")]
    public AudioClip countdownSFX; // Beep SFX
    public AudioClip gameBGM;      // In-game BGM (replaces Home BGM)

    private IEnumerator CountdownCoroutine(int seconds, Action onFinished)
    {
        messagePanel.SetActive(true);

        while (seconds > 0)
        {
            messageText.text = seconds.ToString();
            // Scale up text for dramatic effect
            messageText.transform.localScale = Vector3.one * 1.5f;

            // --- AUDIO: Beep ---
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(countdownSFX);

            seconds--;
            yield return new WaitForSeconds(1f);
        }

        messageText.text = "GO!";

        // --- AUDIO: Switch BGM ---
        if (AudioManager.Instance != null && gameBGM != null)
        {
            AudioManager.Instance.PlayBGM(gameBGM);
        }

        yield return new WaitForSeconds(0.5f);

        messagePanel.SetActive(false);
        // Execute action after countdown (unlock movement)
        onFinished?.Invoke();
    }

    public void ShowMessage(string msg, bool autoHide = true)
    {
        if (messagePanel == null || messageText == null) return;
        messageText.text = msg;
        messagePanel.SetActive(true);
        if (autoHide) Invoke(nameof(HideMessage), 2f);
    }

    public void HideMessage() => messagePanel.SetActive(false);
}