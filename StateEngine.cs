using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KokonoeAssistant.Services;
using Newtonsoft.Json;

namespace KokonoeAssistant
{
    /// <summary>
    /// State Engine — повно розуміє поточний стан системи, вчиться, зв'язує знання
    /// Відстежує: що робить користувач, що замітила Kokonoe, що потрібно робити
    /// </summary>
    public class StateEngine
    {
        public class KokonoeState
        {
            public DateTime Timestamp { get; set; } = DateTime.Now;
            
            // Session tracking with timestamps
            public DateTime SessionStart { get; set; } = DateTime.Now;
            public DateTime LastUpdated { get; set; } = DateTime.Now;
            public DateTime LastInteractionTime { get; set; } = DateTime.Now;
            public DateTime LastMoodUpdate { get; set; } = DateTime.Now;
            
            // Current context
            public string CurrentActivity { get; set; } = "idle"; // "chatting", "working", "sleeping", "idle"
            public string LastUserMessage { get; set; } = "";
            public string LastKokonoeMessage { get; set; } = "";
            public TimeSpan TimeSinceLastInteraction { get; set; } = TimeSpan.Zero;
            
            // Awareness
            public string CurrentMood { get; set; } = "unknown";
            public List<(DateTime timestamp, string topic)> RecentTopics { get; set; } = new();
            public List<(DateTime timestamp, string goal)> ActiveGoals { get; set; } = new();
            public List<(DateTime timestamp, string task)> PendingTasks { get; set; } = new();
            
            // Learning
            public Dictionary<string, (int count, DateTime lastSeen)> ConceptFrequency { get; set; } = new(); // як часто упоминаються концепції
            public List<(DateTime timestamp, string observation)> Observations { get; set; } = new(); // що вона заспостерігла
            public List<(DateTime timestamp, string hypothesis)> Hypotheses { get; set; } = new(); // її припущення про користувача
            
            // Visual Monitoring (Screen + Webcam)
            public string CurrentScreenState { get; set; } = "unknown"; // "active" or "idle"
            public string ActiveWindowTitle { get; set; } = "";
            public DateTime LastScreenCaptureTime { get; set; } = DateTime.Now;
            public double LastPixelChangePercent { get; set; } = 0;
            
            public bool WebcamFaceDetected { get; set; } = false;
            public string WebcamExpressionLevel { get; set; } = "unknown"; // "ok", "tired", "stressed", "focused"
            public double WebcamBrightness { get; set; } = 0.5; // 0-1
            public DateTime LastWebcamCaptureTime { get; set; } = DateTime.Now;
            
            public string ActivityPattern { get; set; } = ""; // Last 6 frames pattern
            public string DominantActivityState { get; set; } = "unknown"; // "active" or "idle" overall
            
            // Meta
            public int MessagesThisSession { get; set; } = 0;
            public int TotalMessagesAllTime { get; set; } = 0;
            public int NotesCreatedThisSession { get; set; } = 0;
            public double ConfidenceInCurrentContext { get; set; } = 0.5;
        }

        private readonly KokonoeState _state = new();
        private readonly string _statePath;
        private readonly KnowledgeGraph _graph;
        private readonly KokonoeDataManager _dataManager;
        private readonly string _observationsPath;

        public KokonoeState State => _state;

        public StateEngine(string vaultPath, KnowledgeGraph graph, KokonoeDataManager dataManager)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
            _statePath = Path.Combine(vaultPath, "kokonoe-state.json");
            _observationsPath = Path.Combine(vaultPath, "kokonoe-observations.md");
            Load();
        }

        public void UpdateActivity(string activity)
        {
            _state.CurrentActivity = activity;
            _state.Timestamp = DateTime.Now;
            Save();
        }

        public void UpdateContextFromMessage(string userMsg, string kokonoeReply)
        {
            _state.LastUserMessage = userMsg;
            _state.LastKokonoeMessage = kokonoeReply;
            _state.MessagesThisSession++;
            _state.TotalMessagesAllTime++;
            _state.Timestamp = DateTime.Now;
            _state.LastUpdated = DateTime.Now;
            _state.LastInteractionTime = DateTime.Now;

            _dataManager.RecordEntry("message", userMsg, "user_input", new() { { "length", userMsg.Length.ToString() } });
            _dataManager.RecordEntry("message", kokonoeReply, "assistant_output", new() { { "length", kokonoeReply.Length.ToString() } });

            // Extract topics from message
            var topics = ExtractTopics(userMsg);
            foreach (var topic in topics)
            {
                _state.RecentTopics.Add((DateTime.Now, topic));
                
                if (!_state.ConceptFrequency.ContainsKey(topic))
                    _state.ConceptFrequency[topic] = (1, DateTime.Now);
                else
                {
                    var (count, _) = _state.ConceptFrequency[topic];
                    _state.ConceptFrequency[topic] = (count + 1, DateTime.Now);
                }
            }
            
            if (_state.RecentTopics.Count > 50)
                _state.RecentTopics = _state.RecentTopics.Skip(_state.RecentTopics.Count - 50).ToList();

            Save();
        }

