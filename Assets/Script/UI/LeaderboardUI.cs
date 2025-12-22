using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Newtonsoft.Json;

public class LeaderboardUI : MonoBehaviour
{
    public Transform content; // Kéo cái Content trong ScrollView vào
    public GameObject rowPrefab; // T?o 1 prefab Text ho?c Row ?? hi?n tên + ?i?m

    public void UpdateList(string jsonPayload)
    {
        // Xóa c?
        foreach (Transform child in content) Destroy(child.gameObject);

        // Deserialize danh sách
        var list = JsonConvert.DeserializeObject<List<PlayerState>>(jsonPayload);

        // T?o m?i
        foreach (var p in list)
        {
            GameObject row = Instantiate(rowPrefab, content);
            // Gi? s? Prefab có component Text
            row.GetComponent<Text>().text = $"{p.name}: {p.score} ?i?m";
        }
    }
}