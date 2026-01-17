using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Newtonsoft.Json;

public class SummaryUI : MonoBehaviour
{
    public static SummaryUI Instance;

    [Header("UI References")]
    public GameObject summaryPanel; // Kéo cái Panel tổng kết vào đây
    public Transform container;     // Kéo cái Content của ScrollView vào đây
    public GameObject rowPrefab;    // Kéo cái Prefab dòng kết quả vào đây

    void Awake()
    {
        Instance = this;
        if (summaryPanel != null) summaryPanel.SetActive(false);
    }

    public void DisplaySummary(string json)
    {
        // Phải để hàm này ở ngoài các hàm khác, nằm trực tiếp trong class
        var list = JsonConvert.DeserializeObject<List<FinalResultData>>(json);
        summaryPanel.SetActive(true);

        // Xóa các dòng cũ
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
                texts[0].text = (i + 1).ToString();      // Hạng
                texts[1].text = list[i].name;            // Tên
                texts[2].text = list[i].score.ToString(); // Điểm
                texts[3].text = string.Format("{0:0.0}s", list[i].time); // Thời gian
            }
        }
    }
}

// Class dữ liệu để hứng JSON
public class FinalResultData
{
    public string name;
    public int score;
    public float time;
}