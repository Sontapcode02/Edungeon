using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    // Singleton for easy access from MessageHandler
    public static LeaderboardUI Instance;

    [Header("Leaderboard")]
    public GameObject leaderboardPanel;
    public Transform playerListContainer;
    public GameObject playerRowPrefab;

    private Dictionary<string, Transform> playerRows = new Dictionary<string, Transform>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Ensure panel is always active to receive data
        if (leaderboardPanel) leaderboardPanel.SetActive(true);
    }

    public void UpdateList(string jsonPayload)
    {
        try
        {
            // [FIX] Server sends SYNC_PLAYERS as List<PlayerState>, not PlayerProgress
            // using List<dynamic> or List<PlayerProgress> depending on definition
            List<PlayerProgress> players = JsonConvert.DeserializeObject<List<PlayerProgress>>(jsonPayload);

            if (players == null) return;

            foreach (var player in players)
            {
                UpdatePlayerRow(player);
            }

            SortLeaderboard();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LeaderboardUI] Leaderboard parse error: {ex.Message}");
        }
    }

    void UpdatePlayerRow(PlayerProgress progress)
    {
        // --- 🎯 [FIX] EXCLUDE HOST FROM LIST ---
        if (string.IsNullOrEmpty(progress.playerId) || progress.playerId.StartsWith("Host_"))
        {
            // If Host, remove old row (if exists) and return
            if (playerRows.ContainsKey(progress.playerId))
            {
                Destroy(playerRows[progress.playerId].gameObject);
                playerRows.Remove(progress.playerId);
            }
            return;
        }

        // Create new row if not exists
        if (!playerRows.ContainsKey(progress.playerId))
        {
            GameObject newRow = Instantiate(playerRowPrefab, playerListContainer);
            playerRows[progress.playerId] = newRow.transform;
        }

        Transform row = playerRows[progress.playerId];
        TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();

        // Fill data into columns (Name - Progress - Score)
        if (texts.Length >= 3)
        {
            texts[0].text = progress.playerName;
            texts[1].text = $"{progress.progressPercentage:F1}%";
            texts[2].text = $"{progress.score}";
        }

        // Change color if player is dead
        Image bgImage = row.GetComponent<Image>();
        if (bgImage)
        {
            bgImage.color = progress.isAlive ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }

    void SortLeaderboard()
    {
        // 1. Convert Dictionary to List for sorting
        List<Transform> rows = new List<Transform>(playerRows.Values);

        // 2. Sort list
        rows.Sort((a, b) =>
        {
            // Get Progress Text (Assuming index 1)
            TextMeshProUGUI[] textsA = a.GetComponentsInChildren<TextMeshProUGUI>();
            TextMeshProUGUI[] textsB = b.GetComponentsInChildren<TextMeshProUGUI>();

            if (textsA.Length >= 2 && textsB.Length >= 2)
            {
                // Extract number from string (e.g. "Progress: 5 questions" -> get 5)
                float valA = ExtractNumber(textsA[1].text);
                float valB = ExtractNumber(textsB[1].text);

                // Sort descending
                return valB.CompareTo(valA);
            }
            return 0;
        });

        // 3. Reset hierarchy position (Sibling Index)
        for (int i = 0; i < rows.Count; i++)
        {
            // SetAsLastSibling pushes item to the end of Layout list
            // Since 'rows' is sorted High -> Low, the lowest will be pushed to bottom.
            rows[i].SetAsLastSibling();
        }
    }

    // Helper function to extract number from text string
    float ExtractNumber(string text)
    {
        string numericPart = System.Text.RegularExpressions.Regex.Match(text, @"\d+").Value;
        return float.TryParse(numericPart, out float result) ? result : 0;
    }
}

