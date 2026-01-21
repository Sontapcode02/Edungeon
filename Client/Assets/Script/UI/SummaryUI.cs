using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Newtonsoft.Json;

public class SummaryUI : MonoBehaviour
{
    public static SummaryUI Instance;

    [Header("UI References")]
    public GameObject summaryPanel; // Drag Summary Panel here
    public Transform container;     // Drag ScrollView Content here
    public GameObject rowPrefab;    // Drag Result Row Prefab here

    void Awake()
    {
        Instance = this;
        if (summaryPanel != null) summaryPanel.SetActive(false);
    }

    public void DisplaySummary(string json)
    {
        // Must keep this function outside others, directly in class
        var list = JsonConvert.DeserializeObject<List<FinalResultData>>(json);
        summaryPanel.SetActive(true);

        // Clear old rows
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < list.Count; i++)
        {
            GameObject row = Instantiate(rowPrefab, container);
            TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();

            if (texts.Length >= 4)
            {
                texts[0].text = (i + 1).ToString();      // Rank
                texts[1].text = list[i].name;            // Name
                texts[2].text = list[i].score.ToString(); // Score
                texts[3].text = string.Format("{0:0.0}s", list[i].time); // Time
            }
        }
    }
}

// Data class to catch JSON
public class FinalResultData
{
    public string name;
    public int score;
    public float time;
}