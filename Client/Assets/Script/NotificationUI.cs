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

    // Hàm đếm ngược mới cho đại ca
    public void StartCountdown(int seconds, Action onFinished)
    {
        StartCoroutine(CountdownCoroutine(seconds, onFinished));
    }

    [Header("Audio")]
    public AudioClip countdownSFX; // Tiếng Beep
    public AudioClip gameBGM;      // Nhạc nền trong game (thay thế Home BGM)

    private IEnumerator CountdownCoroutine(int seconds, Action onFinished)
    {
        messagePanel.SetActive(true);

        while (seconds > 0)
        {
            messageText.text = seconds.ToString();
            // Phóng to chữ một chút cho kịch tính
            messageText.transform.localScale = Vector3.one * 1.5f;

            // --- AUDIO: Beep ---
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(countdownSFX);

            seconds--;
            yield return new WaitForSeconds(1f);
        }

        messageText.text = "BẮT ĐẦU!";

        // --- AUDIO: Switch BGM ---
        if (AudioManager.Instance != null && gameBGM != null)
        {
            AudioManager.Instance.PlayBGM(gameBGM);
        }

        yield return new WaitForSeconds(0.5f);

        messagePanel.SetActive(false);
        // Sau khi đếm xong thì thực hiện hành động (mở khóa di chuyển)
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