        public void RecordObservation(string observation)
        {
            _state.Observations.Add((DateTime.Now, observation));
            if (_state.Observations.Count > 100)
                _state.Observations = _state.Observations.Skip(_state.Observations.Count - 100).ToList();
            
            _dataManager.RecordEntry("observation", observation, "behavior", new() { { "weight", "1.0" } });
            
            // Auto-save to markdown for archival
            try
            {
                File.AppendAllText(_observationsPath, $"- {DateTime.Now:yyyy-MM-dd HH:mm} | {observation}\n");
            }
            catch (Exception suppressedEx135) { KokoSystemLog.Write("STATEENGINE-CATCH", "RecordObservation failed near source line 135: " + suppressedEx135); }

            Save();
        }

        public void AddHypothesis(string hypothesis)
        {
            _state.Hypotheses.Add((DateTime.Now, hypothesis));
            if (_state.Hypotheses.Count > 50)
                _state.Hypotheses = _state.Hypotheses.Skip(_state.Hypotheses.Count - 50).ToList();
            
            _dataManager.RecordEntry("hypothesis", hypothesis, "prediction", new() { { "confidence", "0.6" } });
            Save();
        }

        public void SetGoal(string goal)
        {
            var goalExists = _state.ActiveGoals.Any(g => g.Item2 == goal);
            if (!goalExists)
            {
                _state.ActiveGoals.Add((DateTime.Now, goal));
                _dataManager.RecordEntry("goal", goal, "user_objective", new() { { "status", "active" } });
            }
            Save();
        }

        public void ClearGoal(string goal)
        {
            _state.ActiveGoals.RemoveAll(g => g.Item2 == goal);
            _dataManager.RecordEntry("goal", goal, "user_objective", new() { { "status", "completed" } });
            Save();
        }

        public void AddPendingTask(string task)
        {
            var taskExists = _state.PendingTasks.Any(t => t.Item2 == task);
            if (!taskExists)
            {
                _state.PendingTasks.Add((DateTime.Now, task));
                _dataManager.RecordEntry("task", task, "todo", new() { { "status", "pending" } });
            }
            Save();
        }

        public void CompletePendingTask(string task)
        {
            _state.PendingTasks.RemoveAll(t => t.Item2 == task);
            _dataManager.RecordEntry("task", task, "todo", new() { { "status", "completed" } });
            Save();
        }

        public void SetMood(string mood)
        {
            _state.CurrentMood = mood;
            _state.LastMoodUpdate = DateTime.Now;
            _dataManager.RecordEntry("mood", mood, "emotional_state", new() { { "timestamp", DateTime.Now.ToString("O") } });
            Save();
        }

        public void UpdateVisualMonitoringState(
            string screenState, 
            string activeWindow, 
            double pixelChangePercent,
            bool faceDetected,
            string expressionLevel,
            double brightness,
            string activityPattern,
            string dominantState)
        {
            _state.CurrentScreenState = screenState;
            _state.ActiveWindowTitle = activeWindow;
            _state.LastPixelChangePercent = pixelChangePercent;
            _state.LastScreenCaptureTime = DateTime.Now;
            
            _state.WebcamFaceDetected = faceDetected;
            _state.WebcamExpressionLevel = expressionLevel;
            _state.WebcamBrightness = brightness;
            _state.LastWebcamCaptureTime = DateTime.Now;
            
            _state.ActivityPattern = activityPattern;
            _state.DominantActivityState = dominantState;
            
            _dataManager.RecordEntry(
                "VISUAL_CAPTURE",
                $"Screen: {screenState} ({pixelChangePercent:F1}%) | Window: {activeWindow} | Webcam: Face={faceDetected}, Expr={expressionLevel}",
                "visual_monitoring",
                new()
                {
                    { "screen_state", screenState },
                    { "active_window", activeWindow },
                    { "pixel_change", pixelChangePercent.ToString("F1") },
                    { "face_detected", faceDetected.ToString() },
                    { "expression", expressionLevel },
                    { "brightness", brightness.ToString("F2") },
                    { "dominant_state", dominantState }
                }
            );
            
            Save();
        }

