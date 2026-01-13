using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // Để dùng lock

namespace GameServer
{
    public class Room
    {
        public string RoomId { get; private set; }
        public string HostId { get; private set; } // ID của chủ phòng

        private List<PlayerSession> _players = new List<PlayerSession>();

        // --- SỬA 1: Dùng đúng kiểu QuestionData ---
        private List<Question> _questions;
        private bool _isGameStarted = false;
        private int _currentQuestionIndex = 0;
        public bool IsChatMuted = false;
        public Room(string id, string hostId)
        {
            RoomId = id;
            HostId = hostId;
            // LoadQuestions có thể để ở Program.cs chạy 1 lần lúc bật server thì tốt hơn, 
            // nhưng để đây tạm cũng được.
            Console.WriteLine($"[Room {RoomId}] Created by Host: {HostId}");
        }

        public void Join(PlayerSession player)
        {
            lock (_players)
            {
                _players.Add(player);
                player.CurrentRoom = this;
                player.Score = 0;
            }

            Console.WriteLine($"[Room {RoomId}] Player {player.PlayerId} joined.");

            // Logic gửi danh sách người cũ cho người mới
            var existingPlayers = _players
                .Where(p => p != player)
                .Select(p => new PlayerState
                {
                    playerId = p.PlayerId,
                    playerName = p.PlayerName,
                    score = p.Score,
                    x = p.LastX,
                    y = p.LastY
                }).ToList();

            player.SendPacket(new Packet
            {
                type = "SYNC_PLAYERS",
                payload = JsonHelper.ToJson(existingPlayers)
            });

            // Logic báo người mới cho người cũ
            var newPlayerState = new PlayerState
            {
                playerId = player.PlayerId,
                playerName = player.PlayerName,
                x = 0,
                y = 0
            };

            // Gửi ID Host để client biết ai là trùm
            player.SendPacket(new Packet { type = "ROOM_INFO", payload = HostId });

            // Broadcast
            Broadcast(new Packet { type = "PLAYER_JOINED", payload = JsonHelper.ToJson(newPlayerState) }, null);
        }

        public void Leave(PlayerSession player)
        {
            lock (_players) _players.Remove(player);
            Console.WriteLine($"[Room {RoomId}] Player {player.PlayerId} left.");

            if (player.PlayerId == HostId)
            {
                Console.WriteLine($"[Room {RoomId}] Host left. Destroying room...");
                Broadcast(new Packet { type = "ROOM_DESTROYED", payload = "Chủ phòng đã thoát!" });

                // Giả sử class Server có biến static Rooms
                // Nếu báo lỗi dòng này, đại ca check lại file Server.cs xem biến Rooms có public static không
                Server.Rooms.TryRemove(RoomId, out _);
            }
            else
            {
                Broadcast(new Packet { type = "PLAYER_LEFT", payload = player.PlayerId });
            }
        }

        public void Broadcast(Packet packet, PlayerSession exclude = null)
        {
            lock (_players)
            {
                foreach (var p in _players)
                {
                    if (p != exclude) p.SendPacket(packet);
                }
            }
        }

        public void HandlePacket(PlayerSession sender, Packet packet)
        {
            packet.playerId = sender.PlayerId; // Bảo mật ID

            switch (packet.type)
            {
                case "MOVE":
                    var moveData = JsonHelper.FromJson<PlayerState>(packet.payload);
                    sender.LastX = moveData.x;
                    sender.LastY = moveData.y;
                    Broadcast(packet, sender);
                    break;

                case "ANSWER":
                    HandleAnswer(sender, packet.payload);
                    break;

                case "UPDATE_STATE":
                    Broadcast(packet, sender);
                    break;

                case "HOST_ACTION":
                    if (sender.PlayerId == HostId)
                    {
                        HandleHostAction(packet.payload);
                    }
                    else
                    {
                        Console.WriteLine($"[Cảnh báo] Player {sender.PlayerId} giả danh Host!");
                    }
                    break;

                    // ... Các case khác (Use Item v.v)
            }
        }

        // --- LOGIC GAME ---
        
