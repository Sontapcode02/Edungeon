using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameServer
{
    // LƯU Ý: Xóa class Question ở đây đi vì mình đã dùng chung trong GameData.cs rồi!
    // Nếu đại ca chưa thêm Id vào GameData.cs thì mở file đó ra thêm dòng: public int Id; vào nhé.

    public static class QuestionManager
    {
        // Dictionary lưu câu hỏi theo RoomID
        private static Dictionary<string, List<Question>> _quizzesByRoom = new Dictionary<string, List<Question>>();

        // Load quiz từ CSV file (Viết lại thủ công, không dùng CsvHelper)
        public static bool LoadQuizzesFromCSV(string roomId, string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[ERROR] File not found: {filePath}");
                    return false;
                }

                var questions = new List<Question>();
                int idCounter = 1; // Tạo ID tự động

                // Đọc tất cả dòng
                string[] lines = File.ReadAllLines(filePath);

                // Bắt đầu từ 1 để bỏ qua dòng tiêu đề (Header)
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Tách bằng dấu phẩy (CSV chuẩn) hoặc dấu chấm phẩy (nếu Excel lưu kiểu đó)
                    // Ở đây em dùng dấu phẩy ',' theo chuẩn CsvHelper cũ của đại ca
                    // *Lưu ý: Nếu nội dung câu hỏi có dấu phẩy thì cách split này sẽ lỗi. 
                    // Tốt nhất nên dùng dấu '|' như em khuyên, hoặc xử lý chuỗi kỹ hơn.
                    // Tạm thời em để split ',' để khớp với file cũ của đại ca.
                    string[] parts = line.Split('|');

                    if (parts.Length >= 6)
                    {
                        string qText = parts[0];
                        string aA = parts[1];
                        string aB = parts[2];
                        string aC = parts[3];
                        string aD = parts[4];
                        string correctStr = parts[5].Trim().ToUpper();

                        // Sửa cú pháp switch cho chuẩn C# 7.3 (Unity cũ chạy ngon)
                        int correctIndex = 0;
                        switch (correctStr)
                        {
                            case "A": correctIndex = 0; break;
                            case "B": correctIndex = 1; break;
                            case "C": correctIndex = 2; break;
                            case "D": correctIndex = 3; break;
                            default: correctIndex = 0; break;
                        }

                        // Tạo object Question (Dùng class từ GameData.cs)
                        Question q = new Question();
                        q.Id = idCounter++; // Nếu GameData.cs chưa có Id thì tạm bỏ qua dòng này hoặc thêm vào
                        q.QuestionText = qText;
                        q.Answers = new string[] { aA, aB, aC, aD };
                        q.CorrectIndex = correctIndex;
                        q.TimeLimit = 15; // Mặc định

                        questions.Add(q);
                    }
                }

                if (questions.Count > 0)
                {
                    // Nếu room đã có thì update, chưa có thì thêm mới
                    if (_quizzesByRoom.ContainsKey(roomId))
                        _quizzesByRoom[roomId] = questions;
                    else
                        _quizzesByRoom.Add(roomId, questions);

                    Console.WriteLine($"[Room {roomId}] Loaded {questions.Count} questions.");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load CSV: {ex.Message}");
                return false;
            }
        }

        public static List<Question> GetRoomQuizzes(string roomId)
        {
            if (_quizzesByRoom.TryGetValue(roomId, out var questions))
            {
                return questions;
            }
            return new List<Question>();
        }

        public static Question GetRandomQuestion(string roomId)
        {
            var questions = GetRoomQuizzes(roomId);
            if (questions.Count == 0) return null;

            var random = new Random();
            return questions[random.Next(questions.Count)];
        }

        // Check đáp án (Giờ check theo Index cho dễ)
        public static bool CheckAnswer(string roomId, int questionIndexInList, int answerIndex)
        {
            var questions = GetRoomQuizzes(roomId);
            if (questionIndexInList < 0 || questionIndexInList >= questions.Count) return false;

            return questions[questionIndexInList].CorrectIndex == answerIndex;
        }

        public static void RemoveRoomQuizzes(string roomId)
        {
            if (_quizzesByRoom.ContainsKey(roomId))
                _quizzesByRoom.Remove(roomId);
        }
    }
}