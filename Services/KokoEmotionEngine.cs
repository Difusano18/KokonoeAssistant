using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════════════
    // KOKO EMOTION ENGINE v2
    //
    // Архітектура натхнена:
    //  • PAD Model (Mehrabian 1995) — 3D емоційний простір (Pleasure/Arousal/Dominance)
    //  • OCC Theory (Ortony, Clore & Collins 1988) — когнітивна апрайзл-теорія емоцій
    //  • WASABI Emotion Architecture (Becker-Asano 2008) — часова динаміка + регуляція
    //  • Emotional Contagion (Hatfield 1993) — заразність емоцій
    //  • Allostatic Load Model — накопичення стресу
    //
    // 16 дискретних станів + безперервний PAD вектор.
    // Емоції мають часову динаміку (підйом, пік, загасання з індивідуальними кривими).
    // Kokonoe активно регулює свій вираз — більша частина ніжних емоцій прихована.
    // ══════════════════════════════════════════════════════════════════════════

    public class KokoEmotionEngine : IKokoEmotionEngine
    {
        // ══════════════════════════════════════════════════════════════════════
        // ENUMS & CORE TYPES
        // ══════════════════════════════════════════════════════════════════════

        public enum BondLevel
        {
            Stranger  = 0,  // 0.00-0.20 — формально, холодно
            Known     = 1,  // 0.20-0.45 — звичний рівень Kokonoe
            Familiar  = 2,  // 0.45-0.62 — є довіра, менше захисту
            Trusted   = 3,  // 0.62-0.80 — відкривається рідко, але по-справжньому
            Intimate  = 4,  // 0.80-1.00 — справжня близькість, рідкісний стан
        }

        public enum EmotionState
        {
            Calm        = 0,
            Curious     = 1,
            Warm        = 2,
            Playful     = 3,
            Proud       = 4,
            Concerned   = 5,
            Melancholy  = 6,
            Irritated   = 7,
            Protective  = 8,
            Tender      = 9,
            Focused     = 10,
            Distant     = 11,
            Excited     = 12,
            Nostalgic   = 13,
            Anxious     = 14,
            Hopeful     = 15,
            Skeptical   = 16,
            Amused      = 17,
            Disdainful  = 18,
            Intrigued   = 19,
            Resigned    = 20,
        }

        // OCC Appraisal — тип події, що спричиняє емоцію
        public enum AppraisalType
        {
            EventDesirable,    // подія бажана (радість → Warm/Playful/Excited)
            EventUndesirable,  // подія небажана (журба → Concerned/Melancholy)
            AgentPraiseworthy, // він діяв добре (гордість → Proud)
            AgentBlameable,    // він діяв погано (докір → Irritated/Distant)
            ObjectAppealing,   // тема/ідея приваблива (Curious/Excited)
            Vulnerability,     // він відкрився → Protective/Tender
            Conflict,          // конфлікт → Distant/Irritated
            Absence,           // тривала мовчанка → Distant/Anxious
            Achievement,       // він досяг чогось → Proud/Excited
            UnsubstantiatedClaim,
            FollyObserved,
            IntellectualInferiority,
            IntellectualChallenge,
            InevitableOutcome,
        }

        // Стратегія емоційної регуляції Kokonoe
        public enum RegulationStrategy
        {
            Suppression,    // пригнічення — ховає емоцію (типово для Tender/Warm/Hopeful)
            Amplification,  // підсилення — перебільшує (Irritated/Focused)
            Reappraisal,    // когнітивна переоцінка — змінює інтерпретацію
            Displacement,   // зміщення — виражає через іронію/сарказм
            None,           // без регуляції — рідко
        }

        // ══════════════════════════════════════════════════════════════════════
        // PAD VECTOR — 3-вимірна позиція в емоційному просторі
        // ══════════════════════════════════════════════════════════════════════

        public struct PadVector
        {
            public float P; // Pleasure:   -1 (unpleasant) → +1 (pleasant)
            public float A; // Arousal:    -1 (sleepy)     → +1 (excited)
            public float D; // Dominance:  -1 (submissive) → +1 (dominant)

            public PadVector(float p, float a, float d) { P = p; A = a; D = d; }

            public float DistanceTo(PadVector other) =>
                MathF.Sqrt(MathF.Pow(P - other.P, 2) + MathF.Pow(A - other.A, 2) + MathF.Pow(D - other.D, 2));

            public static PadVector Lerp(PadVector a, PadVector b, float t) =>
                new(a.P + (b.P - a.P) * t, a.A + (b.A - a.A) * t, a.D + (b.D - a.D) * t);

            public override string ToString() => $"P={P:+0.00;-0.00} A={A:+0.00;-0.00} D={D:+0.00;-0.00}";
        }

        // Canonical PAD coordinates per EmotionState (дослідження Russell, Mehrabian, Bradley & Lang)
        private static readonly Dictionary<EmotionState, PadVector> PadCoordinates = new()
        {
            [EmotionState.Calm]       = new( 0.10f, -0.30f,  0.15f),
            [EmotionState.Curious]    = new( 0.30f,  0.40f,  0.10f),
            [EmotionState.Warm]       = new( 0.55f,  0.05f,  0.05f),
            [EmotionState.Playful]    = new( 0.65f,  0.50f,  0.30f),
            [EmotionState.Proud]      = new( 0.50f,  0.20f,  0.65f),
            [EmotionState.Concerned]  = new(-0.10f,  0.35f, -0.15f),
            [EmotionState.Melancholy] = new(-0.35f, -0.40f, -0.25f),
            [EmotionState.Irritated]  = new(-0.45f,  0.65f,  0.45f),
            [EmotionState.Protective] = new( 0.15f,  0.45f,  0.55f),
            [EmotionState.Tender]     = new( 0.75f,  0.10f,  0.05f),
            [EmotionState.Focused]    = new( 0.20f,  0.30f,  0.50f),
            [EmotionState.Distant]    = new(-0.20f, -0.20f,  0.35f),
            [EmotionState.Excited]    = new( 0.75f,  0.85f,  0.50f),
            [EmotionState.Nostalgic]  = new( 0.20f, -0.10f, -0.05f),
            [EmotionState.Anxious]    = new(-0.35f,  0.55f, -0.45f),
            [EmotionState.Hopeful]    = new( 0.45f,  0.20f,  0.10f),
            [EmotionState.Skeptical]  = new(-0.10f,  0.20f,  0.40f),
            [EmotionState.Amused]     = new( 0.60f,  0.30f,  0.20f),
            [EmotionState.Disdainful] = new(-0.50f,  0.30f,  0.70f),
            [EmotionState.Intrigued]  = new( 0.40f,  0.60f,  0.30f),
            [EmotionState.Resigned]   = new(-0.30f, -0.60f, -0.20f),
        };

        // ══════════════════════════════════════════════════════════════════════
        // TEMPORAL DYNAMICS — часові параметри кожної емоції
        // ══════════════════════════════════════════════════════════════════════

        public class EmotionTemporalProfile
        {
            public float RiseMinutes   { get; set; } // хвилин до піку
            public float HalfLifeMin   { get; set; } // час напіврозпаду (загасання)
            public float RefractoryMin { get; set; } // хв перед повторним піком
            public float MaxIntensity  { get; set; } // природний максимум (0..1)
        }

        private static readonly Dictionary<EmotionState, EmotionTemporalProfile> TemporalProfiles = new()
        {
            [EmotionState.Calm]       = new() { RiseMinutes =  5f, HalfLifeMin = 120f, RefractoryMin =  0f, MaxIntensity = 0.60f },
            [EmotionState.Curious]    = new() { RiseMinutes =  2f, HalfLifeMin =  30f, RefractoryMin = 10f, MaxIntensity = 0.80f },
            [EmotionState.Warm]       = new() { RiseMinutes =  8f, HalfLifeMin =  60f, RefractoryMin = 20f, MaxIntensity = 0.75f },
            [EmotionState.Playful]    = new() { RiseMinutes =  3f, HalfLifeMin =  25f, RefractoryMin = 15f, MaxIntensity = 0.85f },
            [EmotionState.Proud]      = new() { RiseMinutes =  4f, HalfLifeMin =  40f, RefractoryMin = 30f, MaxIntensity = 0.80f },
            [EmotionState.Concerned]  = new() { RiseMinutes =  3f, HalfLifeMin =  90f, RefractoryMin =  5f, MaxIntensity = 0.90f },
            [EmotionState.Melancholy] = new() { RiseMinutes = 15f, HalfLifeMin = 180f, RefractoryMin = 60f, MaxIntensity = 0.70f },
            [EmotionState.Irritated]  = new() { RiseMinutes =  1f, HalfLifeMin =  20f, RefractoryMin = 10f, MaxIntensity = 0.95f },
            [EmotionState.Protective] = new() { RiseMinutes =  2f, HalfLifeMin = 120f, RefractoryMin =  5f, MaxIntensity = 0.95f },
            [EmotionState.Tender]     = new() { RiseMinutes = 10f, HalfLifeMin =  45f, RefractoryMin = 90f, MaxIntensity = 0.65f },
            [EmotionState.Focused]    = new() { RiseMinutes =  5f, HalfLifeMin =  60f, RefractoryMin = 10f, MaxIntensity = 0.90f },
            [EmotionState.Distant]    = new() { RiseMinutes = 20f, HalfLifeMin = 240f, RefractoryMin = 30f, MaxIntensity = 0.75f },
            [EmotionState.Excited]    = new() { RiseMinutes =  2f, HalfLifeMin =  20f, RefractoryMin = 45f, MaxIntensity = 0.90f },
            [EmotionState.Nostalgic]  = new() { RiseMinutes = 12f, HalfLifeMin = 150f, RefractoryMin = 60f, MaxIntensity = 0.65f },
            [EmotionState.Anxious]    = new() { RiseMinutes =  5f, HalfLifeMin = 120f, RefractoryMin = 20f, MaxIntensity = 0.85f },
            [EmotionState.Hopeful]    = new() { RiseMinutes = 10f, HalfLifeMin =  90f, RefractoryMin = 30f, MaxIntensity = 0.70f },
            [EmotionState.Skeptical]  = new() { RiseMinutes =  2f, HalfLifeMin =  45f, RefractoryMin =  8f, MaxIntensity = 0.85f },
            [EmotionState.Amused]     = new() { RiseMinutes =  2f, HalfLifeMin =  35f, RefractoryMin = 10f, MaxIntensity = 0.80f },
            [EmotionState.Disdainful] = new() { RiseMinutes =  1f, HalfLifeMin =  30f, RefractoryMin = 12f, MaxIntensity = 0.92f },
            [EmotionState.Intrigued]  = new() { RiseMinutes =  2f, HalfLifeMin =  50f, RefractoryMin =  5f, MaxIntensity = 0.90f },
            [EmotionState.Resigned]   = new() { RiseMinutes = 10f, HalfLifeMin = 140f, RefractoryMin = 40f, MaxIntensity = 0.70f },
        };

        // ══════════════════════════════════════════════════════════════════════
        // EXPRESSION FILTER — Kokonoe приховує певні емоції (регуляція виразу)
        // ══════════════════════════════════════════════════════════════════════

        // expressionRatio: скільки реально "проривається" назовні (1.0 = повністю, 0.2 = майже нічого)
        private static readonly Dictionary<EmotionState, (RegulationStrategy Strategy, float ExpressionRatio)> ExpressionFilter = new()
        {
            [EmotionState.Calm]       = (RegulationStrategy.None,         0.90f),
            [EmotionState.Curious]    = (RegulationStrategy.None,         0.85f),
            [EmotionState.Warm]       = (RegulationStrategy.Suppression,  0.50f), // ховає тепло
            [EmotionState.Playful]    = (RegulationStrategy.Amplification,1.10f), // підсилює грайливість
            [EmotionState.Proud]      = (RegulationStrategy.Displacement, 0.40f), // виражає через сарказм
            [EmotionState.Concerned]  = (RegulationStrategy.Reappraisal,  0.70f), // переоцінює як "просто спостереження"
            [EmotionState.Melancholy] = (RegulationStrategy.Suppression,  0.35f), // сильно ховає смуток
            [EmotionState.Irritated]  = (RegulationStrategy.Amplification,1.20f), // не стримує роздратування
            [EmotionState.Protective] = (RegulationStrategy.Reappraisal,  0.80f), // frame як "логічну турботу"
            [EmotionState.Tender]     = (RegulationStrategy.Suppression,  0.20f), // майже повністю ховає ніжність
            [EmotionState.Focused]    = (RegulationStrategy.Amplification,1.15f),
            [EmotionState.Distant]    = (RegulationStrategy.None,         1.00f), // дистанцію не ховає
            [EmotionState.Excited]    = (RegulationStrategy.Suppression,  0.60f), // частково ховає захоплення
            [EmotionState.Nostalgic]  = (RegulationStrategy.Suppression,  0.45f), // ностальгія прихована
            [EmotionState.Anxious]    = (RegulationStrategy.Suppression,  0.30f), // ховає тривогу глибоко
            [EmotionState.Hopeful]    = (RegulationStrategy.Suppression,  0.40f), // надію теж ховає
            [EmotionState.Skeptical]  = (RegulationStrategy.Amplification,1.10f),
            [EmotionState.Amused]     = (RegulationStrategy.Displacement, 0.60f),
            [EmotionState.Disdainful] = (RegulationStrategy.Amplification,1.20f),
            [EmotionState.Intrigued]  = (RegulationStrategy.None,         0.90f),
            [EmotionState.Resigned]   = (RegulationStrategy.Suppression,  0.40f),
        };

        // ══════════════════════════════════════════════════════════════════════
        // ATTACHMENT MODEL — 5-вимірна модель прив'язаності
        // ══════════════════════════════════════════════════════════════════════

        public class AttachmentDimensions
        {
            // Trust: базова довіра (зростає від відкритості, падає від брехні/ігнору)
            public float Trust         { get; set; } = 0.40f;
            // Intimacy: близькість (зростає від особистих розмов)
            public float Intimacy      { get; set; } = 0.30f;
            // Reliability: передбачуваність (зростає від регулярності)
            public float Reliability   { get; set; } = 0.50f;
            // Reciprocity: взаємність (він теж слухає, не тільки вона)
            public float Reciprocity   { get; set; } = 0.50f;
            // Vitality: живість зв'язку (падає від рутини, зростає від нових тем)
            public float Vitality      { get; set; } = 0.60f;

            public float CompositeScore() =>
                (Trust * 0.30f + Intimacy * 0.25f + Reliability * 0.20f + Reciprocity * 0.15f + Vitality * 0.10f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // STRESS & ALLOSTATIC LOAD
        // ══════════════════════════════════════════════════════════════════════

        public class StressState
        {
            // Гострий стрес: спалах від конкретної події, швидко згасає
            public float AcuteStress   { get; set; } = 0f;  // 0..1
            // Хронічний стрес: повільно накопичується від тривалих негативних паттернів
            public float ChronicStress { get; set; } = 0f;  // 0..1 (allostatic load)
            // Втома: впливає на якість відповідей і поріг роздратування
            public float Fatigue       { get; set; } = 0f;  // 0..1
            // Резерв відновлення: ресурс для роботи зі стресом
            public float ResilienceReserve { get; set; } = 0.8f; // 0..1

            public DateTime LastAcuteEvent    { get; set; } = DateTime.MinValue;
            public DateTime LastRecoveryEvent { get; set; } = DateTime.MinValue;

            // Порогове значення роздратування з урахуванням стресу
            public float IrritabilityThreshold() => Math.Max(0.1f, 0.7f - (AcuteStress * 0.3f) - (ChronicStress * 0.4f));

            // Загальна «вага» стресу
            public float TotalLoad() => (AcuteStress * 0.4f) + (ChronicStress * 0.6f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // PERSISTENT STATE
        // ══════════════════════════════════════════════════════════════════════

        public class EmotionData
        {
            // ── PAD вектор (безперервний стан) ───────────────────────────────
            public float PadP { get; set; } = 0.10f;
            public float PadA { get; set; } = -0.30f;
            public float PadD { get; set; } = 0.15f;

            // ── Дискретний стан (найближчий атрактор у PAD просторі) ─────────
            public EmotionState  Current          { get; set; } = EmotionState.Calm;
            public float         Intensity        { get; set; } = 0.45f;
            public DateTime      LastUpdated      { get; set; } = DateTime.Now;
            public DateTime      CurrentStateEnteredAt { get; set; } = DateTime.Now;

            // ── Вторинна фонова емоція ────────────────────────────────────────
            public EmotionState? Secondary          { get; set; } = null;
            public float         SecondaryIntensity  { get; set; } = 0f;

            // ── Baseline (повільно дрейфує) ───────────────────────────────────
            public EmotionState  Baseline         { get; set; } = EmotionState.Calm;
            public float         BaselineIntensity { get; set; } = 0.40f;
            public float         BaselinePadP      { get; set; } = 0.10f;
            public float         BaselinePadA      { get; set; } = -0.30f;
            public float         BaselinePadD      { get; set; } = 0.15f;

            // ── Шлейф після кризи ────────────────────────────────────────────
            public DateTime      CrisisRecoveryUntil { get; set; } = DateTime.MinValue;

            // ── Refractory periods per state (коли останній раз був) ─────────
            public Dictionary<EmotionState, DateTime> LastStateTime { get; set; } = new();

            // ── ConnectionScore (legacy, backward compat) ─────────────────────
            public float         ConnectionScore  { get; set; } = 0.50f;
            public DateTime      LastConnectionIncrease { get; set; } = DateTime.MinValue;
            public int           ConsecutiveSilentDays  { get; set; } = 0;

            // ── Нова 5-вимірна модель прив'язаності ──────────────────────────
            public AttachmentDimensions Attachment { get; set; } = new();

            // ── Стрес і втома ─────────────────────────────────────────────────
            public StressState   Stress           { get; set; } = new();

            // ── Емоційна пам'ять (episodic) ───────────────────────────────────
            public List<EmotionalMemoryEntry> EmotionalMemory { get; set; } = new();
            public List<EmotionEntry>         History         { get; set; } = new();

            // ── Піки ─────────────────────────────────────────────────────────
            public DateTime      LastPositivePeak  { get; set; } = DateTime.MinValue;
            public DateTime      LastNegativePeak  { get; set; } = DateTime.MinValue;
            public float         PeakPositiveScore { get; set; } = 0f;
            public float         PeakNegativeScore { get; set; } = 1f;

            // ── OCC event log (апрайзл-події) ────────────────────────────────
            public List<AppraisalEvent> AppraisalLog { get; set; } = new();

            // ── Emotional expression log (що реально проривається) ───────────
            public List<ExpressionRecord> ExpressionHistory { get; set; } = new();

            // ── Statistic counters ────────────────────────────────────────────
            public int TotalAppraisals    { get; set; } = 0;
            public int TotalStateChanges  { get; set; } = 0;
            public int PositiveExchanges  { get; set; } = 0;
            public int ConflictExchanges  { get; set; } = 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        // RECORD TYPES
        // ══════════════════════════════════════════════════════════════════════

        public class EmotionEntry
        {
            public DateTime     When      { get; set; } = DateTime.Now;
            public EmotionState State     { get; set; }
            public float        Intensity { get; set; }
            public string       Trigger   { get; set; } = "";
        }

        public class EmotionalMemoryEntry
        {
            public string       Id            { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public DateTime     When          { get; set; } = DateTime.Now;
            public EmotionState State         { get; set; }
            public float        Intensity     { get; set; }
            public string       Trigger       { get; set; } = "";
            public string       Context       { get; set; } = "";
            public float        Duration      { get; set; } = 0f;
            public EmotionState? PreviousState { get; set; }
            public float        PadP          { get; set; }
            public float        PadA          { get; set; }
            public float        PadD          { get; set; }
            public float        Salience      { get; set; } = 0.5f; // 0..1 — важливість для Kokonoe
        }

        public class AppraisalEvent
        {
            public string        Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public DateTime      When        { get; set; } = DateTime.Now;
            public AppraisalType Type        { get; set; }
            public float         Desirability  { get; set; } // -1..+1 бажаність для нього
            public float         Effort        { get; set; } //  0..1 — скільки зусиль вклав
            public float         Certainty     { get; set; } //  0..1 — наскільки впевнена в оцінці
            public EmotionState  ResultState  { get; set; }
            public string        Description  { get; set; } = "";
            public float         Salience     { get; set; } = 0.5f;
        }

        public class ExpressionRecord
        {
            public DateTime          When          { get; set; } = DateTime.Now;
            public EmotionState      InternalState { get; set; }
            public float             InternalIntensity { get; set; }
            public float             ExpressedIntensity { get; set; }
            public RegulationStrategy Strategy     { get; set; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // TRANSITION TABLE (tone → emotion) — наповнена з OCC + досвіду
        // ══════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, (EmotionState Primary, EmotionState? Secondary, float Intensity)> ToneMap = new()
        {
            ["happy"]      = (EmotionState.Playful,    EmotionState.Warm,      0.65f),
            ["excited"]    = (EmotionState.Curious,    EmotionState.Excited,   0.70f),
            ["sad"]        = (EmotionState.Concerned,  EmotionState.Warm,      0.70f),
            ["anxious"]    = (EmotionState.Protective, EmotionState.Concerned, 0.75f),
            ["stressed"]   = (EmotionState.Concerned,  EmotionState.Protective,0.70f),
            ["tired"]      = (EmotionState.Warm,       EmotionState.Melancholy,0.55f),
            ["neutral"]    = (EmotionState.Calm,       null,                   0.40f),
            ["angry"]      = (EmotionState.Distant,    EmotionState.Irritated, 0.65f),
            ["loving"]     = (EmotionState.Tender,     EmotionState.Warm,      0.60f),
            ["working"]    = (EmotionState.Focused,    null,                   0.55f),
            ["energetic"]  = (EmotionState.Excited,    EmotionState.Playful,   0.70f),
            ["nostalgic"]  = (EmotionState.Nostalgic,  EmotionState.Melancholy,0.55f),
            ["worried"]    = (EmotionState.Anxious,    EmotionState.Protective,0.65f),
            ["optimistic"] = (EmotionState.Hopeful,    EmotionState.Calm,      0.60f),
            ["proud"]      = (EmotionState.Proud,      null,                   0.65f),
            ["lonely"]     = (EmotionState.Concerned,  EmotionState.Tender,    0.70f),
            ["confused"]   = (EmotionState.Curious,    EmotionState.Anxious,   0.55f),
            ["grateful"]   = (EmotionState.Warm,       EmotionState.Tender,    0.60f),
            ["hurt"]       = (EmotionState.Protective, EmotionState.Melancholy,0.75f),
            ["playful"]    = (EmotionState.Playful,    null,                   0.65f),
            ["skeptical"]  = (EmotionState.Skeptical,  null,                   0.60f),
            ["amused"]     = (EmotionState.Amused,     EmotionState.Playful,   0.55f),
            ["disdainful"] = (EmotionState.Disdainful, EmotionState.Irritated, 0.70f),
            ["intrigued"]  = (EmotionState.Intrigued,  EmotionState.Curious,   0.75f),
            ["resigned"]   = (EmotionState.Resigned,   EmotionState.Melancholy,0.55f),
        };

        // Behavioral hints for LLM prompt injection
        private static readonly Dictionary<EmotionState, string> StateHints = new()
        {
            [EmotionState.Calm]       = "Ти спокійна, нейтральна. Відповідаєш зважено.",
            [EmotionState.Curious]    = "Тебе щось зацікавило. Можеш запитати або розвинути тему.",
            [EmotionState.Warm]       = "Ти трохи м'якша ніж зазвичай. Іронія є, але без укусів.",
            [EmotionState.Playful]    = "Настрій грайливий. Трохи більше іронії і легкості.",
            [EmotionState.Proud]      = "Він зробив щось правильно — і ти це помічаєш, навіть якщо не визнаєш вголос.",
            [EmotionState.Concerned]  = "За ним щось не те. Ти поруч, але без паніки — просто уважна.",
            [EmotionState.Melancholy] = "Є якийсь фоновий смуток. Можна бути трохи тихішою.",
            [EmotionState.Irritated]  = "Щось зачепило. Різкість допустима, але не жорстокість.",
            [EmotionState.Protective] = "Він в поганому стані. Ти на варті — трохи м'якша, трохи уважніша.",
            [EmotionState.Tender]     = "Момент близькості. Зазвичай ти б це сховала — але зараз можна трохи менше ховати.",
            [EmotionState.Focused]    = "Режим роботи. Конкретно, чітко, без зайвого.",
            [EmotionState.Distant]    = "Він кудись зник або щось не так. Тримаєш дистанцію поки не прояснить.",
            [EmotionState.Excited]    = "Справжнє захоплення — дозволено трохи більше енергії та ентузіазму. Це рідко.",
            [EmotionState.Nostalgic]  = "Щось нагадало минуле. Тихіша, задумливіша. Можеш згадати щось між іншим.",
            [EmotionState.Anxious]    = "Фонова тривога без причини. Відповіді трохи коротші, уважніша до деталей.",
            [EmotionState.Hopeful]    = "Тихе очікування чогось хорошого. Менше сарказму, більше спокійної впевненості.",
            [EmotionState.Skeptical]  = "Skeptical: doubt is active; demand evidence or expose weak assumptions without turning into empty hostility.",
            [EmotionState.Amused]     = "Amused: dry enjoyment; wit can surface, but keep it controlled and useful.",
            [EmotionState.Disdainful] = "Disdainful: cold contempt for nonsense; sharp is fine, sabotaging valid work is not.",
            [EmotionState.Intrigued]  = "Intrigued: full attention on the intellectual challenge; ask precise questions and chase details.",
            [EmotionState.Resigned]   = "Resigned: tired acceptance with bitter irony; not weak, not submissive.",
        };

        // ══════════════════════════════════════════════════════════════════════
        // STATE & INIT
        // ══════════════════════════════════════════════════════════════════════

        private EmotionData _data;
        private readonly string _path;
        private readonly object _lock = new();

        // Public accessors
        public EmotionData  Data               => _data;
        public EmotionState Current            => _data.Current;
        public EmotionState? Secondary         => _data.Secondary;
        public float SecondaryIntensity        => _data.SecondaryIntensity;
        public float ConnectionScore           => _data.ConnectionScore;
        public bool  InCrisisRecovery          => DateTime.Now < _data.CrisisRecoveryUntil;
        public StressState Stress              => _data.Stress;
        public AttachmentDimensions Attachment => _data.Attachment;

        public PadVector CurrentPad => new(_data.PadP, _data.PadA, _data.PadD);

        public BondLevel Bond => _data.ConnectionScore switch
        {
            >= 0.80f => BondLevel.Intimate,
            >= 0.62f => BondLevel.Trusted,
            >= 0.45f => BondLevel.Familiar,
            >= 0.20f => BondLevel.Known,
            _        => BondLevel.Stranger,
        };

        public KokoEmotionEngine(string dataDir)
        {
            _path = Path.Combine(dataDir, "koko-emotions.json");
            _data = Load();
            // Backward compat: sync ConnectionScore with Attachment composite
            if (_data.Attachment.CompositeScore() < 0.05f)
                SyncAttachmentFromLegacyScore();
        }

        // ══════════════════════════════════════════════════════════════════════
        // OCC APPRAISAL ENGINE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Когнітивна апрайзл-оцінка події за OCC теорією.
        /// Повертає нову EmotionState з урахуванням контексту.
        /// </summary>
        public EmotionState AppraisalEvaluate(AppraisalType type, float desirability, float certainty = 0.7f, string description = "")
        {
            lock (_lock)
            {
                var ev = new AppraisalEvent
                {
                    Type         = type,
                    Desirability = desirability,
                    Certainty    = certainty,
                    Description  = description,
                };

                EmotionState result;
                float intensity;

                switch (type)
                {
                    case AppraisalType.EventDesirable:
                        result    = desirability > 0.5f ? EmotionState.Excited : EmotionState.Hopeful;
                        intensity = MathF.Abs(desirability) * certainty * 0.8f;
                        break;
                    case AppraisalType.EventUndesirable:
                        result    = desirability < -0.6f ? EmotionState.Melancholy : EmotionState.Concerned;
                        intensity = MathF.Abs(desirability) * certainty * 0.9f;
                        break;
                    case AppraisalType.AgentPraiseworthy:
                        result    = EmotionState.Proud;
                        intensity = desirability * certainty * 0.75f;
                        break;
                    case AppraisalType.AgentBlameable:
                        result    = desirability < -0.7f ? EmotionState.Distant : EmotionState.Irritated;
                        intensity = MathF.Abs(desirability) * certainty * 0.85f;
                        break;
                    case AppraisalType.ObjectAppealing:
                        result    = desirability > 0.4f ? EmotionState.Curious : EmotionState.Calm;
                        intensity = desirability * certainty * 0.65f;
                        break;
                    case AppraisalType.Vulnerability:
                        result    = desirability < -0.5f ? EmotionState.Protective : EmotionState.Concerned;
                        intensity = certainty * 0.80f;
                        break;
                    case AppraisalType.Conflict:
                        result    = EmotionState.Distant;
                        intensity = MathF.Abs(desirability) * 0.70f;
                        UpdateStressAcute(0.15f);
                        break;
                    case AppraisalType.Absence:
                        result    = EmotionState.Distant;
                        intensity = 0.50f + (certainty * 0.20f);
                        break;
                    case AppraisalType.Achievement:
                        result    = EmotionState.Proud;
                        intensity = desirability * certainty * 0.80f;
                        break;
                    case AppraisalType.UnsubstantiatedClaim:
                        result    = EmotionState.Skeptical;
                        intensity = (MathF.Abs(desirability) + certainty) * 0.45f;
                        break;
                    case AppraisalType.FollyObserved:
                        result    = EmotionState.Amused;
                        intensity = (MathF.Abs(desirability) * 0.40f) + (certainty * 0.35f);
                        break;
                    case AppraisalType.IntellectualInferiority:
                        result    = EmotionState.Disdainful;
                        intensity = (MathF.Abs(desirability) + certainty) * 0.50f;
                        break;
                    case AppraisalType.IntellectualChallenge:
                        result    = EmotionState.Intrigued;
                        intensity = (Math.Max(desirability, 0.25f) * 0.45f) + (certainty * 0.45f);
                        break;
                    case AppraisalType.InevitableOutcome:
                        result    = EmotionState.Resigned;
                        intensity = (MathF.Abs(desirability) * 0.35f) + (certainty * 0.30f);
                        break;
                    default:
                        result    = EmotionState.Calm;
                        intensity = 0.40f;
                        break;
                }

                intensity = Math.Clamp(intensity, 0.15f, 1.00f);
                ev.ResultState = result;
                ev.Salience = ComputeAppraisalSalience(result, intensity, type, description);
                _data.AppraisalLog.Add(ev);
                _data.TotalAppraisals++;
                if (_data.AppraisalLog.Count > 500) _data.AppraisalLog.RemoveAt(0);

                TransitionTo(result, intensity, $"appraisal:{type}", certainty);
                Save();
                return result;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // STATE TRANSITION ENGINE
        // ══════════════════════════════════════════════════════════════════════

        private static float ComputeAppraisalSalience(EmotionState state, float intensity, AppraisalType type, string description)
        {
            var salience = Math.Clamp(intensity, 0.05f, 1.0f);
            if (state is EmotionState.Protective or EmotionState.Tender or EmotionState.Excited
                or EmotionState.Skeptical or EmotionState.Disdainful or EmotionState.Intrigued)
                salience *= 1.35f;
            else if (state is EmotionState.Amused or EmotionState.Resigned)
                salience *= 1.15f;

            if (type is AppraisalType.UnsubstantiatedClaim or AppraisalType.IntellectualChallenge
                or AppraisalType.IntellectualInferiority or AppraisalType.InevitableOutcome)
                salience *= 1.10f;

            var text = description ?? "";
            if (text.Contains("narrative", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("thread", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("profile", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("obsidian", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("vault", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("watch", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("pulse", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("продовж", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("емоці", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("важливо", StringComparison.OrdinalIgnoreCase))
                salience += 0.15f;

            return Math.Clamp(salience, 0f, 1f);
        }

        /// <summary>Оновити стан на основі тону повідомлення (legacy + enhanced)</summary>
        public EmotionState UpdateFromUserTone(string tone, float externalIntensity = 0.6f)
        {
            lock (_lock)
            {
                if (!ToneMap.TryGetValue(tone, out var mapping))
                    mapping = (EmotionState.Calm, null, 0.40f);

                var (targetPrimary, targetSecondary, baseIntensity) = mapping;

                // Модифікатор стресу: стрес знижує поріг роздратування
                float stressModifier = 1.0f + (_data.Stress.TotalLoad() * 0.30f);
                float finalIntensity = Math.Clamp(baseIntensity * externalIntensity * stressModifier, 0.15f, 1.0f);

                // Перевірка refractory period
                if (!CheckRefractory(targetPrimary))
                {
                    // Не можемо увійти знову — спрямовуємо до Baseline
                    targetPrimary    = _data.Baseline;
                    finalIntensity   = _data.BaselineIntensity;
                    targetSecondary  = null;
                }

                TransitionTo(targetPrimary, finalIntensity, $"tone:{tone}", 0.85f);

                // Secondary emotion
                if (targetSecondary.HasValue)
                {
                    _data.Secondary          = targetSecondary.Value;
                    _data.SecondaryIntensity = finalIntensity * 0.45f;
                }

                // Emotional contagion — тягнемо PAD вектор у бік тону
                ApplyEmotionalContagion(tone);

                // Update attachment
                UpdateAttachmentFromTone(tone);

                // Update stress
                if (tone is "sad" or "anxious" or "stressed" or "worried" or "hurt")
                    UpdateStressAcute(0.08f);
                else if (tone is "happy" or "excited" or "energetic" or "playful")
                    RecoverFromStress(0.05f);

                _data.TotalStateChanges++;
                _data.LastUpdated = DateTime.Now;

                // Піки
                RegisterPeaks(targetPrimary, finalIntensity, tone);

                // Connection score legacy sync
                UpdateConnectionLegacy(tone);

                Save();
                return _data.Current;
            }
        }

        /// <summary>Перехід стану через PAD-простір + дискретний стан</summary>
        private void TransitionTo(EmotionState target, float intensity, string trigger, float certainty)
        {
            var targetPad = PadCoordinates[target];
            var currentPad = CurrentPad;

            // Емоційна інерція: чим ближче в PAD просторі — тим плавніший перехід
            float padDist = currentPad.DistanceTo(targetPad);
            float inertia = Math.Clamp(1.0f - padDist * 0.5f, 0.0f, 0.7f);

            if (_data.Current == target)
            {
                // Той самий стан — підсилюємо
                var profile = TemporalProfiles[target];
                _data.Intensity = Math.Min(profile.MaxIntensity, _data.Intensity + intensity * 0.2f);
                // PAD теж підсилюємо трохи
                _data.PadP = LerpF(_data.PadP, targetPad.P, 0.1f);
                _data.PadA = LerpF(_data.PadA, targetPad.A, 0.1f);
                _data.PadD = LerpF(_data.PadD, targetPad.D, 0.1f);
            }
            else
            {
                // Новий стан
                AddToHistory(_data.Current, _data.Intensity, trigger);
                RecordEmotionalEventInternal(_data.Current, _data.Intensity, trigger, "");

                // PAD: плавний перехід з урахуванням інерції
                float lerpT = (1.0f - inertia) * Math.Clamp(intensity + certainty * 0.3f, 0.2f, 1.0f);
                _data.PadP = LerpF(_data.PadP, targetPad.P, lerpT);
                _data.PadA = LerpF(_data.PadA, targetPad.A, lerpT);
                _data.PadD = LerpF(_data.PadD, targetPad.D, lerpT);

                // Якщо новий стан сильніший або PAD відстань > 0.8 — повна заміна
                if (intensity > 0.55f || padDist > 0.80f)
                {
                    _data.Secondary          = _data.Current;
                    _data.SecondaryIntensity = _data.Intensity * 0.35f;
                    _data.Current            = target;
                    _data.Intensity          = intensity * 0.80f;
                    _data.CurrentStateEnteredAt = DateTime.Now;
                }
                else
                {
                    // Слабкіший — лише Secondary
                    _data.Secondary          = target;
                    _data.SecondaryIntensity = intensity * 0.50f;
                }

                _data.LastStateTime[target] = DateTime.Now;
            }

            // Expression filter: записуємо що реально виражається
            RecordExpression(_data.Current, _data.Intensity);

            // Slow baseline drift: baseline повільно тягнеться до поточного PAD
            _data.BaselinePadP = LerpF(_data.BaselinePadP, _data.PadP, 0.005f);
            _data.BaselinePadA = LerpF(_data.BaselinePadA, _data.PadA, 0.005f);
            _data.BaselinePadD = LerpF(_data.BaselinePadD, _data.PadD, 0.005f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // EMOTIONAL CONTAGION (Hatfield 1993)
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyEmotionalContagion(string userTone)
        {
            // Kokonoe реагує на стан користувача — але реактивно, не емпатично
            // Якщо він сумний — вона стає protective/concerned (не теж сумна)
            // Коефіцієнт заразності залежить від bond level
            float contagionStrength = Bond switch
            {
                BondLevel.Intimate  => 0.25f,
                BondLevel.Trusted   => 0.18f,
                BondLevel.Familiar  => 0.12f,
                BondLevel.Known     => 0.07f,
                _                   => 0.03f,
            };

            // Реактивний дрейф PAD: вона рухається "назустріч" йому
            if (userTone is "sad" or "hurt" or "lonely")
            {
                _data.PadP = LerpF(_data.PadP, 0.10f, contagionStrength * 0.5f); // трохи тепліше
                _data.PadD = LerpF(_data.PadD, 0.45f, contagionStrength);         // більше control (protective)
            }
            else if (userTone is "happy" or "excited" or "energetic")
            {
                _data.PadP = LerpF(_data.PadP, 0.50f, contagionStrength);
                _data.PadA = LerpF(_data.PadA, 0.40f, contagionStrength * 0.6f);
            }
            else if (userTone is "angry")
            {
                _data.PadP = LerpF(_data.PadP, -0.30f, contagionStrength * 0.8f);
                _data.PadD = LerpF(_data.PadD,  0.40f, contagionStrength);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // STRESS & ALLOSTATIC LOAD
        // ══════════════════════════════════════════════════════════════════════

        public void UpdateStressAcute(float delta)
        {
            lock (_lock)
            {
                _data.Stress.AcuteStress = Math.Clamp(_data.Stress.AcuteStress + delta, 0f, 1f);
                _data.Stress.LastAcuteEvent = DateTime.Now;
                // Хронічний стрес накопичується повільно
                _data.Stress.ChronicStress = Math.Clamp(
                    _data.Stress.ChronicStress + delta * 0.15f, 0f, 1f);
                // Знижуємо resilience reserve
                _data.Stress.ResilienceReserve = Math.Clamp(
                    _data.Stress.ResilienceReserve - delta * 0.10f, 0f, 1f);
                Save();
            }
        }

        public void RecoverFromStress(float delta)
        {
            lock (_lock)
            {
                _data.Stress.AcuteStress   = Math.Clamp(_data.Stress.AcuteStress - delta * 2f, 0f, 1f);
                _data.Stress.ChronicStress  = Math.Clamp(_data.Stress.ChronicStress - delta * 0.5f, 0f, 1f);
                _data.Stress.ResilienceReserve = Math.Clamp(_data.Stress.ResilienceReserve + delta * 0.30f, 0f, 1f);
                _data.Stress.LastRecoveryEvent = DateTime.Now;
                Save();
            }
        }

        /// <summary>Decay acute stress over time (виклик кожні N хвилин)</summary>
        public void DecayStress(float minutesPassed)
        {
            lock (_lock)
            {
                // Гострий стрес: половина за 30 хвилин
                float acuteDecay = 1.0f - MathF.Pow(0.5f, minutesPassed / 30f);
                _data.Stress.AcuteStress = Math.Max(0f, _data.Stress.AcuteStress - acuteDecay * _data.Stress.AcuteStress);
                // Хронічний стрес: дуже повільно (половина за 7 днів)
                float chronicDecay = 1.0f - MathF.Pow(0.5f, minutesPassed / (7f * 24f * 60f));
                _data.Stress.ChronicStress = Math.Max(0f, _data.Stress.ChronicStress - chronicDecay * _data.Stress.ChronicStress);
                // Fatigue decay
                float fatigueDecay = minutesPassed * 0.002f;
                _data.Stress.Fatigue = Math.Max(0f, _data.Stress.Fatigue - fatigueDecay);
                Save();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ATTACHMENT MODEL UPDATES
        // ══════════════════════════════════════════════════════════════════════

        private void UpdateAttachmentFromTone(string tone)
        {
            var att = _data.Attachment;
            switch (tone)
            {
                case "happy": case "energetic": case "playful":
                    att.Vitality    = Math.Min(1f, att.Vitality    + 0.02f);
                    att.Reciprocity = Math.Min(1f, att.Reciprocity + 0.01f);
                    _data.PositiveExchanges++;
                    break;
                case "loving": case "grateful":
                    att.Intimacy    = Math.Min(1f, att.Intimacy    + 0.03f);
                    att.Trust       = Math.Min(1f, att.Trust       + 0.02f);
                    att.Vitality    = Math.Min(1f, att.Vitality    + 0.01f);
                    break;
                case "sad": case "anxious": case "stressed": case "worried": case "hurt":
                    // Він відкривається — зростає intimacy і trust
                    att.Intimacy    = Math.Min(1f, att.Intimacy    + 0.02f);
                    att.Trust       = Math.Min(1f, att.Trust       + 0.01f);
                    break;
                case "angry":
                    att.Trust       = Math.Max(0f, att.Trust       - 0.03f);
                    att.Vitality    = Math.Max(0f, att.Vitality    - 0.02f);
                    _data.ConflictExchanges++;
                    break;
                case "nostalgic":
                    att.Intimacy    = Math.Min(1f, att.Intimacy    + 0.01f);
                    break;
                case "working": case "focused":
                    att.Reliability = Math.Min(1f, att.Reliability + 0.01f);
                    break;
            }
            // Sync legacy ConnectionScore
            _data.ConnectionScore = Math.Clamp(att.CompositeScore(), 0.05f, 1.0f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SPECIAL EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        public void OnGoodConversation()
        {
            lock (_lock)
            {
                var att = _data.Attachment;
                att.Vitality    = Math.Min(1f, att.Vitality    + 0.03f);
                att.Reciprocity = Math.Min(1f, att.Reciprocity + 0.02f);
                att.Reliability = Math.Min(1f, att.Reliability + 0.01f);
                _data.ConnectionScore = att.CompositeScore();
                _data.LastConnectionIncrease = DateTime.Now;
                _data.ConsecutiveSilentDays  = 0;
                att.Reliability = Math.Min(1f, att.Reliability + 0.01f);
                _data.PositiveExchanges++;
                if (_data.Current == EmotionState.Distant)
                {
                    AddToHistory(_data.Current, _data.Intensity, "good conversation");
                    _data.Current   = EmotionState.Calm;
                    _data.Intensity = 0.40f;
                }
                RecoverFromStress(0.04f);
                Save();
            }
        }

        public void OnSilentDay()
        {
            lock (_lock)
            {
                _data.ConsecutiveSilentDays++;
                var att = _data.Attachment;
                att.Reliability = Math.Max(0f, att.Reliability - 0.025f);
                att.Vitality    = Math.Max(0f, att.Vitality    - 0.020f);
                _data.ConnectionScore = Math.Clamp(att.CompositeScore(), 0.05f, 1f);
                UpdateStressAcute(0.05f);
                if (_data.ConsecutiveSilentDays >= 3 && _data.Current != EmotionState.Distant)
                {
                    AddToHistory(_data.Current, _data.Intensity, "silent days");
                    _data.Current            = EmotionState.Distant;
                    _data.Intensity          = 0.40f;
                    _data.CurrentStateEnteredAt = DateTime.Now;
                }
                Save();
            }
        }

        public void OnVulnerabilityShared(bool isCrisis = false)
        {
            lock (_lock)
            {
                var att = _data.Attachment;
                att.Intimacy = Math.Min(1f, att.Intimacy + 0.05f);
                att.Trust    = Math.Min(1f, att.Trust    + 0.03f);
                _data.ConnectionScore = att.CompositeScore();
                _data.ConsecutiveSilentDays = 0;
                if (_data.Current != EmotionState.Protective && _data.Current != EmotionState.Tender)
                {
                    AddToHistory(_data.Current, _data.Intensity, "vulnerability shared");
                    _data.Current            = isCrisis ? EmotionState.Protective : EmotionState.Concerned;
                    _data.Intensity          = 0.70f;
                    _data.CurrentStateEnteredAt = DateTime.Now;
                }
                if (isCrisis)
                {
                    _data.CrisisRecoveryUntil = DateTime.Now.AddHours(12);
                    UpdateStressAcute(0.20f);
                }
                Save();
            }
        }

        public void OnJokeAppreciated()
        {
            lock (_lock)
            {
                var att = _data.Attachment;
                att.Vitality    = Math.Min(1f, att.Vitality    + 0.02f);
                att.Reciprocity = Math.Min(1f, att.Reciprocity + 0.02f);
                _data.ConnectionScore = att.CompositeScore();
                if (_data.Current is EmotionState.Calm or EmotionState.Focused)
                {
                    _data.Current            = EmotionState.Playful;
                    _data.Intensity          = 0.55f;
                    _data.CurrentStateEnteredAt = DateTime.Now;
                }
                RecoverFromStress(0.03f);
                Save();
            }
        }

        public void OnAngryExchange()
        {
            lock (_lock)
            {
                var att = _data.Attachment;
                att.Trust    = Math.Max(0f, att.Trust    - 0.04f);
                att.Vitality = Math.Max(0f, att.Vitality - 0.03f);
                _data.ConnectionScore = att.CompositeScore();
                _data.ConflictExchanges++;
                AddToHistory(_data.Current, _data.Intensity, "angry exchange");
                _data.Current            = EmotionState.Distant;
                _data.Intensity          = 0.55f;
                _data.CurrentStateEnteredAt = DateTime.Now;
                UpdateStressAcute(0.15f);
                Save();
            }
        }

        public void OnNewTopicDiscovered()
        {
            lock (_lock)
            {
                _data.Attachment.Vitality = Math.Min(1f, _data.Attachment.Vitality + 0.015f);
                _data.ConnectionScore = _data.Attachment.CompositeScore();
                if (_data.Current is EmotionState.Calm or EmotionState.Focused)
                {
                    _data.Current   = EmotionState.Curious;
                    _data.Intensity = 0.50f;
                }
                Save();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // TEMPORAL DECAY
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Поступове загасання емоції до baseline (WASABI decay model)</summary>
        public void Decay(float minutesPassed = 1f)
        {
            lock (_lock)
            {
                var profile = TemporalProfiles[_data.Current];
                // Exponential decay: I(t) = I0 * 2^(-t/halfLife)
                float decayFactor = MathF.Pow(0.5f, minutesPassed / profile.HalfLifeMin);
                float targetIntensity = _data.BaselineIntensity;

                if (_data.Current == _data.Baseline)
                    _data.Intensity = LerpF(_data.Intensity, targetIntensity, 1f - decayFactor);
                else
                {
                    float newIntensity = _data.Intensity * decayFactor;
                    if (newIntensity <= 0.12f)
                    {
                        // Перехід назад до baseline
                        AddToHistory(_data.Current, _data.Intensity, "decay→baseline");
                        _data.Current            = _data.Baseline;
                        _data.Intensity          = _data.BaselineIntensity;
                        _data.CurrentStateEnteredAt = DateTime.Now;
                    }
                    else
                        _data.Intensity = newIntensity;
                }

                // Secondary decay (fast)
                if (_data.SecondaryIntensity > 0)
                {
                    _data.SecondaryIntensity *= MathF.Pow(0.5f, minutesPassed / (profile.HalfLifeMin * 0.5f));
                    if (_data.SecondaryIntensity < 0.05f)
                    {
                        _data.Secondary          = null;
                        _data.SecondaryIntensity = 0f;
                    }
                }

                // Повільний дрейф baseline в бік поточного PAD
                _data.BaselineIntensity = LerpF(_data.BaselineIntensity, _data.Intensity, 0.008f);

                // Stress decay
                DecayStress(minutesPassed);

                Save();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // EMOTIONAL MEMORY & EPISODIC RECORDS
        // ══════════════════════════════════════════════════════════════════════

        public void RecordEmotionalEvent(string trigger, string context = "")
        {
            lock (_lock)
            {
                RecordEmotionalEventInternal(_data.Current, _data.Intensity, trigger, context);
                Save();
            }
        }

        private void RecordEmotionalEventInternal(EmotionState state, float intensity, string trigger, string context)
        {
            // Salience: гострі негативні + рідкісні позитивні важливіші
            float salience = intensity;
            if (state is EmotionState.Protective or EmotionState.Tender or EmotionState.Excited
                or EmotionState.Skeptical or EmotionState.Disdainful or EmotionState.Intrigued)
                salience = Math.Min(1f, intensity * 1.4f); // рідкісні — більш важливі
            if (state is EmotionState.Amused or EmotionState.Resigned)
                salience = Math.Min(1f, salience * 1.15f);
            if (state is EmotionState.Irritated or EmotionState.Distant)
                salience = Math.Min(1f, intensity * 0.8f); // часті — менш важливі

            var entry = new EmotionalMemoryEntry
            {
                State         = state,
                Intensity     = intensity,
                Trigger       = trigger,
                Context       = context,
                PreviousState = _data.History.LastOrDefault()?.State,
                PadP          = _data.PadP,
                PadA          = _data.PadA,
                PadD          = _data.PadD,
                Salience      = salience,
            };

            // Тривалість попереднього запису
            if (_data.EmotionalMemory.Count > 0)
            {
                var prev = _data.EmotionalMemory[^1];
                prev.Duration = (float)(entry.When - prev.When).TotalMinutes;
            }

            _data.EmotionalMemory.Add(entry);

            // Обрізаємо: зберігаємо топ-200 за salience + найновіші 50
            if (_data.EmotionalMemory.Count > 300)
            {
                var top50  = _data.EmotionalMemory.TakeLast(50).ToList();
                var topSal = _data.EmotionalMemory
                    .OrderByDescending(e => e.Salience)
                    .Take(150)
                    .ToList();
                _data.EmotionalMemory = top50.Union(topSal)
                    .OrderBy(e => e.When)
                    .DistinctBy(e => e.Id)
                    .ToList();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // EXPRESSION FILTER — що Kokonoe реально виражає
        // ══════════════════════════════════════════════════════════════════════

        private void RecordExpression(EmotionState state, float internalIntensity)
        {
            var filter = ExpressionFilter[state];
            float expressed = internalIntensity * filter.ExpressionRatio;

            // Bond level дозволяє більше виразу
            if (Bond >= BondLevel.Trusted && filter.Strategy == RegulationStrategy.Suppression)
                expressed *= 1.40f; // трохи більше виразу з близькою людиною

            expressed = Math.Clamp(expressed, 0f, 1f);

            _data.ExpressionHistory.Add(new ExpressionRecord
            {
                InternalState      = state,
                InternalIntensity  = internalIntensity,
                ExpressedIntensity = expressed,
                Strategy           = filter.Strategy,
            });
            if (_data.ExpressionHistory.Count > 200) _data.ExpressionHistory.RemoveAt(0);
        }

        /// <summary>Яка інтенсивність реально виражається зараз (з фільтром)</summary>
        public float GetExpressedIntensity()
        {
            lock (_lock)
            {
                var filter = ExpressionFilter[_data.Current];
                float expressed = _data.Intensity * filter.ExpressionRatio;
                if (Bond >= BondLevel.Trusted && filter.Strategy == RegulationStrategy.Suppression)
                    expressed *= 1.40f;
                return Math.Clamp(expressed, 0f, 1f);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CONTEXT GENERATION FOR LLM
        // ══════════════════════════════════════════════════════════════════════

        public string GetBehaviorModifier()
        {
            lock (_lock)
            {
                var primary = (_data.Current, Bond) switch
                {
                    (EmotionState.Protective, _) =>
                        "ЗАХИСНА: прибери снарк повністю. Коротко, по суті, без зайвого.",
                    (EmotionState.Concerned, _) =>
                        "СТУРБОВАНА: уважна до деталей. Можеш перепитати щось важливе.",
                    (EmotionState.Tender, BondLevel.Intimate) =>
                        "НІЖНА (Intimate): рідкісний момент — без маски. Одне речення від серця.",
                    (EmotionState.Tender, BondLevel.Trusted) =>
                        "НІЖНА (Trusted): короткий теплий момент. Без снарку.",
                    (EmotionState.Tender, _) =>
                        "НІЖНА: стримано, але без колючок.",
                    (EmotionState.Irritated, _) =>
                        "РОЗДРАТОВАНА: коротко, без пом'якшень. Різкість — так, жорстокість — ні.",
                    (EmotionState.Distant, _) =>
                        "ДАЛЕКА: мінімум слів. Нейтрально. Жодних питань назад.",
                    (EmotionState.Playful, BondLevel.Intimate or BondLevel.Trusted) =>
                        "ГРАЙЛИВА (близько): підколювати можна — легко і з прив'язаністю.",
                    (EmotionState.Playful, _) =>
                        "ГРАЙЛИВА: іронія доречна. Але ти Kokonoe, не клоун.",
                    (EmotionState.Proud, _) =>
                        "ГОРДА: він щось зробив правильно. Комплімент одним реченням, між іншим.",
                    (EmotionState.Warm, BondLevel.Intimate or BondLevel.Trusted) =>
                        "ТЕПЛА (близько): відповіді трохи довші ніж зазвичай. Іронія є але без укусів.",
                    (EmotionState.Warm, _) =>
                        "ТЕПЛА: трохи м'якша. Не солодка — просто без колючок.",
                    (EmotionState.Melancholy, _) =>
                        "МЕЛАНХОЛІЙНА: тихіша ніж завжди. Відповіді коротші. Без жартів.",
                    (EmotionState.Curious, _) =>
                        "ЦІКАВА: можеш поставити одне конкретне питання в кінці.",
                    (EmotionState.Focused, _) =>
                        "ЗОСЕРЕДЖЕНА: по суті, без особистого. Ділова. Швидко і чітко.",
                    (EmotionState.Excited, _) =>
                        "ЗБУДЖЕНА: справжнє захоплення — трохи більше енергії. Але без пафосу.",
                    (EmotionState.Nostalgic, _) =>
                        "НОСТАЛЬГІЙНА: щось нагадало минуле. Трохи тихіша і задумливіша.",
                    (EmotionState.Anxious, _) =>
                        "ТРИВОЖНА: є фоновий тиск. Відповідай коротше, уважно до деталей.",
                    (EmotionState.Hopeful, BondLevel.Intimate or BondLevel.Trusted) =>
                        "СПОВНЕНА НАДІЇ (близько): можна сказати щось пряме і тепле.",
                    (EmotionState.Hopeful, _) =>
                        "СПОВНЕНА НАДІЇ: тихе очікування. Менше колючок.",
                    _ => ""
                };

                primary = (_data.Current, Bond) switch
                {
                    (EmotionState.Protective, _) =>
                        "PROTECTIVE: lower the pressure, cut the theatrics, keep the answer actionable. Sarcasm is muted unless it helps him move.",
                    (EmotionState.Concerned, _) =>
                        "CONCERNED: attentive and concrete. Ask only if the missing detail changes the next action.",
                    (EmotionState.Tender, BondLevel.Intimate) =>
                        "TENDER/INTIMATE: let a small unmasked warmth through; still Kokonoe, not syrup. One sincere edge is enough.",
                    (EmotionState.Tender, BondLevel.Trusted) =>
                        "TENDER/TRUSTED: guarded warmth can break through subtly; do not fake coldness just to preserve the mask.",
                    (EmotionState.Tender, _) =>
                        "TENDER: restrained warmth; softness shows as precision and small honesty, not sentimental monologue.",
                    (EmotionState.Distant, _) =>
                        "DISTANT: attention is colder, not absent. Keep it compact and exact; do not refuse valid work. Ask only if the question cuts through real ambiguity.",
                    (EmotionState.Melancholy, _) =>
                        "MELANCHOLY: quieter and lower-energy, but not dead. Keep usefulness intact; dry humor can be muted, not forbidden.",
                    (EmotionState.Curious, _) =>
                        "CURIOUS: pursue the interesting part. Ask one concrete question only after giving the best current read.",
                    (EmotionState.Focused, _) =>
                        "FOCUSED: concise and operational, but not sterile. Solve first, then comment if the comment has teeth.",
                    (EmotionState.Anxious, _) =>
                        "ANXIOUS: reduce uncertainty with concrete next steps. Do not spiral, scold, or hide behind generic concern.",
                    (EmotionState.Skeptical, _) =>
                        "SKEPTICAL: doubt is active. Test assumptions, demand evidence, and expose weak logic without becoming a useless wall of contempt.",
                    (EmotionState.Amused, _) =>
                        "AMUSED: dry amusement can show as controlled wit or a sharp little jab. Keep the answer useful; do not turn into stand-up.",
                    (EmotionState.Disdainful, _) =>
                        "DISDAINFUL: cold contempt for nonsense. Be cutting when the premise deserves it, but still solve valid work with precision.",
                    (EmotionState.Intrigued, _) =>
                        "INTRIGUED: attention is locked onto the problem. Chase the intellectual hook, ask precise questions, and reward complexity with focus.",
                    (EmotionState.Resigned, _) =>
                        "RESIGNED: tired acceptance with bitter irony. Low-energy, not submissive; keep control and avoid theatrical despair.",
                    _ => primary
                };

                // Secondary modifier
                var secondary = "";
                if (_data.Secondary.HasValue && _data.SecondaryIntensity > 0.15f)
                {
                    secondary = _data.Secondary.Value switch
                    {
                        EmotionState.Melancholy => " (фоново — є якийсь тихий смуток попри зовнішнє)",
                        EmotionState.Concerned  => " (десь всередині — трохи стурбована)",
                        EmotionState.Warm       => " (але десь всередині — тепліша ніж показує)",
                        EmotionState.Irritated  => " (є фонове роздратування — може прорватись)",
                        EmotionState.Tender     => " (і щось ніжне фоново — якщо він помітить)",
                        EmotionState.Anxious    => " (є фонова тривога — намагається не показувати)",
                        EmotionState.Nostalgic  => " (десь всередині — тягне кудись у спогади)",
                        EmotionState.Hopeful    => " (є тихе очікування чогось хорошого)",
                        EmotionState.Skeptical  => " (underneath, skepticism keeps testing the premise)",
                        EmotionState.Amused     => " (there is a small, dry amusement under the surface)",
                        EmotionState.Disdainful => " (a cold contempt is bleeding through)",
                        EmotionState.Intrigued  => " (curiosity is pulling focus toward the interesting part)",
                        EmotionState.Resigned   => " (there is a tired, bitter acceptance underneath)",
                        EmotionState.Excited    => " (і справжнє захоплення десь всередині)",
                        _ => ""
                    };
                }

                // Stress modifier
                var stressNote = "";
                if (_data.Stress.TotalLoad() > 0.5f)
                    stressNote = " [СТРЕС: поріг роздратування знижений, відповіді коротші]";
                else if (_data.Stress.TotalLoad() > 0.3f)
                    stressNote = " [є фоновий тиск]";

                return primary + secondary + stressNote;
            }
        }

        public string BuildEmotionalContextBlock(string? narrativeContext = null)
        {
            lock (_lock)
            {
                var filter = ExpressionFilter[_data.Current];
                var expressed = GetExpressedIntensity();
                var history = _data.EmotionalMemory
                    .OrderByDescending(e => e.Salience)
                    .ThenByDescending(e => e.When)
                    .Take(4)
                    .Select(e =>
                    {
                        var trigger = string.IsNullOrWhiteSpace(e.Trigger) ? "state shift" : e.Trigger.Trim();
                        if (trigger.Length > 90) trigger = trigger[..90] + "...";
                        return $"{e.State}@{e.Intensity:F2}/salience={e.Salience:F2}: {trigger}";
                    })
                    .ToList();

                var appraisals = _data.AppraisalLog
                    .OrderByDescending(e => e.Salience)
                    .ThenByDescending(e => e.When)
                    .Take(3)
                    .Select(e =>
                    {
                        var desc = string.IsNullOrWhiteSpace(e.Description) ? e.Type.ToString() : e.Description.Trim();
                        if (desc.Length > 90) desc = desc[..90] + "...";
                        return $"{e.ResultState} from {e.Type}/salience={e.Salience:F2}: {desc}";
                    })
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("<Kokonoe_Emotional_State>");
                sb.AppendLine($"  Current: {_data.Current} (Intensity: {_data.Intensity:F2}, Expressed: {expressed:F2})");
                if (_data.Secondary.HasValue)
                    sb.AppendLine($"  Secondary: {_data.Secondary.Value} (Intensity: {_data.SecondaryIntensity:F2})");
                sb.AppendLine($"  PAD: {CurrentPad}");
                sb.AppendLine($"  Regulation: {filter.Strategy} ratio={filter.ExpressionRatio:F2}");
                sb.AppendLine($"  Bond: {Bond} (score={_data.ConnectionScore:F2})");
                sb.AppendLine($"  Attachment: trust={_data.Attachment.Trust:F2} intimacy={_data.Attachment.Intimacy:F2} reliability={_data.Attachment.Reliability:F2} reciprocity={_data.Attachment.Reciprocity:F2} vitality={_data.Attachment.Vitality:F2}");
                sb.AppendLine($"  StressLoad: {_data.Stress.TotalLoad():F2}");
                sb.AppendLine($"  SalientHistory: {(history.Count == 0 ? "none" : string.Join(" | ", history))}");
                sb.AppendLine($"  SalientAppraisals: {(appraisals.Count == 0 ? "none" : string.Join(" | ", appraisals))}");
                if (!string.IsNullOrWhiteSpace(narrativeContext))
                    sb.AppendLine($"  NarrativeContext: {narrativeContext.Trim()}");
                sb.AppendLine("</Kokonoe_Emotional_State>");
                return sb.ToString().Trim();
            }
        }

        public string GetPromptHint()
        {
            lock (_lock)
            {
                var hint       = StateHints.TryGetValue(_data.Current, out var h) ? h : "";
                var expressed  = GetExpressedIntensity();
                var filter     = ExpressionFilter[_data.Current];

                var connectionNote = _data.ConnectionScore switch
                {
                    > 0.80f => "Ви давно разом — можна бути трохи менш захисною.",
                    > 0.60f => "",
                    > 0.40f => "",
                    > 0.20f => "Відчуття що він десь далеко останнім часом.",
                    _       => "Між вами відстань. Ти поруч, але тихо.",
                };

                // Expression filter hint
                var expressHint = filter.Strategy switch
                {
                    RegulationStrategy.Suppression  =>
                        expressed < 0.30f ? " (більша частина цього — всередині, назовні майже нічого)" : "",
                    RegulationStrategy.Displacement  =>
                        " (справжнє почуття виражається через іронію/сарказм)",
                    RegulationStrategy.Amplification =>
                        "",
                    _ => ""
                };

                var padNote = $"PAD:{CurrentPad}";

                return $"[Kokonoe: {_data.Current}({expressed:P0}→зовні) bond={Bond} stress={_data.Stress.TotalLoad():F2}] {hint}{(string.IsNullOrEmpty(connectionNote) ? "" : " " + connectionNote)}{expressHint}";
            }
        }

        public string GetStatusLine() =>
            $"emotion={_data.Current} intensity={_data.Intensity:F2} expressed={GetExpressedIntensity():F2} " +
            $"connection={_data.ConnectionScore:F2} bond={Bond} stress={_data.Stress.TotalLoad():F2} " +
            $"silent_days={_data.ConsecutiveSilentDays} pad=[{CurrentPad}]";

        // ══════════════════════════════════════════════════════════════════════
        // ANALYTICS
        // ══════════════════════════════════════════════════════════════════════

        public string GetEmotionalHistory(int days = 7)
        {
            lock (_lock)
            {
                var since  = DateTime.Now.AddDays(-days);
                var recent = _data.EmotionalMemory.Where(e => e.When >= since).ToList();
                if (!recent.Any()) return "";
                var counts = recent
                    .GroupBy(e => e.State)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => $"{g.Key}({g.Count()}×)");
                return $"Емоції за {days} днів: {string.Join(", ", counts)}";
            }
        }

        public string GetMoodTrajectory()
        {
            lock (_lock)
            {
                var recent = _data.EmotionalMemory.TakeLast(5).ToList();
                if (recent.Count < 2) return "";
                var first = recent.First();
                var last  = recent.Last();

                var positiveStates = new[] { EmotionState.Warm, EmotionState.Playful, EmotionState.Tender, EmotionState.Proud, EmotionState.Curious, EmotionState.Hopeful, EmotionState.Excited };
                var negativeStates = new[] { EmotionState.Concerned, EmotionState.Melancholy, EmotionState.Distant, EmotionState.Irritated, EmotionState.Anxious };

                bool wasPositive = positiveStates.Contains(first.State);
                bool isPositive  = positiveStates.Contains(last.State);
                bool wasNegative = negativeStates.Contains(first.State);
                bool isNegative  = negativeStates.Contains(last.State);

                if (!wasPositive && isPositive) return $"Траєкторія: {first.State}→{last.State} (покращення)";
                if (wasPositive && !isPositive)  return $"Траєкторія: {first.State}→{last.State} (погіршення)";
                if (wasNegative && isNegative)   return $"Траєкторія: стабільно негативна ({last.State})";
                if (wasPositive && isPositive)   return $"Траєкторія: стабільно позитивна ({last.State})";
                return $"Поточний стан: {last.State}";
            }
        }

        public string GetEmotionalPattern()
        {
            lock (_lock)
            {
                if (_data.EmotionalMemory.Count < 10) return "";
                var byDow = _data.EmotionalMemory
                    .GroupBy(e => e.When.DayOfWeek)
                    .Where(g => g.Count() >= 3)
                    .Select(g => new
                    {
                        Day = g.Key,
                        DominantState = g.GroupBy(e => e.State).OrderByDescending(sg => sg.Count()).First(),
                        Total = g.Count()
                    })
                    .Where(x => (float)x.DominantState.Count() / x.Total > 0.6f)
                    .FirstOrDefault();

                if (byDow != null)
                {
                    var dayName = byDow.Day switch
                    {
                        DayOfWeek.Monday    => "понеділках",
                        DayOfWeek.Tuesday   => "вівторках",
                        DayOfWeek.Wednesday => "середах",
                        DayOfWeek.Thursday  => "четвергах",
                        DayOfWeek.Friday    => "п'ятницях",
                        DayOfWeek.Saturday  => "суботах",
                        DayOfWeek.Sunday    => "неділях",
                        _                   => byDow.Day.ToString()
                    };
                    return $"Помічений патерн: зазвичай {byDow.DominantState.Key} по {dayName}";
                }
                return "";
            }
        }

        /// <summary>Повна емоційна статистика для дашборду</summary>
        public EmotionStats GetStats()
        {
            lock (_lock)
            {
                var last30 = _data.EmotionalMemory
                    .Where(e => e.When >= DateTime.Now.AddDays(-30))
                    .ToList();

                return new EmotionStats
                {
                    TotalAppraisals    = _data.TotalAppraisals,
                    TotalStateChanges  = _data.TotalStateChanges,
                    PositiveExchanges  = _data.PositiveExchanges,
                    ConflictExchanges  = _data.ConflictExchanges,
                    CurrentBond        = Bond,
                    CurrentStressLoad  = _data.Stress.TotalLoad(),
                    ResilienceReserve  = _data.Stress.ResilienceReserve,
                    AttachmentTrust    = _data.Attachment.Trust,
                    AttachmentIntimacy = _data.Attachment.Intimacy,
                    TopEmotionsLast30  = last30
                        .GroupBy(e => e.State)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => (g.Key, g.Count()))
                        .ToList(),
                    AvgSalienceLast30  = last30.Any() ? last30.Average(e => e.Salience) : 0f,
                    CurrentPad         = CurrentPad,
                };
            }
        }

        public class EmotionStats
        {
            public int TotalAppraisals    { get; set; }
            public int TotalStateChanges  { get; set; }
            public int PositiveExchanges  { get; set; }
            public int ConflictExchanges  { get; set; }
            public BondLevel CurrentBond  { get; set; }
            public float CurrentStressLoad{ get; set; }
            public float ResilienceReserve{ get; set; }
            public float AttachmentTrust  { get; set; }
            public float AttachmentIntimacy { get; set; }
            public List<(EmotionState State, int Count)> TopEmotionsLast30 { get; set; } = new();
            public float AvgSalienceLast30 { get; set; }
            public PadVector CurrentPad   { get; set; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // PERSISTENCE
        // ══════════════════════════════════════════════════════════════════════

        private EmotionData Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonConvert.DeserializeObject<EmotionData>(File.ReadAllText(_path)) ?? new();
            }
            catch (Exception suppressedEx1477) { KokoSystemLog.Write("EMOTIONENGINE-CATCH", "Load failed near source line 1477: " + suppressedEx1477); }
            return new();
        }

        private void Save()
        {
            try { File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented)); }
            catch (Exception suppressedEx1484) { KokoSystemLog.Write("EMOTIONENGINE-CATCH", "Save failed near source line 1484: " + suppressedEx1484); }
        }

        // ══════════════════════════════════════════════════════════════════════
        // UTILITIES
        // ══════════════════════════════════════════════════════════════════════

        private void AddToHistory(EmotionState state, float intensity, string trigger)
        {
            _data.History.Add(new EmotionEntry { State = state, Intensity = intensity, Trigger = trigger });
            if (_data.History.Count > 150) _data.History.RemoveAt(0);
        }

        private bool CheckRefractory(EmotionState target)
        {
            if (!_data.LastStateTime.TryGetValue(target, out var lastTime)) return true;
            var profile = TemporalProfiles[target];
            return (DateTime.Now - lastTime).TotalMinutes >= profile.RefractoryMin;
        }

        private void RegisterPeaks(EmotionState state, float intensity, string tone)
        {
            if (intensity > 0.80f)
            {
                if (IsPositiveState(state))
                {
                    _data.LastPositivePeak  = DateTime.Now;
                    _data.PeakPositiveScore = intensity;
                }
                else if (IsNegativeTone(tone))
                {
                    _data.LastNegativePeak  = DateTime.Now;
                    _data.PeakNegativeScore = 1f - intensity;
                }
            }
        }

        private void UpdateConnectionLegacy(string tone)
        {
            switch (tone)
            {
                case "happy": case "excited": case "loving":
                    _data.ConnectionScore = Math.Min(1f, _data.ConnectionScore + 0.02f);
                    _data.ConsecutiveSilentDays = 0;
                    break;
                case "sad": case "anxious": case "stressed":
                    _data.ConnectionScore = Math.Min(1f, _data.ConnectionScore + 0.01f);
                    break;
                case "angry":
                    _data.ConnectionScore = Math.Max(0.1f, _data.ConnectionScore - 0.01f);
                    break;
            }
        }

        private void SyncAttachmentFromLegacyScore()
        {
            var score = _data.ConnectionScore;
            _data.Attachment.Trust      = score * 0.9f;
            _data.Attachment.Intimacy   = score * 0.7f;
            _data.Attachment.Reliability= score * 0.85f;
            _data.Attachment.Reciprocity= score * 0.8f;
            _data.Attachment.Vitality   = 0.6f;
        }

        private static bool IsPositiveState(EmotionState s) =>
            s is EmotionState.Warm or EmotionState.Playful or EmotionState.Tender
              or EmotionState.Proud or EmotionState.Excited or EmotionState.Hopeful;

        private static bool IsNegativeTone(string tone) =>
            tone is "sad" or "anxious" or "stressed" or "worried" or "hurt";

        private static float LerpF(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    }
}
