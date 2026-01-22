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

        // Dictionary storing players
        public Dictionary<string, PlayerSession> Players = new Dictionary<string, PlayerSession>();

        // List of questions
        public List<QuestionData> Questions { get; set; } = new List<QuestionData>();

        public bool IsChatMuted { get; private set; } = false;
        public DateTime StartTime { get; set; } // Time when Host starts the game
        public bool IsGameStarted { get; set; } = false;
        private DateTime? pauseStartTime;
        public Room(string roomId, string hostId)
        {
            RoomId = roomId;
            HostId = hostId;
        }

        // --- QUESTION MANAGEMENT ---
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

                // --- [FIX JSON ERROR HERE] ---
                // Instead of sending just the name, send the full PlayerState (JSON)
                PlayerState newState = new PlayerState
                {
                    playerId = session.PlayerId,
                    playerName = session.PlayerName,
                    x = 0, // Default position
                    y = 0,
                    score = 0
                };

                string jsonState = JsonConvert.SerializeObject(newState);

                // Broadcast to existing players that a new player joined
                Broadcast(new Packet
                {
                    type = "PLAYER_JOINED",
                    payload = jsonState // Send standard JSON
                });

                // Send current player list to the new player
                SyncPlayers(session); // Send specifically to new player

                Console.WriteLine($"[Room {RoomId}] {session.PlayerName} joined.");
            }
        }

        public void Leave(PlayerSession session)
        {
            if (Players.ContainsKey(session.PlayerId))
            {
                Players.Remove(session.PlayerId);
                session.CurrentRoom = null;

                // Send ID of leaver so Client can remove character
                Broadcast(new Packet
                {
                    type = "PLAYER_LEFT",
                    payload = session.PlayerId // Client expects ID (string)
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
                player.Send(new Packet { type = "ROOM_DESTROYED", payload = "Host has left." });
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
                sender.Send(new Packet { type = "ERROR", payload = "Chat is muted!" });
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
                    try
                    {
                        // 1. Deserialize to update Server state
                        var moveState = JsonConvert.DeserializeObject<PlayerState>(packet.payload);
                        session.LastX = moveState.x;
                        session.LastY = moveState.y;

                        // 2. Broadcast position JSON to others
                        // [DEBUG] Track movement on Server
                        // Console.WriteLine($"[Room {RoomId}] {session.PlayerName} moved to ({moveState.x}, {moveState.y})");

                        Broadcast(new Packet
                        {
                            type = "MOVE",
                            playerId = session.PlayerId,
                            payload = packet.payload
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Room {RoomId}] MOVE Error: {ex.Message}");
                    }
                    break;


                case "REQUEST_QUESTION":
                    try
                    {
                        string monsterId = packet.payload;
                        if (session.CompletedMilestones.Contains(monsterId.GetHashCode())) // Cast to int to match HashSet<int>
                        {
                            session.Send(new Packet { type = "ERROR", payload = "You have already defeated this monster!" });
                            return;
                        }
                        if (session.CurrentQuestionIndex < this.Questions.Count)
                        {
                            var question = this.Questions[session.CurrentQuestionIndex];
                            session.Send(new Packet { type = "NEW_QUESTION", payload = JsonConvert.SerializeObject(question) });
                        }
                        else
                        {
                            session.Send(new Packet { type = "OUT_OF_QUESTIONS", payload = "You have answered all questions!" });
                        }
                    }
                    catch (Exception ex)
                    {
                        // If error occurs, print to console instead of crashing the thread
                        Console.WriteLine($"❌ [CRITICAL] WARNING REQUEST_QUESTION from {session.PlayerName}: {ex.Message}");
                    }
                    break;

                case "ANSWER":
                    try
                    {
                        var data = JsonConvert.DeserializeObject<dynamic>(packet.payload);
                        int selectedIndex = (int)data.answerIndex;
                        string mId = (string)data.monsterId;

                        var currentQ = this.Questions[session.CurrentQuestionIndex];

                        // --- SAVE MILESTONE REGARDLESS OF ACCURACY ---
                        session.CompletedMilestones.Add(mId.GetHashCode());
                        session.CurrentQuestionIndex++; // Increment index for next question

                        if (selectedIndex == currentQ.correctIndex)
                        {
                            session.Score += 10;
                            session.Send(new Packet { type = "ANSWER_RESULT", payload = "CORRECT!" });

                        }
                        else
                        {
                            session.Send(new Packet { type = "ANSWER_RESULT", payload = "WRONG!" });
                            // Still send packet so Client knows to fade the monster
                        }
                        BroadcastLeaderboard();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ ANSWER Error: {ex.Message}");
                    }
                    break;
                case "REACHED_FINISH":
                    if (!session.HasReachedFinish)
                    {
                        session.HasReachedFinish = true;
                        TimeSpan elapsed = DateTime.Now - this.StartTime;
                        session.FinishTime = (float)elapsed.TotalSeconds;

                        Console.WriteLine($"[FINISH] {session.PlayerName} reached finish line: {session.FinishTime:F2}s");

                        // 1. Update Leaderboard immediately
                        BroadcastLeaderboard();

                        // 2. Check if all Players (excluding Host) have finished
                        bool allFinished = Players.Values
                            .Where(p => p.PlayerId != HostId)
                            .All(p => p.HasReachedFinish);

                        if (allFinished)
                        {
                            Console.WriteLine("🏁 All finished! Sending summary...");
                            SendFinalSummary();
                        }
                    }
                    break;


                case "PAUSE_GAME":
                    pauseStartTime = DateTime.Now;
                    Broadcast(new Packet { type = "GAME_PAUSED", payload = "Game Paused!" });
                    break;

                case "RESUME_GAME":
                    if (pauseStartTime.HasValue)
                    {
                        TimeSpan pauseDuration = DateTime.Now - pauseStartTime.Value;
                        this.StartTime = this.StartTime.Add(pauseDuration);
                        pauseStartTime = null; // Reset
                    }
                    Broadcast(new Packet { type = "GAME_RESUMED", payload = "Game Resumed!" });
                    break;
            }
        }

        private void SendFinalSummary()
        {
            // Sort: High Score -> Low Time
            var finalData = Players.Values
                .Where(p => p.PlayerId != HostId)
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.FinishTime)
                .Select(p => new
                {
                    name = p.PlayerName,
                    score = p.Score,
                    time = p.FinishTime
                }).ToList();

            string json = JsonConvert.SerializeObject(finalData);
            Broadcast(new Packet { type = "GAME_OVER_SUMMARY", payload = json });

            // 3. Wait 10 seconds then kick players (except Host)
            Task.Delay(10000).ContinueWith(t => KickPlayersToHome());
        }

        private void KickPlayersToHome()
        {
            // Copy list to temporary array to avoid "Collection was modified" error
            var playersToKick = Players.Values.Where(p => p.PlayerId != HostId).ToList();

            foreach (var player in playersToKick)
            {
                player.Send(new Packet { type = "RETURN_TO_HOME", payload = "Game Over!" });
                // Should not call Leave(player) here immediately, let Client disconnect itself
            }
            Console.WriteLine("🔔 Kicked all players to Home (except Host).");
        }
        public void BroadcastLeaderboard()
        {
            // Sort player list (exclude Host)
            var rankedPlayers = Players.Values
                .Where(p => p.PlayerId != HostId)
                .OrderByDescending(p => p.Score)        // 1. High Score priority
                .ThenBy(p => p.FinishTime)              // 2. Same Score -> Lower Time priority
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
                    // Can use isAlive as "Finished" flag for UI coloring
                    isAlive = !p.HasReachedFinish
                });
            }

            string json = JsonConvert.SerializeObject(progressList);
            Broadcast(new Packet { type = "PROGRESS_UPDATE", payload = json });
        }

        // --- [FIX JSON ERROR HERE] ---
        // Sync player list function
        private void SyncPlayers(PlayerSession newSession)
        {
            // Create list of PlayerState
            List<PlayerState> states = new List<PlayerState>();

            foreach (var p in Players.Values)
            {
                states.Add(new PlayerState
                {
                    playerId = p.PlayerId,
                    playerName = p.PlayerName,
                    x = p.LastX,
                    y = p.LastY
                });
            }

            string jsonList = JsonConvert.SerializeObject(states);

            // [DEBUG] Show what we are sending to new player
            Console.WriteLine($"[Room {RoomId}] SyncPlayers for {newSession.PlayerName}: {jsonList}");

            // Send specifically to new player
            newSession.Send(new Packet { type = "SYNC_PLAYERS", payload = jsonList });
        }
    }
}