using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json; // Đại ca nhớ cài NuGet package: Newtonsoft.Json

namespace GameServer
{
    public class Room
    {
        public string RoomId { get; private set; }
        public string HostId { get; private set; }
        public string QuizFilePath { get; set; }

        private List<PlayerSession> _players = new List<PlayerSession>();
        // Lưu ý: Class Question và PlayerSession đại ca đã định nghĩa ở file khác
        private List<Question> _questions;
        private bool _isGameStarted = false;
        public bool IsChatMuted = false;

        // Dictionary lưu câu hỏi hiện tại của mỗi player (khi gặp enemy)
        private Dictionary<string, int> _playerCurrentQuestionId = new Dictionary<string, int>();

        public Room(string id, string hostId)
        {
            RoomId = id;
            HostId = hostId;
            Console.WriteLine($"[Room {RoomId}] Created by Host: {HostId}");
        }

        public void Join(PlayerSession player)
        {
            lock (_players)
            {
                _players.Add(player);
                player.CurrentRoom = this;
                player.Score = 0;
                player.CorrectAnswersCount = 0;
                player.TotalQuestionsAnswered = 0;
            }

            Console.WriteLine($"[Room {RoomId}] Player {player.PlayerId} joined.");

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

            player.Send(new Packet
            {
                type = "SYNC_PLAYERS",
                payload = JsonHelper.ToJson(existingPlayers)
            });

            var newPlayerState = new PlayerState
            {
                playerId = player.PlayerId,
                playerName = player.PlayerName,
                x = 0,
                y = 0
            };

            player.Send(new Packet { type = "ROOM_INFO", payload = HostId });
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
                QuestionManager.RemoveRoomQuizzes(RoomId);
                // Giả sử Server.Rooms là ConcurrentDictionary
                // Server.Rooms.TryRemove(RoomId, out _); 
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
                    if (p != exclude) p.Send(packet);
                }
            }
        }

        public void HandlePacket(PlayerSession sender, Packet packet)
        {
            packet.playerId = sender.PlayerId;

            switch (packet.type)
            {
                case "MOVE":
                    var moveData = JsonHelper.FromJson<PlayerState>(packet.payload);
                    sender.LastX = moveData.x;
                    sender.LastY = moveData.y;
                    Broadcast(packet, sender);
                    break;

                case "ENEMY_ENCOUNTER":
                    HandleEnemyEncounter(sender);
                    break;

                case "ANSWER":
                    HandleAnswer(sender, packet.payload);
                    break;

                case "REACH_FINISH":
                    HandleReachFinish(sender);
                    break;

                case "UPDATE_STATE":
                    Broadcast(packet, sender);
                    break;

                case "CHAT":
                    HandleChat(sender, packet.payload);
                    break;

                case "CHAT_MUTE":
                    if (sender.PlayerId == HostId)
                    {
                        bool mute = packet.payload == "true";
                        ToggleChat(mute);
                    }
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
            }
        }

        private void HandleEnemyEncounter(PlayerSession player)
        {
            Console.WriteLine($"[Room {RoomId}] Player {player.PlayerName} encountered an enemy!");

            var questions = QuestionManager.GetRoomQuizzes(RoomId);
            if (questions == null || questions.Count == 0)
            {
                player.Send(new Packet
                {
                    type = "QUIZ_ERROR",
                    payload = "Không có câu hỏi trong phòng này!"
                });
                return;
            }

            var random = new Random();
            var selectedQuestion = questions[random.Next(questions.Count)];

            _playerCurrentQuestionId[player.PlayerId] = selectedQuestion.Id;

            var questionPayload = new
            {
                id = selectedQuestion.Id,
                text = selectedQuestion.QuestionText,
                answers = selectedQuestion.Answers,
                timeLimit = 15
            };

            player.Send(new Packet
            {
                type = "NEW_QUESTION",
                payload = JsonHelper.ToJson(questionPayload)
            });

            Console.WriteLine($"[Room {RoomId}] Sent question {selectedQuestion.Id} to {player.PlayerName}");
        }

        private void HandleAnswer(PlayerSession player, string payload)
        {
            var data = JsonHelper.FromJson<AnswerPayload>(payload);

            if (!_playerCurrentQuestionId.TryGetValue(player.PlayerId, out int questionId))
            {
                player.Send(new Packet { type = "ANSWER_RESULT", payload = "ERROR" });
                return;
            }

            bool isCorrect = QuestionManager.CheckAnswer(RoomId, questionId, data.answerIndex);

            player.TotalQuestionsAnswered++;

            if (isCorrect)
            {
                player.CorrectAnswersCount++;
                player.Score += 10;
                player.Send(new Packet { type = "ANSWER_RESULT", payload = "CORRECT" });

                Console.WriteLine($"[Room {RoomId}] {player.PlayerName} answered CORRECT! " +
                    $"Progress: {player.CorrectAnswersCount}/{player.TotalQuestionsAnswered}");
            }
            else
            {
                player.Score = Math.Max(0, player.Score - 5);
                player.Send(new Packet { type = "ANSWER_RESULT", payload = "WRONG" });

                Console.WriteLine($"[Room {RoomId}] {player.PlayerName} answered WRONG! " +
                    $"Progress: {player.CorrectAnswersCount}/{player.TotalQuestionsAnswered}");
            }

            _playerCurrentQuestionId.Remove(player.PlayerId);
            BroadcastLeaderboard();
        }

        private void HandleReachFinish(PlayerSession player)
        {
            if (player.HasReachedFinish) return;

            player.HasReachedFinish = true;
            player.FinishTime = DateTime.Now;

            Console.WriteLine($"[Room {RoomId}] {player.PlayerName} reached the finish line!");

            Broadcast(new Packet
            {
                type = "PLAYER_FINISHED",
                payload = JsonHelper.ToJson(new
                {
                    playerId = player.PlayerId,
                    playerName = player.PlayerName,
                    correctAnswers = player.CorrectAnswersCount,
                    totalAnswered = player.TotalQuestionsAnswered,
                    score = player.Score
                })
            });
        }

        private void HandleHostAction(string action)
        {
            Console.WriteLine($"[Room {RoomId}] Host action: {action}");

            if (action == "START_GAME")
            {
                if (!_isGameStarted) StartGame();
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

            lock (_players)
            {
                foreach (var p in _players)
                {
                    p.CorrectAnswersCount = 0;
                    p.TotalQuestionsAnswered = 0;
                    p.HasReachedFinish = false;
                }
            }

            Broadcast(new Packet { type = "GAME_STARTED", payload = "" });
            Console.WriteLine($"[Room {RoomId}] Game started!");
        }

        private void EndGame()
        {
            _isGameStarted = false;
            Console.WriteLine($"[Room {RoomId}] Game Over!");

            lock (_players)
            {
                var finalLeaderboard = _players
                    .OrderByDescending(p => p.Score)
                    .ThenByDescending(p => p.CorrectAnswersCount)
                    .Select((p, index) => new
                    {
                        rank = index + 1,
                        playerId = p.PlayerId,
                        playerName = p.PlayerName,
                        score = p.Score,
                        correctAnswers = p.CorrectAnswersCount,
                        totalAnswered = p.TotalQuestionsAnswered,
                        accuracy = p.TotalQuestionsAnswered > 0
                            ? (p.CorrectAnswersCount * 100.0 / p.TotalQuestionsAnswered).ToString("F1") + "%"
                            : "0%"
                    })
                    .ToList();

                Broadcast(new Packet
                {
                    type = "FINAL_LEADERBOARD",
                    payload = JsonHelper.ToJson(finalLeaderboard)
                });
            }
        }

        public void HandleChat(PlayerSession sender, string message)
        {
            if (IsChatMuted && sender.PlayerId != HostId) return;

            string nameColor = (sender.PlayerId == HostId) ? "red" : "blue";
            string finalMessage = $"<color={nameColor}><b>{sender.PlayerName}</b></color>: {message}";

            Broadcast(new Packet
            {
                type = "CHAT_RECEIVE",
                payload = finalMessage
            });
        }

        public void ToggleChat(bool mute)
        {
            IsChatMuted = mute;
            Broadcast(new Packet
            {
                type = "CHAT_STATUS",
                payload = IsChatMuted ? "MUTED" : "ACTIVE"
            });
        }

        private void BroadcastLeaderboard()
        {
            var stats = _players.Select(p => new
            {
                playerId = p.PlayerId,
                playerName = p.PlayerName,
                score = p.Score,
                correctAnswers = p.CorrectAnswersCount,
                totalAnswered = p.TotalQuestionsAnswered,
                accuracy = p.TotalQuestionsAnswered > 0
                    ? (p.CorrectAnswersCount * 100.0 / p.TotalQuestionsAnswered).ToString("F1") + "%"
                    : "0%"
            })
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.correctAnswers)
            .ToList();

            Broadcast(new Packet
            {
                type = "LEADERBOARD_UPDATE",
                payload = JsonHelper.ToJson(stats)
            });
        }
    }

    public class AnswerPayload
    {
        public int questionId;
        public int answerIndex;
    }

    // EM ĐÃ THÊM CLASS NÀY VÀO ĐÂY ĐỂ FIX LỖI "JsonHelper does not exist"
    // Nếu đại ca đã có file JsonHelper.cs riêng thì xoá đoạn class này đi nhé.
    public static class JsonHelper
    {
        public static string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}