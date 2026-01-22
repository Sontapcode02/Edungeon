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
            List<PlayerProgress> players = JsonConvert.DeserializeObject<List<PlayerProgress>>(jsonPayload);

            if (players == null) return;

            // [OPTIMIZATION] Server already sorts High -> Low.
            // We just need to ensure the UI hierarchy matches this list order.

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                UpdatePlayerRow(player, i);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LeaderboardUI] Leaderboard parse error: {ex.Message}");
        }
    }

    void UpdatePlayerRow(PlayerProgress progress, int siblingIndex)
    {
        // --- 🎯 [FIX] EXCLUDE HOST FROM LIST ---
        if (string.IsNullOrEmpty(progress.playerId) || progress.playerId.StartsWith("Host_"))
        {
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

        // [FIX] Enforce Order: SetSiblingIndex ensures the UI follows the Server's List order
        row.SetSiblingIndex(siblingIndex);

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



}
