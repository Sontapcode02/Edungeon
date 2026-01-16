using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    // Tạo Singleton để MessageHandler gọi dễ dàng
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
        // Đảm bảo panel luôn bật để nhận dữ liệu
        if (leaderboardPanel) leaderboardPanel.SetActive(true);
    }

    public void UpdateList(string jsonPayload)
    {
        try
        {
            // [FIX] Server gửi SYNC_PLAYERS là List<PlayerState>, không phải PlayerProgress
            // Ta dùng kiểu List<dynamic> hoặc List<PlayerProgress> tùy theo class đại ca định nghĩa
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
            Debug.LogError($"[LeaderboardUI] Lỗi parse leaderboard: {ex.Message}");
        }
    }

    void UpdatePlayerRow(PlayerProgress progress)
    {
        // --- 🎯 [FIX] LOẠI BỎ HOST KHỎI DANH SÁCH ---
        if (string.IsNullOrEmpty(progress.playerId) || progress.playerId.StartsWith("Host_"))
        {
            // Nếu là Host thì xóa dòng cũ (nếu lỡ có) và thoát
            if (playerRows.ContainsKey(progress.playerId))
            {
                Destroy(playerRows[progress.playerId].gameObject);
                playerRows.Remove(progress.playerId);
            }
            return;
        }

        // Tạo dòng mới nếu chưa có
        if (!playerRows.ContainsKey(progress.playerId))
        {
            GameObject newRow = Instantiate(playerRowPrefab, playerListContainer);
            playerRows[progress.playerId] = newRow.transform;
        }

        Transform row = playerRows[progress.playerId];
        TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();

        // Đổ dữ liệu vào các cột (Tên - Tiến độ - Điểm)
        if (texts.Length >= 3)
        {
            texts[0].text = progress.playerName;
            texts[1].text = $"{progress.progressPercentage:F1}%";
            texts[2].text = $"{progress.score}";
        }

        // Đổi màu nếu người chơi đã chết
        Image bgImage = row.GetComponent<Image>();
        if (bgImage)
        {
            bgImage.color = progress.isAlive ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }

    void SortLeaderboard()
    {
        // 1. Chuyển Dictionary thành một danh sách để dễ sắp xếp
        List<Transform> rows = new List<Transform>(playerRows.Values);

        // 2. Sắp xếp danh sách
        rows.Sort((a, b) =>
        {
            // Lấy Text hiển thị tiến độ (giả sử là ô thứ 2 - index 1)
            TextMeshProUGUI[] textsA = a.GetComponentsInChildren<TextMeshProUGUI>();
            TextMeshProUGUI[] textsB = b.GetComponentsInChildren<TextMeshProUGUI>();

            if (textsA.Length >= 2 && textsB.Length >= 2)
            {
                // Trích xuất con số từ chuỗi (ví dụ: "Tiến độ: 5 câu" -> lấy số 5)
                float valA = ExtractNumber(textsA[1].text);
                float valB = ExtractNumber(textsB[1].text);

                // Sắp xếp giảm dần (ông nào lớn hơn thì trả về kết quả nhỏ hơn để lên đầu)
                return valB.CompareTo(valA);
            }
            return 0;
        });

        // 3. Thiết lập lại vị trí trong Hierarchy (Sibling Index)
        for (int i = 0; i < rows.Count; i++)
        {
            // SetAsLastSibling sẽ đẩy thằng lần lượt vào cuối danh sách của Layout
            // Vì danh sách 'rows' đã xếp từ Cao -> Thấp, nên thằng thấp nhất sẽ được đẩy xuống cuối cùng.
            rows[i].SetAsLastSibling();
        }
    }

    // Hàm phụ để tách lấy số từ chuỗi văn bản (đề phòng đại ca đổi format hiển thị)
    float ExtractNumber(string text)
    {
        string numericPart = System.Text.RegularExpressions.Regex.Match(text, @"\d+").Value;
        return float.TryParse(numericPart, out float result) ? result : 0;
    }
}

