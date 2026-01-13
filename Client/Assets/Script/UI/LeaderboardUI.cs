using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    [Header("Leaderboard")]
    public GameObject leaderboardPanel;
    public Transform playerListContainer;
    public GameObject playerRowPrefab;

    private Dictionary<string, Transform> playerRows = new Dictionary<string, Transform>();

    void Start()
    {
        leaderboardPanel.SetActive(true);
    }

    public void UpdateList(string jsonPayload)
    {
                try
        {
            List<PlayerProgress> players = JsonConvert.DeserializeObject<List<PlayerProgress>>(jsonPayload);

            foreach (var player in players)
            {
                UpdatePlayerRow(player);
            }

            // Sắp xếp theo thứ hạng
            SortLeaderboard();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LeaderboardUI] Lỗi parse leaderboard: {ex.Message}");
        }
    }

    void UpdatePlayerRow(PlayerProgress progress)
    {
        if (!playerRows.ContainsKey(progress.playerId))
        {
            GameObject newRow = Instantiate(playerRowPrefab, playerListContainer);
            playerRows[progress.playerId] = newRow.transform;
        }

        Transform row = playerRows[progress.playerId];
        TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length >= 3)
        {
            texts[0].text = progress.playerName;
            texts[1].text = $"{progress.progressPercentage:F1}%";
            texts[2].text = $"Score: {progress.score}";
        }

        // Đổi màu nếu người chơi đã chết
        Image bgImage = row.GetComponent<Image>();
        if (bgImage && !progress.isAlive)
        {
            bgImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }

    void SortLeaderboard()
    {
        List<Transform> rows = new List<Transform>();
        foreach (var kvp in playerRows)
        {
            rows.Add(kvp.Value);
        }

        rows.Sort((a, b) =>
        {
            TextMeshProUGUI[] textsA = a.GetComponentsInChildren<TextMeshProUGUI>();
            TextMeshProUGUI[] textsB = b.GetComponentsInChildren<TextMeshProUGUI>();

            if (textsA.Length >= 2 && textsB.Length >= 2)
            {
                float progressA = float.Parse(textsA[1].text.Replace("%", ""));
                float progressB = float.Parse(textsB[1].text.Replace("%", ""));
                return progressB.CompareTo(progressA); // Giảm dần
            }
            return 0;
        });

        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].SetAsLastSibling();
        }
    }
}