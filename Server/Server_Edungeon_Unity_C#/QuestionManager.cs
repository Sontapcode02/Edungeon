using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer
{
    // 1. Định nghĩa Class Question chuẩn khớp với Room.cs
    public class Question
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } // Room.cs đang đòi cái này
        public string[] Answers { get; set; }    // Room.cs đang đòi cái này
        public int CorrectIndex { get; set; }    // 0: A, 1: B, 2: C, 3: D
    }

    // 2. Class quản lý câu hỏi
    public static class QuestionManager
    {
        // Danh sách câu hỏi lưu trong RAM
        private static List<Question> _allQuestions = new List<Question>();

        // Hàm giả lập load câu hỏi (Sau này đại ca thay bằng đọc file JSON/DB)
        public static void LoadQuestions()
        {
            _allQuestions.Clear();
            _allQuestions.Add(new Question
            {
                Id = 1,
                QuestionText = "Thủ đô của Việt Nam là gì?",
                Answers = new[] { "HCM", "Hà Nội", "Đà Nẵng", "Cần Thơ" },
                CorrectIndex = 1
            });

            _allQuestions.Add(new Question
            {
                Id = 2,
                QuestionText = "1 + 1 bằng mấy?",
                Answers = new[] { "1", "2", "3", "4" },
                CorrectIndex = 1
            });

            _allQuestions.Add(new Question
            {
                Id = 3,
                QuestionText = "Ai là người tạo ra C#?",
                Answers = new[] { "Bill Gates", "Anders Hejlsberg", "Elon Musk", "Steve Jobs" },
                CorrectIndex = 1
            });

            _allQuestions.Add(new Question
            {
                Id = 4,
                QuestionText = "Mặt trời mọc hướng nào?",
                Answers = new[] { "Tây", "Nam", "Bắc", "Đông" },
                CorrectIndex = 3
            });

            _allQuestions.Add(new Question
            {
                Id = 5,
                QuestionText = "Con gì có 4 chân?",
                Answers = new[] { "Gà", "Vịt", "Chó", "Cá" },
                CorrectIndex = 2
            });

            Console.WriteLine($"[System] Loaded {_allQuestions.Count} questions.");
        }

        // --- HÀM MÀ ROOM.CS ĐANG BÁO LỖI THIẾU ĐÂY ---
        public static List<Question> GetRandomQuestions(int count)
        {
            // Nếu chưa load thì load data mẫu
            if (_allQuestions.Count == 0) LoadQuestions();

            // Lấy ngẫu nhiên 'count' câu hỏi (Xáo trộn list rồi lấy)
            Random rnd = new Random();
            return _allQuestions.OrderBy(x => rnd.Next()).Take(count).ToList();
        }

        // Hàm check đáp án
        public static bool CheckAnswer(int questionId, int answerIndex)
        {
            var q = _allQuestions.FirstOrDefault(x => x.Id == questionId);
            if (q == null) return false;
            return q.CorrectIndex == answerIndex;
        }
    }
}