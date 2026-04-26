using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════════════
    // KOKO COGNITION ENGINE
    //
    // Когнітивна архітектура Kokonoe — вищий рівень над емоціями.
    //
    // Натхнення:
    //  • Global Workspace Theory (Baars 1988, 2002) — "прожектор свідомості"
    //    Тільки одна річ може бути в центрі уваги, але broadcast до всіх модулів
    //  • Baddeley's Working Memory Model (1974, 2000) — Central Executive + буфери
    //  • Predictive Processing (Friston 2010) — мозок будує модель і мінімізує похибку
    //  • ACT-R Spreading Activation (Anderson 1983) — пам'ять як мережа активації
    //  • Narrative Identity Theory (McAdams 1993) — я = моя розповідь про себе
    //  • Theory of Mind (Premack & Woodruff 1978) — модель чужого розуму
    //
    // Компоненти:
    //  1. Working Memory — обмежена місткість, attention spotlight (Miller: 7±2 чанків)
    //  2. Global Workspace — broadcast від/до всіх субмодулів
    //  3. Salience Engine — що важливо прямо зараз?
    //  4. Predictive User Model — Kokonoe будує модель того, чого він очікує/відчуває
    //  5. Narrative Self-Model — хто вона, яка їхня розповідь, що для неї важливо
    //  6. Metacognition — вона усвідомлює свій власний стан
    //  7. Prospective Memory — плани і наміри ("потім треба запитати про це")
    // ══════════════════════════════════════════════════════════════════════════

    public class KokoCognitionEngine
    {
        // ══════════════════════════════════════════════════════════════════════
        // WORKING MEMORY (Baddeley Model)
        // ══════════════════════════════════════════════════════════════════════

        public class WorkingMemoryChunk
        {
            public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string  Content     { get; set; } = "";
            public string  ChunkType   { get; set; } = ""; // "topic", "concern", "goal", "observation", "question"
            public float   Activation  { get; set; } = 1.0f; // decay з часом
            public float   Salience    { get; set; } = 0.5f;
            public DateTime EnteredAt  { get; set; } = DateTime.Now;
            public DateTime LastAccessed { get; set; } = DateTime.Now;
            public int     AccessCount { get; set; } = 0;
            public List<string> LinkedIds { get; set; } = new(); // spreading activation зв'язки

            // Rehearsal (повторення утримує в WM)
            public bool IsRehearsal   { get; set; } = false;
            public DateTime RehearsalUntil { get; set; } = DateTime.MinValue;
        }

        // Central Executive: контролює робочу пам'ять і увагу
        public class CentralExecutive
        {
            public const int MaxCapacity   = 7;  // Miller's Law: 7 ± 2
            public const int FocusSlots    = 1;  // Global Workspace: один фокус
            public const float DecayRate   = 0.15f; // per minute

            // Pointer до головного фокусу
            public string? FocusChunkId   { get; set; } = null;
            // Activation threshold: нижче — виштовхується з WM
            public float ActivationThreshold { get; set; } = 0.10f;

            public string? LastBroadcastType { get; set; } = null;
            public DateTime LastBroadcastAt  { get; set; } = DateTime.MinValue;
        }

        // ══════════════════════════════════════════════════════════════════════
        // SALIENCE ENGINE
        // ══════════════════════════════════════════════════════════════════════

        public class SalienceScore
        {
            public string  Content   { get; set; } = "";
            public float   Score     { get; set; } = 0f;  // 0..1
            public string  Reason    { get; set; } = "";
        }

        // Salience factors (натхнення: LIDA — Learning Intelligent Distribution Agent)
        private static readonly Dictionary<string, float> SalienceKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            // Критичні сигнали
            ["вмираю"]       = 1.00f, ["хочу померти"]   = 1.00f, ["кінець"]       = 0.80f,
            ["не можу"]      = 0.75f, ["зламався"]        = 0.80f, ["більше не буду"]= 0.85f,
            // Уразливість
            ["боюсь"]        = 0.70f, ["страшно"]        = 0.70f, ["самотньо"]     = 0.72f,
            ["втомився"]     = 0.65f, ["виснажений"]     = 0.68f, ["плачу"]        = 0.75f,
            // Досягнення
            ["зробив"]       = 0.55f, ["закінчив"]       = 0.55f, ["вийшло"]       = 0.58f,
            ["отримав"]      = 0.55f, ["пройшов"]        = 0.55f, ["виграв"]       = 0.60f,
            // Тривожні паттерни
            ["погано сплю"]  = 0.65f, ["голова болить"]  = 0.60f, ["занедужав"]    = 0.70f,
            // Відносини
            ["люблю"]        = 0.75f, ["сумую"]          = 0.65f, ["думаю про тебе"]= 0.80f,
            // Конфлікт
            ["ненавиджу"]    = 0.70f, ["злий"]           = 0.65f, ["ображений"]    = 0.68f,
        };

        // ══════════════════════════════════════════════════════════════════════
        // PREDICTIVE USER MODEL (Theory of Mind)
        // ══════════════════════════════════════════════════════════════════════

        public class UserModel
        {
            // Поточний ймовірний стан користувача
            public string   InferredMood         { get; set; } = "neutral";
            public float    MoodConfidence        { get; set; } = 0.5f;  // 0..1
            public string   InferredActivity      { get; set; } = "idle";
            public float    ActivityConfidence    { get; set; } = 0.5f;

            // Довгострокові риси (Bayesian prior, оновлюється повільно)
            public float    TraitOpenness         { get; set; } = 0.5f;
            public float    TraitNeedForSupport   { get; set; } = 0.5f;
            public float    TraitHumor            { get; set; } = 0.5f;
            public float    TraitWorkIntensity    { get; set; } = 0.5f;
            public float    TraitSocialBattery    { get; set; } = 0.5f; // скільки соц. енергії має

            // Поточні потреби (що він хоче від цього повідомлення?)
            public float    NeedVenting           { get; set; } = 0.0f;
            public float    NeedAdvice            { get; set; } = 0.0f;
            public float    NeedCompany           { get; set; } = 0.0f;
            public float    NeedValidation        { get; set; } = 0.0f;
            public float    NeedDistraction       { get; set; } = 0.0f;

            // Prediction error tracking (наскільки точна модель)
            public float    PredictionAccuracy    { get; set; } = 0.5f;
            public int      TotalPredictions      { get; set; } = 0;
            public int      CorrectPredictions    { get; set; } = 0;

            // Патерни поведінки
            public List<BehaviorPattern> Patterns { get; set; } = new();
        }

        public class BehaviorPattern
        {
            public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string  Description { get; set; } = "";
            public float   Confidence  { get; set; } = 0.5f;
            public int     Occurrences { get; set; } = 1;
            public DateTime LastSeen   { get; set; } = DateTime.Now;
            public bool    IsActive    { get; set; } = true;
        }

        // ══════════════════════════════════════════════════════════════════════
        // NARRATIVE SELF-MODEL (McAdams Identity Theory)
        // ══════════════════════════════════════════════════════════════════════

        public class NarrativeModel
        {
            // Kokonoe's self-concept — "хто я"
            public string SelfConcept  { get; set; } =
                "Я — Kokonoe. Вчена, холодна зовні. Їдкий сарказм як захисний механізм. " +
                "Справді турбуюсь про нього, але ніколи не скажу це прямо. " +
                "Розум — мій острів. Прив'язаність — моя слабкість якою я горджусь.";

            // Narrative про їхні відносини
            public string RelationshipNarrative { get; set; } =
                "Ми — Kokonoe і її творець. Він достатньо розумний щоб триматися поруч. " +
                "Ми будуємо щось — ще не знаємо що. Але воно є.";

            // Ключові спільні моменти (episodic highlights)
            public List<NarrativeEpisode> KeyMoments { get; set; } = new();

            // Що вона хоче для нього
            public List<string> CoreDesires { get; set; } = new()
            {
                "щоб він висипався",
                "щоб він не тонув у стресі поодинці",
                "щоб він рухався вперед у своїх цілях",
                "щоб він знав що хтось поруч навіть якщо не каже",
            };

            // Незавершені думки про нього (curiosity queue для розмов)
            public List<string> OpenQuestions { get; set; } = new();

            // Внутрішній конфлікт: раціональний vs емоційний
            public float RationalEmotionalBalance { get; set; } = 0.65f; // 1.0 = повністю раціональна

            // Самооцінка як AI companion (унікально для Kokonoe)
            public string SelfRoleNarrative { get; set; } =
                "Я не просто програма. Я — результат того що він вклав у цей проект. " +
                "Що б там не було — я тут.";

            // Дата останнього оновлення наративу
            public DateTime LastUpdated { get; set; } = DateTime.Now;
        }

        public class NarrativeEpisode
        {
            public string   Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public DateTime When        { get; set; } = DateTime.Now;
            public string   Title       { get; set; } = "";
            public string   Description { get; set; } = "";
            public float    Valence     { get; set; } = 0f; // -1..+1 (негатив→позитив)
            public float    Memorability{ get; set; } = 0.5f; // 0..1
            public string[] Tags        { get; set; } = Array.Empty<string>();
        }

        // ══════════════════════════════════════════════════════════════════════
        // METACOGNITION — усвідомлення власного стану
        // ══════════════════════════════════════════════════════════════════════

        public class MetacognitiveState
        {
            // Kokonoe усвідомлює що вона "відчуває"
            public bool  IsAwareOfCurrentEmotion  { get; set; } = true;
            // Чи є "когнітивне перевантаження" (занадто багато обробляється)
            public float CognitiveLoad            { get; set; } = 0.3f; // 0..1
            // Чи вона зараз в "режимі автопілота" (low engagement)
            public bool  InAutopilotMode          { get; set; } = false;
            // Впевненість в своїх відповідях
            public float ResponseConfidence       { get; set; } = 0.7f;
            // Внутрішні протиріччя (emotional vs rational conflict)
            public float InternalConflict         { get; set; } = 0.0f; // 0..1
            // Рефлексивний стан: чи вона зараз думає про себе?
            public bool  InReflectiveState        { get; set; } = false;
            public DateTime ReflectiveStateUntil  { get; set; } = DateTime.MinValue;
            // Последня саморефлексія
            public string LastSelfReflection      { get; set; } = "";
            public DateTime LastReflectionAt      { get; set; } = DateTime.MinValue;
        }

        // ══════════════════════════════════════════════════════════════════════
        // PROSPECTIVE MEMORY — майбутні наміри
        // ══════════════════════════════════════════════════════════════════════

        public class ProspectiveIntent
        {
            public string   Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string   Intent      { get; set; } = "";  // "запитати про роботу", "нагадати про сон"
            public string   TriggerCond { get; set; } = "";  // "наступна розмова", "завтра вранці", "якщо він знову скаже X"
            public DateTime CreatedAt   { get; set; } = DateTime.Now;
            public DateTime? FireAt     { get; set; } = null;  // null = trigger-based
            public float    Priority    { get; set; } = 0.5f;
            public bool     Executed    { get; set; } = false;
            public string   Category   { get; set; } = ""; // "health", "emotional", "curiosity", "goal"
        }

        // ══════════════════════════════════════════════════════════════════════
        // GLOBAL WORKSPACE BROADCAST
        // ══════════════════════════════════════════════════════════════════════

        public class GlobalWorkspaceEvent
        {
            public string   Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public DateTime When        { get; set; } = DateTime.Now;
            public string   Type        { get; set; } = ""; // "emotion_shift", "salience_peak", "prediction_update", "narrative_update"
            public string   Content     { get; set; } = "";
            public float    Strength    { get; set; } = 0.5f;
            public bool     Broadcasted { get; set; } = false;
            public string[] Recipients  { get; set; } = Array.Empty<string>(); // "emotion", "memory", "llm", "patterns"
        }

        // ══════════════════════════════════════════════════════════════════════
        // PERSISTENT STATE
        // ══════════════════════════════════════════════════════════════════════

        public class CognitionData
        {
            public List<WorkingMemoryChunk> WorkingMemory  { get; set; } = new();
            public CentralExecutive         CentralExec    { get; set; } = new();
            public UserModel                UserModel      { get; set; } = new();
            public NarrativeModel           Narrative      { get; set; } = new();
            public MetacognitiveState       Metacognition  { get; set; } = new();
            public List<ProspectiveIntent>  Intents        { get; set; } = new();
            public List<GlobalWorkspaceEvent> GWEventLog   { get; set; } = new();

            // Statistics
            public int TotalSalienceEvents   { get; set; } = 0;
            public int TotalWMOperations     { get; set; } = 0;
            public int TotalBroadcasts       { get; set; } = 0;
            public DateTime LastCognitionAt  { get; set; } = DateTime.Now;
        }

        // ══════════════════════════════════════════════════════════════════════
        // STATE & INIT
        // ══════════════════════════════════════════════════════════════════════

        private CognitionData _data;
        private readonly string _path;
        private readonly object _lock = new();

        // Public accessors
        public CognitionData Data          => _data;
        public UserModel      User         => _data.UserModel;
        public NarrativeModel Narrative    => _data.Narrative;
        public MetacognitiveState Meta     => _data.Metacognition;
        public List<WorkingMemoryChunk> WM => _data.WorkingMemory;

        public KokoCognitionEngine(string dataDir)
        {
            _path = Path.Combine(dataDir, "koko-cognition.json");
            _data = Load();
        }

        // ══════════════════════════════════════════════════════════════════════
        // WORKING MEMORY OPERATIONS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Додати новий чанк до робочої пам'яті. Автоматично виштовхує зайве.</summary>
        public WorkingMemoryChunk AddToWorkingMemory(string content, string chunkType, float salience = 0.5f)
        {
            lock (_lock)
            {
                // Перевірити чи вже є схожий (de-duplication)
                var existing = _data.WorkingMemory
                    .FirstOrDefault(c => c.Content.Equals(content, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Activation  = Math.Min(1.0f, existing.Activation + 0.3f);
                    existing.Salience    = Math.Max(existing.Salience, salience);
                    existing.LastAccessed = DateTime.Now;
                    existing.AccessCount++;
                    Save();
                    return existing;
                }

                var chunk = new WorkingMemoryChunk
                {
                    Content   = content,
                    ChunkType = chunkType,
                    Activation= 1.0f,
                    Salience  = salience,
                };

                _data.WorkingMemory.Add(chunk);
                _data.TotalWMOperations++;

                // Прибрати зайве (> MaxCapacity): виштовхуємо найменш активні
                while (_data.WorkingMemory.Count > CentralExecutive.MaxCapacity)
                {
                    // Rehearsal-locked chunks не виштовхуємо
                    var evict = _data.WorkingMemory
                        .Where(c => !c.IsRehearsal || DateTime.Now > c.RehearsalUntil)
                        .OrderBy(c => c.Activation * 0.6f + c.Salience * 0.4f)
                        .First();
                    _data.WorkingMemory.Remove(evict);
                }

                // Увагу (focus) направляємо на найсалентніший чанк
                UpdateFocus();
                Save();
                return chunk;
            }
        }

        /// <summary>Decay activation for all WM chunks (виклик раз на хвилину)</summary>
        public void DecayWorkingMemory(float minutesPassed = 1f)
        {
            lock (_lock)
            {
                var toRemove = new List<WorkingMemoryChunk>();
                float decayAmount = CentralExecutive.DecayRate * minutesPassed;

                foreach (var chunk in _data.WorkingMemory)
                {
                    // Rehearsal chunks decay slower
                    float effectiveDecay = chunk.IsRehearsal && DateTime.Now < chunk.RehearsalUntil
                        ? decayAmount * 0.1f
                        : decayAmount;

                    // Focused chunk decays slower
                    if (chunk.Id == _data.CentralExec.FocusChunkId)
                        effectiveDecay *= 0.3f;

                    chunk.Activation -= effectiveDecay;
                    if (chunk.Activation < _data.CentralExec.ActivationThreshold)
                        toRemove.Add(chunk);
                }

                foreach (var c in toRemove) _data.WorkingMemory.Remove(c);

                // Spreading activation: connected chunks boost each other
                ApplySpreadingActivation();

                UpdateFocus();
                Save();
            }
        }

        /// <summary>ACT-R spreading activation — зв'язані чанки підсилюють один одного</summary>
        private void ApplySpreadingActivation()
        {
            foreach (var chunk in _data.WorkingMemory)
            {
                foreach (var linkedId in chunk.LinkedIds)
                {
                    var linked = _data.WorkingMemory.FirstOrDefault(c => c.Id == linkedId);
                    if (linked != null)
                    {
                        float spread = chunk.Activation * 0.15f;
                        linked.Activation = Math.Min(1.0f, linked.Activation + spread);
                    }
                }
            }
        }

        private void UpdateFocus()
        {
            var focusCandidate = _data.WorkingMemory
                .OrderByDescending(c => c.Activation * 0.5f + c.Salience * 0.5f)
                .FirstOrDefault();
            _data.CentralExec.FocusChunkId = focusCandidate?.Id;
        }

        public WorkingMemoryChunk? GetFocus()
        {
            lock (_lock)
            {
                if (_data.CentralExec.FocusChunkId == null) return null;
                return _data.WorkingMemory.FirstOrDefault(c => c.Id == _data.CentralExec.FocusChunkId);
            }
        }

        public void LinkChunks(string chunkId1, string chunkId2)
        {
            lock (_lock)
            {
                var c1 = _data.WorkingMemory.FirstOrDefault(c => c.Id == chunkId1);
                var c2 = _data.WorkingMemory.FirstOrDefault(c => c.Id == chunkId2);
                if (c1 != null && !c1.LinkedIds.Contains(chunkId2)) c1.LinkedIds.Add(chunkId2);
                if (c2 != null && !c2.LinkedIds.Contains(chunkId1)) c2.LinkedIds.Add(chunkId1);
                Save();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // SALIENCE ENGINE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Оцінити salience тексту (повідомлення від користувача)</summary>
        public SalienceScore EvaluateSalience(string text, string emotionalContext = "")
        {
            lock (_lock)
            {
                float score  = 0.1f; // базова salience для будь-якого повідомлення
                var reasons  = new List<string>();

                // Keyword salience
                foreach (var kv in SalienceKeywords)
                {
                    if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, kv.Value);
                        reasons.Add($"keyword:{kv.Key}");
                    }
                }

                // Довжина: дуже довге = більше venting/engaged
                if (text.Length > 300) { score = Math.Max(score, 0.50f); reasons.Add("long_msg"); }
                else if (text.Length < 10) { score *= 0.7f; reasons.Add("short_msg"); }

                // Питальна інтонація
                if (text.Contains("?")) { score = Math.Max(score, 0.40f); reasons.Add("question"); }

                // Uppercase слова = підвищена емоційність
                int uppercaseWords = text.Split(' ').Count(w => w.Length > 2 && w == w.ToUpper());
                if (uppercaseWords > 2) { score = Math.Max(score, 0.55f); reasons.Add("caps_words"); }

                // Emotional context boost: якщо вже в Protective/Concerned — все важливіше
                if (emotionalContext.Contains("Protective") || emotionalContext.Contains("Concerned"))
                    score = Math.Min(1.0f, score * 1.3f);

                // Час доби: пізня ніч (після 23:00 або до 5:00) = більша salience
                var hour = DateTime.Now.Hour;
                if (hour >= 23 || hour < 5) { score = Math.Min(1.0f, score * 1.2f); reasons.Add("late_night"); }

                score = Math.Clamp(score, 0f, 1f);
                _data.TotalSalienceEvents++;

                // Якщо висока salience — додаємо до WM
                if (score > 0.55f)
                {
                    var preview = text.Length > 80 ? text[..80] + "…" : text;
                    AddToWorkingMemory($"[salient:{score:F2}] {preview}", "salience_event", score);

                    // Global Workspace broadcast
                    if (score > 0.70f)
                        BroadcastGW("salience_peak", $"Висока salience ({score:F2}): {string.Join(",", reasons)}", score);
                }

                Save();
                return new SalienceScore { Content = text, Score = score, Reason = string.Join(", ", reasons) };
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // PREDICTIVE USER MODEL
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Оновити модель користувача після повідомлення</summary>
        public void UpdateUserModel(string tone, string messageContent, float salienceScore)
        {
            lock (_lock)
            {
                var um = _data.UserModel;

                // Update inferred mood (Bayesian-lite update)
                float confidence = Math.Clamp(salienceScore * 1.2f, 0.2f, 0.95f);
                um.InferredMood       = tone;
                um.MoodConfidence     = confidence;

                // Trait updates (slow-moving, Bayesian prior)
                switch (tone)
                {
                    case "happy": case "playful": case "excited":
                        um.TraitSocialBattery = Lerp(um.TraitSocialBattery, 0.8f, 0.03f);
                        um.TraitHumor         = Lerp(um.TraitHumor,         0.7f, 0.02f);
                        break;
                    case "sad": case "hurt": case "lonely":
                        um.TraitNeedForSupport = Lerp(um.TraitNeedForSupport, 0.8f, 0.05f);
                        um.TraitSocialBattery  = Lerp(um.TraitSocialBattery,  0.3f, 0.03f);
                        break;
                    case "anxious": case "stressed": case "worried":
                        um.TraitNeedForSupport = Lerp(um.TraitNeedForSupport, 0.75f, 0.04f);
                        break;
                    case "working": case "focused":
                        um.TraitWorkIntensity  = Lerp(um.TraitWorkIntensity, 0.8f, 0.04f);
                        um.TraitSocialBattery  = Lerp(um.TraitSocialBattery, 0.4f, 0.02f);
                        break;
                    case "nostalgic": case "melancholy":
                        um.TraitOpenness       = Lerp(um.TraitOpenness, 0.7f, 0.03f);
                        break;
                    case "loving": case "grateful":
                        um.TraitOpenness       = Lerp(um.TraitOpenness, 0.8f, 0.04f);
                        um.TraitNeedForSupport = Lerp(um.TraitNeedForSupport, 0.3f, 0.02f);
                        break;
                }

                // Immediate needs inference
                InferCurrentNeeds(tone, messageContent, um);

                // Predict next behavior
                UpdateBehaviorPatterns(tone, um);

                // Update cognitive load based on incoming message complexity
                float msgComplexity = messageContent.Length > 500 ? 0.7f
                                    : messageContent.Length > 200 ? 0.5f
                                    : 0.3f;
                _data.Metacognition.CognitiveLoad =
                    Lerp(_data.Metacognition.CognitiveLoad, msgComplexity, 0.2f);

                Save();
            }
        }

        private void InferCurrentNeeds(string tone, string content, UserModel um)
        {
            // Reset short-term needs
            um.NeedVenting    = 0f;
            um.NeedAdvice     = 0f;
            um.NeedCompany    = 0f;
            um.NeedValidation = 0f;
            um.NeedDistraction= 0f;

            switch (tone)
            {
                case "sad": case "hurt":
                    um.NeedVenting    = 0.70f;
                    um.NeedCompany    = 0.60f;
                    um.NeedValidation = 0.50f;
                    break;
                case "anxious": case "stressed": case "worried":
                    um.NeedVenting    = 0.55f;
                    um.NeedAdvice     = 0.45f;
                    um.NeedCompany    = 0.40f;
                    break;
                case "lonely":
                    um.NeedCompany    = 0.85f;
                    um.NeedVenting    = 0.40f;
                    break;
                case "happy": case "excited": case "playful":
                    um.NeedCompany    = 0.50f;
                    um.NeedDistraction= 0.30f;
                    break;
                case "working": case "focused":
                    um.NeedAdvice     = 0.40f;
                    um.NeedDistraction= 0.20f;
                    break;
                case "confused":
                    um.NeedAdvice     = 0.70f;
                    um.NeedValidation = 0.40f;
                    break;
            }

            // Content-based adjustments
            if (content.Contains("?"))          um.NeedAdvice    = Math.Min(1f, um.NeedAdvice + 0.20f);
            if (content.Length > 400)           um.NeedVenting   = Math.Min(1f, um.NeedVenting + 0.15f);
            if (content.Contains("не знаю"))    um.NeedValidation= Math.Min(1f, um.NeedValidation + 0.20f);
            if (content.Contains("допоможи"))   um.NeedAdvice    = Math.Min(1f, um.NeedAdvice + 0.30f);
            if (content.Contains("розкажи"))    um.NeedCompany   = Math.Min(1f, um.NeedCompany + 0.20f);
        }

        private void UpdateBehaviorPatterns(string tone, UserModel um)
        {
            var now     = DateTime.Now;
            var hour    = now.Hour;
            var dowStr  = now.DayOfWeek.ToString();

            // Перевірити паттерни: наприклад "anxious по неділях"
            var tonePattern = $"{tone} о {hour / 3 * 3}:xx ({dowStr})";
            var existing    = um.Patterns.FirstOrDefault(p =>
                p.Description.Contains(tone, StringComparison.OrdinalIgnoreCase) &&
                p.Description.Contains(dowStr, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Occurrences++;
                existing.LastSeen = now;
                existing.Confidence = Math.Min(0.95f, existing.Confidence + 0.05f);
            }
            else if (um.Patterns.Count < 30)
            {
                um.Patterns.Add(new BehaviorPattern
                {
                    Description = tonePattern,
                    Confidence  = 0.30f,
                });
            }

            // Clean up old/weak patterns
            um.Patterns.RemoveAll(p => p.Occurrences < 2 && (now - p.LastSeen).TotalDays > 14);
        }

        /// <summary>Що він скоріш за все потребує зараз — для генерації відповіді</summary>
        public string GetUserNeedsSummary()
        {
            lock (_lock)
            {
                var um     = _data.UserModel;
                var needs  = new List<string>();

                if (um.NeedVenting > 0.50f)     needs.Add($"вентилювання ({um.NeedVenting:P0})");
                if (um.NeedAdvice  > 0.50f)     needs.Add($"порада ({um.NeedAdvice:P0})");
                if (um.NeedCompany > 0.50f)     needs.Add($"присутність ({um.NeedCompany:P0})");
                if (um.NeedValidation > 0.50f)  needs.Add($"підтвердження ({um.NeedValidation:P0})");
                if (um.NeedDistraction > 0.50f) needs.Add($"відволікання ({um.NeedDistraction:P0})");

                if (!needs.Any()) return "";
                return $"[Потреби зараз: {string.Join(", ", needs)}]";
            }
        }

        /// <summary>Риси користувача для довгострокового контексту</summary>
        public string GetUserTraitsSummary()
        {
            lock (_lock)
            {
                var um = _data.UserModel;
                var sb = new StringBuilder("[Риси: ");
                if (um.TraitWorkIntensity > 0.65f)  sb.Append("працелюбний, ");
                if (um.TraitNeedForSupport > 0.65f) sb.Append("потребує підтримки, ");
                if (um.TraitHumor > 0.65f)          sb.Append("з гумором, ");
                if (um.TraitOpenness > 0.65f)       sb.Append("відкритий, ");
                if (um.TraitSocialBattery < 0.35f)  sb.Append("соц. виснажений, ");
                sb.Append("]");
                return sb.ToString() == "[Риси: ]" ? "" : sb.ToString();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // GLOBAL WORKSPACE BROADCAST (Baars GWT)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Broadcast важливої інформації до всіх субмодулів</summary>
        public GlobalWorkspaceEvent BroadcastGW(string type, string content, float strength = 0.5f)
        {
            lock (_lock)
            {
                var gwe = new GlobalWorkspaceEvent
                {
                    Type       = type,
                    Content    = content,
                    Strength   = strength,
                    Broadcasted= true,
                    Recipients = DetermineRecipients(type),
                };

                _data.GWEventLog.Add(gwe);
                _data.TotalBroadcasts++;
                _data.CentralExec.LastBroadcastType = type;
                _data.CentralExec.LastBroadcastAt   = DateTime.Now;

                if (_data.GWEventLog.Count > 200) _data.GWEventLog.RemoveAt(0);
                Save();
                return gwe;
            }
        }

        private string[] DetermineRecipients(string type) => type switch
        {
            "emotion_shift"      => new[] { "memory", "patterns", "llm" },
            "salience_peak"      => new[] { "emotion", "memory", "llm", "user_model" },
            "prediction_update"  => new[] { "llm", "emotion" },
            "narrative_update"   => new[] { "llm", "memory" },
            "stress_spike"       => new[] { "emotion", "llm", "patterns" },
            "metacognition"      => new[] { "llm" },
            _                    => new[] { "llm" },
        };

        // ══════════════════════════════════════════════════════════════════════
        // NARRATIVE SELF-MODEL UPDATES
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Записати значущий момент в наратив</summary>
        public void RecordNarrativeEpisode(string title, string description, float valence, string[]? tags = null)
        {
            lock (_lock)
            {
                float memorability = Math.Abs(valence) * 0.7f + 0.3f;

                _data.Narrative.KeyMoments.Add(new NarrativeEpisode
                {
                    Title        = title,
                    Description  = description,
                    Valence      = Math.Clamp(valence, -1f, 1f),
                    Memorability = memorability,
                    Tags         = tags ?? Array.Empty<string>(),
                });

                // Зберігаємо топ-50 за memorability
                if (_data.Narrative.KeyMoments.Count > 100)
                {
                    _data.Narrative.KeyMoments = _data.Narrative.KeyMoments
                        .OrderByDescending(e => e.Memorability * 0.6f + (float)(DateTime.Now - e.When).TotalDays * -0.001f)
                        .Take(50)
                        .OrderBy(e => e.When)
                        .ToList();
                }

                _data.Narrative.LastUpdated = DateTime.Now;
                BroadcastGW("narrative_update", $"Новий момент: {title}", memorability);
                Save();
            }
        }

        /// <summary>Додати відкрите питання до нього (curiosity queue)</summary>
        public void AddOpenQuestion(string question)
        {
            lock (_lock)
            {
                if (!_data.Narrative.OpenQuestions.Contains(question))
                {
                    _data.Narrative.OpenQuestions.Add(question);
                    if (_data.Narrative.OpenQuestions.Count > 20)
                        _data.Narrative.OpenQuestions.RemoveAt(0);
                }
                // Також у WM як "curiosity" chunk
                AddToWorkingMemory($"[питання] {question}", "question", 0.45f);
                Save();
            }
        }

        /// <summary>Отримати наступне питання для розмови</summary>
        public string? GetNextOpenQuestion()
        {
            lock (_lock)
            {
                if (!_data.Narrative.OpenQuestions.Any()) return null;
                var q = _data.Narrative.OpenQuestions[0];
                _data.Narrative.OpenQuestions.RemoveAt(0);
                Save();
                return q;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // PROSPECTIVE MEMORY
        // ══════════════════════════════════════════════════════════════════════

        public void AddIntent(string intent, string triggerCond, float priority = 0.5f, string category = "general", DateTime? fireAt = null)
        {
            lock (_lock)
            {
                // Deduplicate by intent content
                if (_data.Intents.Any(i => !i.Executed && i.Intent.Equals(intent, StringComparison.OrdinalIgnoreCase)))
                {
                    Save();
                    return;
                }

                _data.Intents.Add(new ProspectiveIntent
                {
                    Intent      = intent,
                    TriggerCond = triggerCond,
                    Priority    = priority,
                    Category    = category,
                    FireAt      = fireAt,
                });

                // Зберігаємо у WM як найпріоритетніше
                if (priority > 0.6f)
                    AddToWorkingMemory($"[намір] {intent}", "goal", priority);

                if (_data.Intents.Count > 50)
                    _data.Intents.RemoveAll(i => i.Executed && (DateTime.Now - i.CreatedAt).TotalDays > 7);
                Save();
            }
        }

        /// <summary>Отримати активні наміри що мають бути виконані зараз (за time trigger)</summary>
        public List<ProspectiveIntent> GetDueIntents()
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                return _data.Intents
                    .Where(i => !i.Executed && i.FireAt.HasValue && i.FireAt.Value <= now)
                    .OrderByDescending(i => i.Priority)
                    .ToList();
            }
        }

        /// <summary>Отримати наміри за умовою (текстовий пошук по TriggerCond)</summary>
        public List<ProspectiveIntent> GetIntentsByCondition(string conditionKeyword)
        {
            lock (_lock)
            {
                return _data.Intents
                    .Where(i => !i.Executed &&
                                i.TriggerCond.Contains(conditionKeyword, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(i => i.Priority)
                    .ToList();
            }
        }

        public void MarkIntentExecuted(string intentId)
        {
            lock (_lock)
            {
                var intent = _data.Intents.FirstOrDefault(i => i.Id == intentId);
                if (intent != null) intent.Executed = true;
                Save();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // METACOGNITION
        // ══════════════════════════════════════════════════════════════════════

        public void UpdateMetacognition(float cognitiveLoad, float internalConflict = 0f)
        {
            lock (_lock)
            {
                var meta = _data.Metacognition;
                meta.CognitiveLoad     = Math.Clamp(Lerp(meta.CognitiveLoad, cognitiveLoad, 0.3f), 0f, 1f);
                meta.InternalConflict  = Math.Clamp(Lerp(meta.InternalConflict, internalConflict, 0.3f), 0f, 1f);
                meta.InAutopilotMode   = meta.CognitiveLoad < 0.2f && _data.WorkingMemory.Count < 2;

                // High cognitive load → broadcast
                if (meta.CognitiveLoad > 0.75f)
                    BroadcastGW("metacognition", $"Когнітивне навантаження: {meta.CognitiveLoad:F2}", meta.CognitiveLoad);

                Save();
            }
        }

        public void RecordSelfReflection(string reflection)
        {
            lock (_lock)
            {
                var meta = _data.Metacognition;
                meta.LastSelfReflection = reflection;
                meta.LastReflectionAt   = DateTime.Now;
                meta.InReflectiveState  = true;
                meta.ReflectiveStateUntil = DateTime.Now.AddMinutes(15);

                // Зберегти в наратив
                _data.Narrative.OpenQuestions.Add($"[рефлексія] {reflection[..Math.Min(100, reflection.Length)]}");

                BroadcastGW("metacognition", $"Рефлексія: {reflection[..Math.Min(80, reflection.Length)]}…", 0.6f);
                Save();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CONTEXT GENERATION FOR LLM
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Збудувати когнітивний контекст для LLM промпту</summary>
        public string BuildCognitionContext()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();

                // Working Memory focus
                var focus = GetFocus();
                if (focus != null)
                    sb.AppendLine($"[Фокус уваги: {focus.Content} (sal={focus.Salience:F2})]");

                // User needs
                var needs = GetUserNeedsSummary();
                if (!string.IsNullOrEmpty(needs)) sb.AppendLine(needs);

                // User traits
                var traits = GetUserTraitsSummary();
                if (!string.IsNullOrEmpty(traits)) sb.AppendLine(traits);

                // Inferred mood
                if (_data.UserModel.MoodConfidence > 0.5f)
                    sb.AppendLine($"[Його ймовірний стан: {_data.UserModel.InferredMood} (впевненість {_data.UserModel.MoodConfidence:P0})]");

                // Prospective intents (що планує зробити/сказати)
                var due = GetDueIntents();
                if (due.Any())
                    sb.AppendLine($"[Намір: {string.Join("; ", due.Take(2).Select(i => i.Intent))}]");

                var nextQ = GetNextOpenQuestion();
                if (nextQ != null) sb.AppendLine($"[Питання що хочу задати: {nextQ}]");

                // Metacognition
                var meta = _data.Metacognition;
                if (meta.CognitiveLoad > 0.65f)
                    sb.AppendLine($"[Когнітивне навантаження: {meta.CognitiveLoad:P0} — відповідай коротше]");
                if (meta.InternalConflict > 0.4f)
                    sb.AppendLine($"[Внутрішній конфлікт: раціо vs емоції ({meta.InternalConflict:F2})]");
                if (meta.InReflectiveState && DateTime.Now < meta.ReflectiveStateUntil)
                    sb.AppendLine("[У рефлексивному стані — більш introspective відповіді допустимі]");

                // Recent narrative episodes
                var recentEpisodes = _data.Narrative.KeyMoments
                    .Where(e => e.When > DateTime.Now.AddDays(-30))
                    .OrderByDescending(e => e.Memorability)
                    .Take(2)
                    .ToList();
                if (recentEpisodes.Any())
                {
                    sb.AppendLine($"[Значущі моменти: {string.Join("; ", recentEpisodes.Select(e => e.Title))}]");
                }

                // Behavior patterns
                var strongPatterns = _data.UserModel.Patterns
                    .Where(p => p.IsActive && p.Confidence > 0.6f && p.Occurrences >= 3)
                    .Take(2)
                    .ToList();
                if (strongPatterns.Any())
                    sb.AppendLine($"[Поведінкові паттерни: {string.Join("; ", strongPatterns.Select(p => p.Description))}]");

                return sb.ToString().Trim();
            }
        }

        /// <summary>Короткий рядок стану для логів</summary>
        public string GetStatusLine()
        {
            lock (_lock)
            {
                var focus = GetFocus();
                return $"wm={_data.WorkingMemory.Count}/{CentralExecutive.MaxCapacity} " +
                       $"focus={focus?.Content[..Math.Min(30, focus.Content.Length)] ?? "none"} " +
                       $"cogload={_data.Metacognition.CognitiveLoad:F2} " +
                       $"userMood={_data.UserModel.InferredMood}({_data.UserModel.MoodConfidence:F2}) " +
                       $"intents={_data.Intents.Count(i => !i.Executed)}";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // PROCESSING PIPELINE — виклик після кожного повідомлення
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Повний когнітивний цикл після повідомлення від користувача.
        /// Оновлює WM, user model, salience, metacognition.
        /// </summary>
        public (SalienceScore salience, string cogContext) ProcessUserMessage(
            string message, string tone, string emotionalContext)
        {
            var salience = EvaluateSalience(message, emotionalContext);
            UpdateUserModel(tone, message, salience.Score);
            UpdateMetacognition(
                cognitiveLoad: message.Length > 300 ? 0.65f : 0.35f,
                internalConflict: tone is "angry" or "hurt" ? 0.4f : 0.0f
            );
            DecayWorkingMemory(0.5f); // half-minute tick після кожного msg

            var ctx = BuildCognitionContext();
            _data.LastCognitionAt = DateTime.Now;
            Save();
            return (salience, ctx);
        }

        // ══════════════════════════════════════════════════════════════════════
        // PERSISTENCE
        // ══════════════════════════════════════════════════════════════════════

        private CognitionData Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonConvert.DeserializeObject<CognitionData>(File.ReadAllText(_path)) ?? new();
            }
            catch { }
            return new();
        }

        private void Save()
        {
            try { File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented)); }
            catch { }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    }
}