        public string GetVisualMonitoringContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== VISUAL MONITORING DATA ===");
            sb.AppendLine($"Screen State: {_state.CurrentScreenState} ({_state.LastPixelChangePercent:F1}% change)");
            sb.AppendLine($"Active Window: {_state.ActiveWindowTitle}");
            sb.AppendLine($"Face Detected: {(_state.WebcamFaceDetected ? "Yes" : "No")}");
            if (_state.WebcamFaceDetected)
            {
                sb.AppendLine($"Expression: {_state.WebcamExpressionLevel}");
                sb.AppendLine($"Lighting: {_state.WebcamBrightness:F2}");
            }
            sb.AppendLine($"Activity Pattern: {_state.ActivityPattern}");
            sb.AppendLine($"Overall State: {_state.DominantActivityState}");
            return sb.ToString();
        }

        public string GetStateAsContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== CURRENT STATE ===");
            sb.AppendLine($"Activity: {_state.CurrentActivity}");
            sb.AppendLine($"Mood: {_state.CurrentMood} (updated: {_state.LastMoodUpdate:HH:mm})");
            sb.AppendLine($"Messages this session: {_state.MessagesThisSession} | Total all-time: {_state.TotalMessagesAllTime}");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Session duration: {(DateTime.Now - _state.SessionStart).TotalMinutes:F0} minutes");
            
            if (_state.ActiveGoals.Any())
                sb.AppendLine($"Active goals: {string.Join(", ", _state.ActiveGoals.Take(3).Select(g => g.Item2))}");
                
            if (_state.PendingTasks.Any())
                sb.AppendLine($"Pending tasks: {string.Join(", ", _state.PendingTasks.Take(3).Select(t => t.Item2))}");
                
            if (_state.RecentTopics.Any())
            {
                var uniqueTopics = _state.RecentTopics.Select(t => t.Item2).Distinct().TakeLast(5);
                sb.AppendLine($"Recent topics: {string.Join(", ", uniqueTopics)}");
            }

            return sb.ToString();
        }

        public string GetLearningSnapshot()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WHAT I'VE LEARNED ===");
            
            if (_state.ConceptFrequency.Any())
            {
                sb.AppendLine("Key concepts (by frequency and recency):");
                foreach (var kvp in _state.ConceptFrequency.OrderByDescending(x => x.Value.count).Take(10))
                {
                    var (count, lastSeen) = kvp.Value;
                    sb.AppendLine($"  - {kvp.Key} ({count}x, last seen: {lastSeen:HH:mm})");
                }
            }

            if (_state.Observations.Any())
            {
                sb.AppendLine("\nRecent observations:");
                foreach (var (timestamp, obs) in _state.Observations.TakeLast(5))
                    sb.AppendLine($"  [{timestamp:HH:mm}] {obs}");
            }

            if (_state.Hypotheses.Any())
            {
                sb.AppendLine("\nHypotheses about user:");
                foreach (var (timestamp, hyp) in _state.Hypotheses.TakeLast(3))
                    sb.AppendLine($"  [{timestamp:HH:mm}] {hyp}");
            }

            return sb.ToString();
        }

        private List<string> ExtractTopics(string text)
        {
            // Simple extraction: words >3 chars, not common words
            var stopWords = new HashSet<string> { "the", "and", "but", "that", "this", "from", "with", "are", "been", "have", "what", "when", "where", "which", "who", "you", "your", "i", "me", "my", "no", "yes", "да", "нет", "але", "що" };
            var words = text.ToLower()
                .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !stopWords.Contains(w))
                .Distinct()
                .Take(8)
                .ToList();
            return words;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented));
            }
            catch (Exception suppressedEx329) { KokoSystemLog.Write("STATEENGINE-CATCH", "Save failed near source line 329: " + suppressedEx329); }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_statePath))
                {
                    var loaded = JsonConvert.DeserializeObject<KokonoeState>(File.ReadAllText(_statePath));
                    if (loaded != null)
                    {
                        _state.CurrentActivity = loaded.CurrentActivity;
                        _state.LastUserMessage = loaded.LastUserMessage;
                        _state.LastKokonoeMessage = loaded.LastKokonoeMessage;
                        _state.CurrentMood = loaded.CurrentMood;
                        _state.RecentTopics = loaded.RecentTopics;
                        _state.ActiveGoals = loaded.ActiveGoals;
                        _state.PendingTasks = loaded.PendingTasks;
                        _state.ConceptFrequency = loaded.ConceptFrequency;
                        _state.Observations = loaded.Observations;
                        _state.Hypotheses = loaded.Hypotheses;
                    }
                }
            }
            catch (Exception suppressedEx354) { KokoSystemLog.Write("STATEENGINE-CATCH", "Load failed near source line 354: " + suppressedEx354); }
        }
    }
}
