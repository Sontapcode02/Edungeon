using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Newtonsoft.Json;

public class LeaderboardUI : MonoBehaviour
{
    public Text leaderboardText;

    public void UpdateList(string jsonArray)
    {
        var list = JsonConvert.DeserializeObject<List<PlayerState>>(jsonArray);

        string display = "LEADERBOARD:\n";
        foreach (var p in list)
        {
            display += $"{p.playerId}: {p.score}\n";
        }
        leaderboardText.text = display;
    }
}