        private void HandleAnswer(PlayerSession player, string payload)
        {
            // Cần class AnswerPayload (đã định nghĩa ở dưới cùng file này)
            var data = JsonHelper.FromJson<AnswerPayload>(payload);

            bool isCorrect = QuestionManager.CheckAnswer(data.questionId, data.answerIndex);

            if (isCorrect)
            {
                player.Score += 10;
                player.SendPacket(new Packet { type = "ANSWER_RESULT", payload = "CORRECT" });
            }
            else
            {
                player.Score = Math.Max(0, player.Score - 5);
                player.SendPacket(new Packet { type = "ANSWER_RESULT", payload = "WRONG" });
            }

            BroadcastLeaderboard();
        }

        private void HandleHostAction(string action)
        {
            Console.WriteLine($"[Room {RoomId}] Host action: {action}");

            if (action == "START_GAME")
            {
                if (!_isGameStarted) StartGame();
            }
            else if (action == "NEXT_QUESTION")
            {
                // --- SỬA 2: Logic chuyển câu hỏi ---
                _currentQuestionIndex++; // Tăng số thứ tự
                SendCurrentQuestion();   // Gửi câu mới
            }
            else if (action == "END_GAME")
            {
                EndGame();
            }
        }

        public void StartGame()
        {
            if (_isGameStarted) return;

            _isGameStarted = true;
            _currentQuestionIndex = 0;

            // Lấy 5 câu hỏi ngẫu nhiên từ QuestionManager
            _questions = QuestionManager.GetRandomQuestions(5);

            Broadcast(new Packet { type = "GAME_STARTED", payload = "" });

            // Gửi câu đầu tiên
            SendCurrentQuestion();
        }

        private void SendCurrentQuestion()
        {
            if (_questions != null && _currentQuestionIndex < _questions.Count)
            {
                var q = _questions[_currentQuestionIndex];

                // Tạo object ẩn danh để gửi JSON (Không gửi CorrectIndex)
                var questionPayload = new
                {
                    id = q.Id,
                    text = q.QuestionText,
                    answers = q.Answers,
                    timeLimit = 15
                };

                Console.WriteLine($"[Room {RoomId}] Sending Question {_currentQuestionIndex + 1}");

                Broadcast(new Packet
                {
                    type = "NEW_QUESTION",
                    payload = JsonHelper.ToJson(questionPayload)
                });
            }
            else
            {
                EndGame();
            }
        }

        // Hàm xử lý chat
        public void HandleChat(PlayerSession sender, string message)
        {
            if (IsChatMuted && sender.PlayerId != HostId) return;

            // Format: <b>Tên</b>: Nội dung
            // Đại ca nhớ đảm bảo sender.PlayerName đã được gán lúc Join/Create room nhé
            string nameColor = (sender.PlayerId == HostId) ? "red" : "blue"; // Host màu đỏ, Member màu xanh

            string finalMessage = $"<color={nameColor}><b>{sender.PlayerName}</b></color>: {message}";

            Broadcast(new Packet
            {
                type = "CHAT_RECEIVE",
                payload = finalMessage
            });
        }

        // Hàm bật/tắt chat
        public void ToggleChat(bool mute)
        {
            IsChatMuted = mute;
            // Thông báo cho tất cả client biết để disable/enable cái ô nhập liệu
            Broadcast(new Packet
            {
                type = "CHAT_STATUS",
                payload = IsChatMuted ? "MUTED" : "ACTIVE"
            });
        }

        private void EndGame()
        {
            _isGameStarted = false;
            Console.WriteLine($"[Room {RoomId}] Game Over!");
            Broadcast(new Packet { type = "END_GAME", payload = "" });
            BroadcastLeaderboard();
        }

        private void BroadcastLeaderboard()
        {
            var stats = _players.Select(p => new PlayerState
            {
                playerId = p.PlayerId,
                playerName = p.PlayerName,
                score = p.Score
            }).OrderByDescending(x => x.score).ToList();

            Broadcast(new Packet { type = "LEADERBOARD_UPDATE", payload = JsonHelper.ToJson(stats) });
        }
    }
    public class AnswerPayload
    {
        public int questionId;
        public int answerIndex;
    }


}