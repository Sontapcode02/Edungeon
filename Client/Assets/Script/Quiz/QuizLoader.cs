using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class QuizLoader : MonoBehaviour
{
    // Hàm đọc file CSV thủ công (Thay thế CsvHelper)
    public static List<Question> LoadCSV(string filePath)
    {
        List<Question> result = new List<Question>();

        if (!File.Exists(filePath))
        {
            Debug.LogError("Không tìm thấy file: " + filePath);
            return result;
        }

        // Đọc hết các dòng
        string[] lines = File.ReadAllLines(filePath);

        // Bỏ qua dòng tiêu đề (nếu dòng 1 là header)
        int startIdx = 0;

        for (int i = startIdx; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line)) continue;

            // Tách bằng dấu gạch đứng '|' (như đã bàn ở các tin nhắn trước)
            string[] parts = line.Split('|');

            // Kiểm tra xem có đủ dữ liệu không (Câu hỏi + 4 đáp án + 1 đáp án đúng = 6 cột)
            if (parts.Length >= 6)
            {
                Question q = new Question();
                q.QuestionText = parts[0];
                q.Answers = new string[]
                {
                    parts[1], parts[2], parts[3], parts[4]
                };

                // Parse số an toàn (tránh crash nếu nhập chữ vào ô số)
                int correctIdx = 0;
                int.TryParse(parts[5], out correctIdx);
                q.CorrectIndex = correctIdx;

                // Mặc định 15s nếu không nhập cột thời gian
                q.TimeLimit = 15;

                result.Add(q);
            }
        }

        Debug.Log($"Đã load thành công {result.Count} câu hỏi.");
        return result;
    }
}