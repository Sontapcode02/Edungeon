using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace GameServer
{
    public class Room
    {
        public string RoomId { get; private set; }
        public string HostId { get; private set; }

        // Dictionary lưu người chơi
        public Dictionary<string, PlayerSession> Players = new Dictionary<string, PlayerSession>();

        // Danh sách câu hỏi
        public List<QuestionData> Questions { get; set; } = new List<QuestionData>();

        public bool IsChatMuted { get; private set; } = false;
        public DateTime StartTime { get; set; } // Thời điểm Host bấm Start
        public bool IsGameStarted { get; set; } = false;
        private DateTime? pauseStartTime;
        public Room(string roomId, string hostId)
        {
            RoomId = roomId;
            HostId = hostId;
        }

        // --- QUẢN LÝ CÂU HỎI ---
        public QuestionData GetRandomQuestion()
        {
            if (Questions == null || Questions.Count == 0) return null;
            Random rnd = new Random();
            return Questions[rnd.Next(Questions.Count)];
        }

        public bool CheckAnswer(int questionId, int answerIndex)
        {
            var q = Questions.Find(x => x.id == questionId);
            if (q == null) return false;
            return q.correctIndex == answerIndex;
        }
        // -----------------------

        public void Join(PlayerSession session)
        {
            if (!Players.ContainsKey(session.PlayerId))
            {
                Players.Add(session.PlayerId, session);
                session.CurrentRoom = this;

                // --- [FIX LỖI JSON Ở ĐÂY] ---
                // Thay vì gửi mỗi cái tên, ta gửi cả cục PlayerState (JSON)
                PlayerState newState = new PlayerState
                {
                    playerId = session.PlayerId,
                    playerName = session.PlayerName,
                    x = 0, // Vị trí mặc định
                    y = 0,
                    score = 0
                };

                string jsonState = JsonConvert.SerializeObject(newState);

                // Gửi cho tất cả người cũ biết có người mới vào
                Broadcast(new Packet
                {
                    type = "PLAYER_JOINED",
                    payload = jsonState // Gửi JSON chuẩn
                });

                // Gửi danh sách người chơi hiện tại cho người mới
                SyncPlayers(session); // Gửi riêng cho người mới

                Console.WriteLine($"[Room {RoomId}] {session.PlayerName} joined.");
            }
        }

        public void Leave(PlayerSession session)
        {
            if (Players.ContainsKey(session.PlayerId))
            {
                Players.Remove(session.PlayerId);
                session.CurrentRoom = null;

                // Gửi ID người thoát để Client xóa nhân vật
                Broadcast(new Packet
                {
                    type = "PLAYER_LEFT",
                    payload = session.PlayerId // Client mong chờ ID (string), cái này OK
                });

                if (session.PlayerId == HostId)
                {
                    CloseRoom();
                }
            }
        }

        public void CloseRoom()
        {
            foreach (var player in Players.Values)
            {
                player.CurrentRoom = null;
                player.Send(new Packet { type = "ROOM_DESTROYED", payload = "Host đã thoát." });
            }
            Players.Clear();
            Questions.Clear();
            Server.Rooms.TryRemove(RoomId, out _);
            Console.WriteLine($"[Room {RoomId}] Closed.");
        }

        public void ToggleChat(bool isMuted)
        {
            IsChatMuted = isMuted;
            string msg = isMuted ? "MUTED" : "UNMUTED";
            Broadcast(new Packet { type = "CHAT_STATUS", payload = msg });
        }

        public void HandleChat(PlayerSession sender, string message)
        {
            if (IsChatMuted && sender.PlayerId != HostId)
            {
                sender.Send(new Packet { type = "ERROR", payload = "Chat đang bị khóa!" });
                return;
            }
            string fullMsg = $"{sender.PlayerName}: {message}";
            Broadcast(new Packet { type = "CHAT_RECEIVE", payload = fullMsg });
        }

        public void Broadcast(Packet packet)
        {
            foreach (var player in Players.Values)
            {
                player.Send(packet);
            }
        }

        public void HandlePacket(PlayerSession session, Packet packet)
        {
            switch (packet.type)
            {
                case "MOVE":
                    try { 
                    // Broadcast vị trí JSON cho người khác
                    Broadcast(new Packet
                    {
                        type = "MOVE",
                        playerId = session.PlayerId,
                        payload = packet.payload // Payload này từ Client gửi lên đã là JSON PlayerState rồi
                    });
                        } catch (Exception ex)
                    {
                        Console.WriteLine($"[Room {RoomId}] Lỗi xử lý MOVE: {ex.Message}");
                    }
                    break;


                case "REQUEST_QUESTION":
                    try
                    {
                        string monsterId = packet.payload;
                        if (session.CompletedMilestones.Contains(monsterId.GetHashCode())) // Ép về int để khớp với HashSet<int>
                        {
                            session.Send(new Packet { type = "ERROR", payload = "Quái này đại ca đã đánh bại rồi!" });
                            return;
                        }
                        if (session.CurrentQuestionIndex < this.Questions.Count)
                        {
                            var question = this.Questions[session.CurrentQuestionIndex];
                            session.Send(new Packet { type = "NEW_QUESTION", payload = JsonConvert.SerializeObject(question) });
                        }
                        else
                        {
                            session.Send(new Packet { type = "OUT_OF_QUESTIONS", payload = "Đại ca đã phá đảo tất cả câu hỏi!" });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Nếu lỗi xảy ra, in ra console thay vì để luồng xử lý bị chết
                        Console.WriteLine($"❌ [CRITICAL] Lỗi REQUEST_QUESTION của {session.PlayerName}: {ex.Message}");
                    }
                    break;

                case "ANSWER":
                    try
                    {
                        var data = JsonConvert.DeserializeObject<dynamic>(packet.payload);
                        int selectedIndex = (int)data.answerIndex;
                        string mId = (string)data.monsterId;

                        var currentQ = this.Questions[session.CurrentQuestionIndex];

                        // --- DÙ ĐÚNG HAY SAI CŨNG LƯU MILESTONE ---
                        session.CompletedMilestones.Add(mId.GetHashCode());
                        session.CurrentQuestionIndex++; // Tăng index để lần sau ra câu tiếp theo

                        if (selectedIndex == currentQ.correctIndex)
                        {
                            session.Score += 10;
                            session.Send(new Packet { type = "ANSWER_RESULT", payload = "CHÍNH XÁC!" });

                        }
                        else
                        {
                            session.Send(new Packet { type = "ANSWER_RESULT", payload = "SAI RỒI!" });
                            // Vẫn gửi gói tin này để Client biết mà làm mờ con quái
                        }
                        BroadcastLeaderboard();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Lỗi ANSWER: {ex.Message}");
                    }
                    break;
                case "REACHED_FINISH":
                    if (!session.HasReachedFinish)
                    {
                        session.HasReachedFinish = true;
                        TimeSpan elapsed = DateTime.Now - this.StartTime;
                        session.FinishTime = (float)elapsed.TotalSeconds;

                        Console.WriteLine($"[FINISH] {session.PlayerName} về đích: {session.FinishTime:F2}s");

                        // 1. Cập nhật Leaderboard ngay lập tức
                        BroadcastLeaderboard();

                        // 2. Kiểm tra xem tất cả các Player (không tính Host) đã về đích hết chưa
                        bool allFinished = Players.Values
                            .Where(p => p.PlayerId != HostId)
                            .All(p => p.HasReachedFinish);

                        if (allFinished)
                        {
                            Console.WriteLine("🏁 Tất cả đã về đích! Đang gửi bảng tổng kết...");
                            SendFinalSummary();
                        }
                    }
                    break;


                case "PAUSE_GAME":
                    pauseStartTime = DateTime.Now;
                    Broadcast(new Packet { type = "GAME_PAUSED", payload = "Trận đấu tạm dừng!" });
                    break;

                case "RESUME_GAME":
                    if (pauseStartTime.HasValue)
                     {
                        TimeSpan pauseDuration = DateTime.Now - pauseStartTime.Value;
                        this.StartTime = this.StartTime.Add(pauseDuration);
                        pauseStartTime = null; // Reset lại
                     }
                    Broadcast(new Packet { type = "GAME_RESUMED", payload = "Tiếp tục đua nào!" });
                    break;
               }
        }

        private void SendFinalSummary()
        {
            // Sắp xếp: Score Cao -> Time Thấp
            var finalData = Players.Values
                .Where(p => p.PlayerId != HostId)
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.FinishTime)
                .Select(p => new {
                    name = p.PlayerName,
                    score = p.Score,
                    time = p.FinishTime
                }).ToList();

            string json = JsonConvert.SerializeObject(finalData);
            Broadcast(new Packet { type = "GAME_OVER_SUMMARY", payload = json });

            // 3. Đợi 10 giây rồi đá người chơi ra (trừ Host)
            Task.Delay(10000).ContinueWith(t => KickPlayersToHome());
        }

        private void KickPlayersToHome()
        {
            // Copy danh sách ra một mảng tạm để tránh lỗi "Collection was modified"
            var playersToKick = Players.Values.Where(p => p.PlayerId != HostId).ToList();

            foreach (var player in playersToKick)
            {
                player.Send(new Packet { type = "RETURN_TO_HOME", payload = "Game kết thúc!" });
                // Không nên gọi Leave(player) ở đây ngay, hãy để Client tự thoát khi nhận lệnh
            }
            Console.WriteLine("🔔 Đã đá tất cả người chơi về Home (Trừ Host).");
        }
        public void BroadcastLeaderboard()
        {
            // Sắp xếp danh sách người chơi (loại bỏ Host)
            var rankedPlayers = Players.Values
                .Where(p => p.PlayerId != HostId)
                .OrderByDescending(p => p.Score)        // 1. Ưu tiên Score cao
                .ThenBy(p => p.FinishTime)              // 2. Bằng Score thì ưu tiên thời gian ít (nhanh hơn)
                .ToList();

            List<PlayerProgress> progressList = new List<PlayerProgress>();

            for (int i = 0; i < rankedPlayers.Count; i++)
            {
                var p = rankedPlayers[i];
                float percent = (Questions.Count > 0) ? (float)p.CurrentQuestionIndex / Questions.Count * 100f : 0;

                progressList.Add(new PlayerProgress
                {
                    playerId = p.PlayerId,
                    playerName = p.PlayerName,
                    score = p.Score,
                    progressPercentage = percent,
                    // Đại ca có thể dùng isAlive làm cờ báo "Đã về đích" để UI đổi màu
                    isAlive = !p.HasReachedFinish
                });
            }

            string json = JsonConvert.SerializeObject(progressList);
            Broadcast(new Packet { type = "PROGRESS_UPDATE", payload = json });
        }

        // --- [FIX LỖI JSON Ở ĐÂY] ---
        // Hàm đồng bộ danh sách người chơi
        private void SyncPlayers(PlayerSession newSession)
        {
            // Tạo danh sách các PlayerState
            List<PlayerState> states = new List<PlayerState>();

            foreach (var p in Players.Values)
            {
                states.Add(new PlayerState
                {
                    playerId = p.PlayerId,
                    playerName = p.PlayerName,
                    x = 0,
                    y = 0
                });
            }

            string jsonList = JsonConvert.SerializeObject(states);

            // Gửi riêng cho người mới vào
            newSession.Send(new Packet { type = "SYNC_PLAYERS", payload = jsonList });
        }
    }
}