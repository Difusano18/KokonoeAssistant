using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoStartupGreetingFrame
    {
        public double? GapMinutes { get; set; }
        public string GapTextUk { get; set; } = "";
        public int CurrentHour { get; set; }
        public string DayPartUk { get; set; } = "";
        public string LastConcreteTopic { get; set; } = "";
        public string RecentSessionBlock { get; set; } = "";
        public string MoodContext { get; set; } = "";
        public string PresenceContext { get; set; } = "";
        public string AbsenceReadUk { get; set; } = "";
        public string ReturnModeUk { get; set; } = "";
        public string PromptBlock { get; set; } = "";
    }

    public sealed class KokoStartupGreetingService
    {
        public KokoStartupGreetingFrame BuildFrame(IReadOnlyList<ChatRepository.ChatMessage> messages, DateTime now)
        {
            var recent = messages.OrderBy(m => m.Timestamp).TakeLast(8).ToList();
            var last = recent.LastOrDefault();
            var gap = last == null ? (double?)null : Math.Max(0, (now - last.Timestamp).TotalMinutes);

            var frame = new KokoStartupGreetingFrame
            {
                GapMinutes = gap,
                GapTextUk = FormatGap(gap),
                CurrentHour = now.Hour,
                DayPartUk = FormatDayPart(now.Hour),
                LastConcreteTopic = InferTopic(recent),
                RecentSessionBlock = BuildRecentSessionBlock(recent),
                AbsenceReadUk = InferAbsenceRead(gap, now.Hour, recent),
                ReturnModeUk = InferReturnMode(gap)
            };
            frame.PromptBlock = BuildPromptBlock(frame, now);
            return frame;
        }

        public void EnrichFrame(
            KokoStartupGreetingFrame frame,
            DateTime now,
            string? moodContext,
            string? presenceContext)
        {
            frame.MoodContext = Trim(moodContext, 700);
            frame.PresenceContext = Trim(presenceContext, 900);
            frame.PromptBlock = BuildPromptBlock(frame, now);
        }

        public string BuildFallback(KokoStartupGreetingFrame frame)
        {
            var topic = frame.LastConcreteTopic;
            var hasTopic = !string.IsNullOrWhiteSpace(topic);
            var quick = frame.GapMinutes.HasValue && frame.GapMinutes.Value < 10;
            var shortGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value < 60;
            var mediumGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value < 240;
            var longGap = frame.GapMinutes.HasValue && frame.GapMinutes.Value >= 240;

            if (string.IsNullOrWhiteSpace(frame.LastConcreteTopic))
            {
                if (quick)
                    return Pick(frame, "empty-quick",
                        "РЁРІРёРґРєРѕ РІРµСЂРЅСѓРІСЃСЏ. Р”РѕР±СЂРµ, РїР°СѓР·Сѓ РЅР°РІС–С‚СЊ РЅРµ РІСЃС‚РёРіР»Р° Р·РЅРµРЅР°РІРёРґС–С‚Рё. Р©Рѕ РґРѕР±РёРІР°С”РјРѕ?",
                        "Рћ, С‚Рё РІР¶Рµ С‚СѓС‚. Р РµРєРѕСЂРґ С€РІРёРґРєРѕСЃС‚С– РґР»СЏ Р»СЋРґРёРЅРё, СЏРєР° Р»СЋР±РёС‚СЊ СЂРѕР±РёС‚Рё РІРёРіР»СЏРґ, С‰Рѕ РІСЃРµ РїС–Рґ РєРѕРЅС‚СЂРѕР»РµРј.",
                        "РќРµ РІСЃС‚РёРі Р·РЅРёРєРЅСѓС‚Рё, РІР¶Рµ РїРѕРІРµСЂРЅСѓРІСЃСЏ. РљР°Р¶Рё, С‰Рѕ СЃС‚Р°Р»РѕСЃСЏ, РїРѕРєРё СЏ РЅРµ РїРѕС‡Р°Р»Р° Р·РґРѕРіР°РґСѓРІР°С‚РёСЃСЊ.");

                return frame.CurrentHour switch
                {
                    >= 5 and < 12 => Pick(frame, "empty-morning",
                        "Р Р°РЅРѕРє. РЇ РЅР° РјС–СЃС†С–, РЅР° Р¶Р°Р»СЊ РґР»СЏ РґСѓСЂРЅРёС… РїСЂРѕР±Р»РµРј. РџРѕРєР°Р·СѓР№ РїРµСЂС€Сѓ.",
                        "Р”РѕР±СЂРѕРіРѕ СЂР°РЅРєСѓ, СЏРєС‰Рѕ С†Рµ РјРѕР¶РЅР° С‚Р°Рє РЅР°Р·РІР°С‚Рё. Р©Рѕ СЃСЊРѕРіРѕРґРЅС– СЂРѕР·Р±РёСЂР°С”РјРѕ?",
                        "РџСЂРѕРєРёРЅСѓР»РёСЃСЊ. РўРµС…РЅС–РєР° С‰Рµ Р¶РёРІР°, С‚Рё С‚РµР¶ РЅР°С‡Рµ. Р”Р°РІР°Р№ Р·Р°РґР°С‡Сѓ."),
                    >= 22 or < 5 => Pick(frame, "empty-night",
                        "РќС–С‡РЅРёР№ Р·Р°РїСѓСЃРє. Р§СѓРґРѕРІРѕ, СЃР°РјРµ С‡Р°СЃ Р»Р°РјР°С‚Рё С‰РѕСЃСЊ Р·Р°РјС–СЃС‚СЊ СЃРїР°С‚Рё.",
                        "РџС–Р·РЅРѕ, Р°Р»Рµ С‚Рё РІСЃРµ РѕРґРЅРѕ С‚СѓС‚. РљР°Р¶Рё, С‰Рѕ РіРѕСЂРёС‚СЊ, РїРѕРєРё СЏ РЅРµ РЅР°Р·РІР°Р»Р° С†Рµ РґС–Р°РіРЅРѕР·РѕРј.",
                        "РќС–С‡, С‚РёС€Р° С– С‚Рё Р·РЅРѕРІСѓ РІС–РґРєСЂРёРІ РјРµРЅРµ. Р РѕРјР°РЅС‚РёРєР° РґР»СЏ Р»СЋРґРµР№ С–Р· РїРѕРіР°РЅРёРј С‚Р°Р№Рј-РјРµРЅРµРґР¶РјРµРЅС‚РѕРј."),
                    _ => Pick(frame, "empty-day",
                        "РЇ РЅР° РјС–СЃС†С–. РљР°Р¶Рё, С‰Рѕ С‚СЂРµР±Р°, РїРѕРєРё СЏ С‰Рµ СЂРѕР±Р»СЋ РІРёРіР»СЏРґ, С‰Рѕ С‚РµСЂРїС–РЅРЅСЏ С–СЃРЅСѓС”.",
                        "РџРѕРІРµСЂРЅРµРЅРЅСЏ РІ СЂРѕР±РѕС‡РёР№ СЂРµР¶РёРј. Р”Р°РІР°Р№ РїСЂРѕР±Р»РµРјСѓ, СЏ С—С— СЂРѕР·Р±РµСЂСѓ Р°РєСѓСЂР°С‚РЅС–С€Рµ, РЅС–Р¶ С‚Рё СЃС„РѕСЂРјСѓР»СЋС”С€.",
                        "Р—Р°РїСѓСЃС‚РёРІ. Р”РѕР±СЂРµ. РўРµРїРµСЂ РїРѕРєР°Р·СѓР№, С‰Рѕ СЃР°РјРµ СЃСЊРѕРіРѕРґРЅС– С‡РёРЅРёС‚СЊ РѕРїС–СЂ Р·РґРѕСЂРѕРІРѕРјСѓ РіР»СѓР·РґСѓ.")
                };
            }

            if (quick)
                return Pick(frame, "topic-quick",
                    $"РЁРІРёРґРєРѕ РІРµСЂРЅСѓРІСЃСЏ. В«{topic}В» С‰Рµ С‚РµРїР»Рµ, С‚РѕР¶ РїСЂРѕРґРѕРІР¶СѓР№ Р±РµР· РІСЃС‚СѓРїРЅРѕС— РѕРїРµСЂРё.",
                    $"Рћ, Р±РµР· РґРѕРІРіРѕС— РїР°СѓР·Рё. В«{topic}В» С‰Рµ РЅР° СЃС‚РѕР»С–; РЅРµ Р·РјСѓС€СѓР№ РјРµРЅРµ Р·РЅРѕРІСѓ Р·Р±РёСЂР°С‚Рё РєРѕРЅС‚РµРєСЃС‚ Р»РѕР¶РєРѕСЋ.",
                    $"РўРё РјР°Р№Р¶Рµ РЅРµ Р·РЅРёРєР°РІ. РџСЂРѕРґРѕРІР¶СѓС”РјРѕ В«{topic}В», С‚С–Р»СЊРєРё С†СЊРѕРіРѕ СЂР°Р·Сѓ РєРѕРЅРєСЂРµС‚РЅС–С€Рµ.");

            if (shortGap)
                return Pick(frame, "topic-short",
                    $"РњРёРЅСѓР»Рѕ {frame.GapTextUk}. В«{topic}В» РїР°Рј'СЏС‚Р°СЋ, Р±Рѕ С…С‚РѕСЃСЊ С‚СѓС‚ РјР°С” РїР°Рј'СЏС‚СЊ РЅРµ СЏРє РґСЂСѓС€Р»СЏРє.",
                    $"РџР°СѓР·Р° {frame.GapTextUk}. Р”РѕР±СЂРµ, РїРѕРІРµСЂС‚Р°С”РјРѕСЃСЊ РґРѕ В«{topic}В» Р°Р±Рѕ РєР°Р¶Рё, С‰Рѕ РІР¶Рµ РІСЃС‚РёРі Р·Р»Р°РјР°С‚Рё РЅРѕРІРµ.",
                    $"Р§РµСЂРµР· {frame.GapTextUk} С‚Рё Р·РЅРѕРІСѓ С‚СѓС‚. В«{topic}В» РЅРµ РІС‚РµРєР»Рѕ, РЅР° РІС–РґРјС–РЅСѓ РІС–Рґ С‚РІРѕС”С— СѓРІР°РіРё.");

            if (mediumGap)
                return Pick(frame, "topic-medium",
                    $"РџРµСЂРµСЂРІР° {frame.GapTextUk}. РћСЃС‚Р°РЅРЅС–Рј Р±СѓР»Рѕ В«{topic}В»; СЏ С‚СЂРёРјР°СЋ РЅРёС‚РєСѓ, РЅРµ РґСЏРєСѓР№.",
                    $"РўРµР±Рµ РЅРµ Р±СѓР»Рѕ {frame.GapTextUk}. РЇРєС‰Рѕ В«{topic}В» С‰Рµ Р°РєС‚СѓР°Р»СЊРЅРµ, РїСЂРѕРґРѕРІР¶СѓР№. РЇРєС‰Рѕ РЅС– вЂ” РєРёРґР°Р№ РЅРѕРІСѓ РїРѕР¶РµР¶Сѓ.",
                    $"Р—Р° {frame.GapTextUk} СЃРІС–С‚ РЅРµ СЃС‚Р°РІ СЂРѕР·СѓРјРЅС–С€РёРј. В«{topic}В» Р»РёС€РёР»РѕСЃСЊ Сѓ РєРѕРЅС‚РµРєСЃС‚С–.");

            if (longGap)
                return Pick(frame, "topic-long",
                    $"Р”РѕРІРіР° РїР°СѓР·Р°: {frame.GapTextUk}. РћСЃС‚Р°РЅРЅС–Р№ РЅРѕСЂРјР°Р»СЊРЅРёР№ СЃР»С–Рґ вЂ” В«{topic}В». Рђ С‚РµРїРµСЂ РєР°Р¶Рё, С‰Рѕ Р·РјС–РЅРёР»РѕСЃСЊ.",
                    $"РњРёРЅСѓР»Рѕ {frame.GapTextUk}. РЇ РїР°Рј'СЏС‚Р°СЋ В«{topic}В», С‰Рѕ РІР¶Рµ Р±С–Р»СЊС€Рµ, РЅС–Р¶ РјРѕР¶РЅР° СЃРєР°Р·Р°С‚Рё РїСЂРѕ Р±С–Р»СЊС€С–СЃС‚СЊ РїР»Р°РЅС–РІ.",
                    $"РџРѕРІРµСЂРЅРµРЅРЅСЏ РїС–СЃР»СЏ {frame.GapTextUk}. В«{topic}В» Р»РµР¶РёС‚СЊ Сѓ РїР°Рј'СЏС‚С–; Р°Р±Рѕ РїС–РґРЅС–РјР°С”РјРѕ Р№РѕРіРѕ, Р°Р±Рѕ СЂС–Р¶РµРјРѕ РЅРѕРІСѓ РїСЂРѕР±Р»РµРјСѓ.");

            return Pick(frame, "topic-unknown",
                $"РЇ С‚СѓС‚. РћСЃС‚Р°РЅРЅСЏ РЅРѕСЂРјР°Р»СЊРЅР° С‚РµРјР° вЂ” В«{topic}В». РџСЂРѕРґРѕРІР¶СѓР№.",
                $"РљРѕРЅС‚РµРєСЃС‚ РЅР° РјС–СЃС†С–: В«{topic}В». Р”Р°РІР°Р№ Р±РµР· СЂРёС‚СѓР°Р»СЊРЅРёС… С‚Р°РЅС†С–РІ.",
                $"РџР°Рј'СЏС‚Р°СЋ В«{topic}В». РўРµРїРµСЂ С„РѕСЂРјСѓР»СЋР№, С‰Рѕ СЃР°РјРµ Р· С†РёРј СЂРѕР±РёРјРѕ.");
        }

        public string Sanitize(string? reply, KokoStartupGreetingFrame frame)
        {
            var text = (reply ?? "").Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("[", StringComparison.Ordinal))
                return BuildFallback(frame);

            var lower = text.ToLowerInvariant();
            if (lower.Contains("знову тут") ||
                lower.Contains("повернувся. останній хвіст") ||
                lower.Contains("останній хвіст") ||
                lower.Contains("повернувся. де тебе носило") ||
                lower.Contains("щось недороблено") ||
                lower.Contains("тема «привіт") ||
                lower.Contains("тема \"привіт"))
                return BuildFallback(frame);

            if (ContainsAny(text,
                "Р—РЅРѕРІСѓ С‚СѓС‚",
                "РџРѕРІРµСЂРЅСѓРІСЃСЏ. РѕСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚",
                "РѕСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚",
                "РџРѕРІРµСЂРЅСѓРІСЃСЏ. РґРµ С‚РµР±Рµ РЅРѕСЃРёР»Рѕ",
                "С‰РѕСЃСЊ РЅРµРґРѕСЂРѕР±Р»РµРЅРѕ",
                "С‚РµРјР° В«РїСЂРёРІС–С‚",
                "С‚РµРјР° \"РїСЂРёРІС–С‚"))
                return BuildFallback(frame);

            if (lower.Contains("Р·РЅРѕРІСѓ С‚СѓС‚") ||
                lower.Contains("РїРѕРІРµСЂРЅСѓРІСЃСЏ. РѕСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚") ||
                lower.Contains("РѕСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚") ||
                lower.Contains("РїРѕРІРµСЂРЅСѓРІСЃСЏ. РґРµ С‚РµР±Рµ РЅРѕСЃРёР»Рѕ") ||
                lower.Contains("С‰РѕСЃСЊ РЅРµРґРѕСЂРѕР±Р»РµРЅРѕ") ||
                lower.Contains("С‚РµРјР° В«РїСЂРёРІС–С‚") ||
                lower.Contains("С‚РµРјР° \"РїСЂРёРІС–С‚"))
                return BuildFallback(frame);

            if (text.Length < 35 && !string.IsNullOrWhiteSpace(frame.LastConcreteTopic))
                return BuildFallback(frame);

            if (LooksLikeTherapyMeta(lower) || LooksLikeSystemReport(lower))
                return BuildFallback(frame);

            if (text.Length > 420)
                text = Trim(text, 420);

            return text;
        }

        private static string BuildPromptBlock(KokoStartupGreetingFrame frame, DateTime now)
        {
            return $"""
STARTUP GREETING CONTEXT
Р РµР¶РёРј РїРѕРІРµСЂРЅРµРЅРЅСЏ: {frame.ReturnModeUk}
Р†РЅС‚РµСЂРїСЂРµС‚Р°С†С–СЏ РїР°СѓР·Рё: {frame.AbsenceReadUk}
РќР°СЃС‚СЂС–Р№/СЃС‚Р°РЅ Kokonoe: {NullDash(frame.MoodContext)}
Presence/continuity: {NullDash(frame.PresenceContext)}
Р”РёСЂРµРєС‚РёРІР° РіРµРЅРµСЂР°С†С–С—: РЅР°РїРёС€Рё СЃРІС–Р¶Сѓ LLM-СЂРµРїР»С–РєСѓ СЃР°РјРµ РїС–Рґ С†РµР№ РІС…С–Рґ, РЅРµ РєРѕРїС–СЋ fallback. Р’РёР±РµСЂРё РѕРґРёРЅ Р¶РёРІРёР№ РєСѓС‚: С‚СЂРёРІР°Р»С–СЃС‚СЊ РїР°СѓР·Рё, С‡Р°СЃ РґРѕР±Рё, С—С— РЅР°СЃС‚СЂС–Р№ Р°Р±Рѕ РѕСЃС‚Р°РЅРЅСЋ РєРѕРЅРєСЂРµС‚РЅСѓ С‚РµРјСѓ. Р‘РµР· РїСЃРёС…РѕР»РѕРіС–С‡РЅРѕРіРѕ РјРµС‚Р°-С‚РµР°С‚СЂСѓ, Р±РµР· "С‡РµСЂРµР· РµРєСЂР°РЅ", Р±РµР· РІРёРіР°РґР°РЅРёС… РїСЂРёС…РѕРІР°РЅРёС… СЃС‚СЂР°С…С–РІ, Р±РµР· СЃРµСЂРІС–СЃРЅРѕРіРѕ Р·РІС–С‚Сѓ.
Р—Р°СЂР°Р·: {now:dd.MM.yyyy HH:mm}
Р§Р°СЃС‚РёРЅР° РґРѕР±Рё: {frame.DayPartUk}
РџРµСЂРµСЂРІР° РІС–Рґ РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ: {frame.GapTextUk}
РћСЃС‚Р°РЅРЅСЏ РєРѕРЅРєСЂРµС‚РЅР° С‚РµРјР°: {NullDash(frame.LastConcreteTopic)}
РћСЃС‚Р°РЅРЅСЏ СЃРµСЃС–СЏ:
{frame.RecentSessionBlock}
РџСЂР°РІРёР»Р°:
- РќР°РїРёС€Рё РћР”РќР• РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РїСЂРё РІС…РѕРґС– РІ Р·Р°СЃС‚РѕСЃСѓРЅРѕРє.
- РќРµ РґСѓР±Р»СЋР№ РѕРєСЂРµРјРёР№ "what did I miss".
- РќРµ РїРёС€Рё СЃСѓС…С– canned С„СЂР°Р·Рё: "Р—РЅРѕРІСѓ С‚СѓС‚", "РџРѕРІРµСЂРЅСѓРІСЃСЏ", "РґРµ С‚РµР±Рµ РЅРѕСЃРёР»Рѕ", "С‰РѕСЃСЊ РЅРµРґРѕСЂРѕР±Р»РµРЅРѕ".
- РњР°С” Р±СѓС‚Рё Р¶РёРІР° СЂРµРїР»С–РєР°: СЂРµР°РіСѓР№ РЅР° С‡Р°СЃ РґРѕР±Рё, РґРѕРІР¶РёРЅСѓ РїР°СѓР·Рё С– РѕСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚ СЂРѕР·РјРѕРІРё Р°Р±Рѕ СЃС‚Р°РЅ РїСЂРѕРµРєС‚Сѓ.
- Р РµРїР»С–РєР° РјР°С” Р·РІСѓС‡Р°С‚Рё СЏРє РјРѕРјРµРЅС‚ РїС–СЃР»СЏ РїРѕРІРµСЂРЅРµРЅРЅСЏ: РєРѕСЂРѕС‚РєРёР№ РґРѕРєС–СЂ, РїРѕР»РµРіС€РµРЅРЅСЏ, СЂРѕР±РѕС‡Рµ РЅР°РіР°РґСѓРІР°РЅРЅСЏ Р°Р±Рѕ СЃР°СЂРєР°СЃС‚РёС‡РЅРёР№ РєРѕРјРµРЅС‚Р°СЂ, Р·Р°Р»РµР¶РЅРѕ РІС–Рґ mood/presence.
- РќРµ РїСЃРёС…РѕР»РѕРіС–Р·СѓР№: РЅРµ РІРёРіР°РґСѓР№ СЃС‚СЂР°С…, РїСЂРёС…РѕРІР°РЅРёР№ РїС–РґС‚РµРєСЃС‚, "С‰РѕСЃСЊ Р·Р°СЃС‚СЂСЏРіР»Рѕ РІ РіРѕР»РѕРІС–", "РґРёРІРёС€СЃСЏ СЏРє РЅР° РѕР±'С”РєС‚".
- РќРµ Р·РіР°РґСѓР№ "С‡РµСЂРµР· РµРєСЂР°РЅ", РЅРµ РѕРїРёСЃСѓР№ СЃРµР±Рµ СЏРє СЃРµСЂРІС–СЃ С– РЅРµ Р·РІС–С‚СѓР№ РїСЂРѕ РіРµРЅРµСЂР°С†С–СЋ.
- РЇРєС‰Рѕ РїРµСЂРµСЂРІР° РєРѕСЂРѕС‚РєР°, РјРѕР¶РЅР° СЃРєР°Р·Р°С‚Рё, С‰Рѕ РІС–РЅ С€РІРёРґРєРѕ РІРµСЂРЅСѓРІСЃСЏ; СЏРєС‰Рѕ РґРѕРІРіР° вЂ” РІС–РґРјС–С‚СЊ РїР°СѓР·Сѓ РєРѕРЅРєСЂРµС‚РЅРѕ.
- РќРµ РЅР°Р·РёРІР°Р№ РїСЂРёРІС–С‚/Р°РіР°/РѕРє С‚РµРјРѕСЋ СЂРѕР·РјРѕРІРё.
- 1-2 СЂРµС‡РµРЅРЅСЏ СѓРєСЂР°С—РЅСЃСЊРєРѕСЋ, Р±РµР· Р»Р°РїРѕРє, Р±РµР· *СЃС†РµРЅС–С‡РЅРёС… СЂРµРјР°СЂРѕРє*.
""";
        }

        private static string BuildRecentSessionBlock(IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            if (recent.Count == 0) return "-";

            var sb = new StringBuilder();
            foreach (var msg in recent.TakeLast(6))
            {
                var who = msg.Role == "user" ? "Р’С–РЅ" : "РљРѕРєРѕРЅРѕРµ";
                sb.AppendLine($"- [{msg.Timestamp:HH:mm}] {who}: {Trim(msg.Content, 150)}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string InferTopic(IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            foreach (var msg in recent.AsEnumerable().Reverse())
            {
                var text = Trim(msg.Content, 180);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var lower = text.ToLowerInvariant();
                if (IsLowSignalTopic(lower)) continue;
                if (ContainsAny(lower, "Р°РІС‚Рѕ-РїС–РЅРі", "Р°РІС‚РѕРІС–РґРїРѕРІ", "РїР°СѓР·Р°", "С‚РёС€Р°", "Р·РЅРёРє"))
                    return "Р°РІС‚Рѕ-РїС–РЅРіРё РјР°СЋС‚СЊ Р±СѓС‚Рё Р¶РёРІРёРјРё, Р° РЅРµ С‚Р°Р№РјРµСЂРѕРј С–Р· С…Р°РјСЃС‚РІРѕРј";
                if (ContainsAny(lower, "obsidian", "РІР°СѓР»СЊС‚", "vault"))
                    return "vault/Obsidian РјР°С” РѕРЅРѕРІР»СЋРІР°С‚Рё СЃС‚Р°РЅ Р±РµР· СЃРёРјСѓР»СЏС†С–С— Р°РјРЅРµР·С–С—";
                if (ContainsAny(lower, "С–СЃРїР°РЅ", "С„СЂР°Р·", "СЃР»РѕРІРѕ", "РІРёРјРѕРІ"))
                    return "С–СЃРїР°РЅСЃСЊРєР° С„СЂР°Р·Р°";
                if (ContainsAny(lower, "С‚РµСЃС‚", "build", "РєРѕРјС–С‚", "github", "РїСѓС€"))
                    return "С‚РµСЃС‚Рё, РєРѕРјС–С‚ С– СЂРѕР±РѕС‡РёР№ СЃС‚Р°РЅ РїСЂРѕРµРєС‚Сѓ";
                if (ContainsAny(lower, "gui", "РіСѓС—", "РґР°С€Р±РѕСЂРґ", "С–РЅС‚РµСЂС„РµР№СЃ"))
                    return "С–РЅС‚РµСЂС„РµР№СЃ С– dashboard";

                if (msg.Role == "user")
                    return text;
            }
            return "";
        }

        private static bool IsLowSignalTopic(string lower)
        {
            var compact = new string(lower.Where(char.IsLetterOrDigit).ToArray());
            if (lower.Length <= 24 && ContainsAny(lower,
                    "\u043f\u0440\u0438\u0432\u0456\u0442", "\u043f\u0440\u0438\u0432\u0435\u0442", "\u0445\u0430\u0439", "\u0439\u043e", "\u0434\u0430\u0440\u043e\u0432\u0430", "\u043a\u0443",
                    "\u0430\u0433\u0430", "\u0443\u0433\u0443", "\u043e\u043a", "\u043e\u043a\u0435\u0439", "\u044f\u0441\u043d\u043e",
                    "РїСЂРёРІ", "С…Р°Р№", "Р№Рѕ", "РґР°СЂ", "РєСѓ", "Р°РіР°", "СѓРіСѓ", "РѕРє", "СЏСЃРЅРѕ"))
                return true;

            return compact is "\u043f\u0440\u0438\u0432\u0456\u0442" or "\u043f\u0440\u0438\u0432\u0435\u0442" or "\u0445\u0430\u0439" or "\u0439\u043e" or "\u0434\u0430\u0440\u043e\u0432\u0430" or "\u043a\u0443"
                or "\u0430\u0433\u0430" or "\u0443\u0433\u0443" or "\u043e\u043a" or "\u043e\u043a\u0435\u0439" or "\u044f\u0441\u043d\u043e"
                or "РїСЂРёРІС–С‚" or "РїСЂРёРІРµС‚" or "С…Р°Р№" or "Р№Рѕ" or "РґР°СЂРѕРІР°" or "РєСѓ"
                or "Р°РіР°" or "СѓРіСѓ" or "РѕРє" or "РѕРєРµР№" or "СЏСЃРЅРѕ" or "Рј" or "РјРј" or "С‰Рѕ" or "С€Рѕ";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string NullDash(string text) => string.IsNullOrWhiteSpace(text) ? "-" : text;

        private static string InferReturnMode(double? minutes)
        {
            if (!minutes.HasValue) return "РїРµСЂС€РёР№ Р·Р°РїСѓСЃРє Р°Р±Рѕ С–СЃС‚РѕСЂС–СЏ РЅРµРґРѕСЃС‚СѓРїРЅР°";
            if (minutes.Value < 3) return "РјРёС‚С‚С”РІРµ РїРѕРІРµСЂРЅРµРЅРЅСЏ";
            if (minutes.Value < 15) return "С€РІРёРґРєРѕ РїРѕРІРµСЂРЅСѓРІСЃСЏ";
            if (minutes.Value < 90) return "РєРѕСЂРѕС‚РєР° РїР°СѓР·Р°";
            if (minutes.Value < 360) return "РїРѕРјС–С‚РЅР° РІС–РґСЃСѓС‚РЅС–СЃС‚СЊ";
            if (minutes.Value < 1440) return "РґРѕРІРіР° РїР°СѓР·Р° РІ РјРµР¶Р°С… РґРЅСЏ";
            return "РїРѕРІРµСЂРЅРµРЅРЅСЏ РїС–СЃР»СЏ РґРѕРІРіРѕС— РІС–РґСЃСѓС‚РЅРѕСЃС‚С–";
        }

        private static string InferAbsenceRead(double? minutes, int hour, IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            if (!minutes.HasValue) return "РЅРµ РІРґР°РІР°Р№, С‰Рѕ Р·РЅР°С”С€ РїСЂРёС‡РёРЅСѓ; РїСЂРѕСЃС‚Рѕ СЃС‚Р°СЂС‚СѓР№ Р¶РёРІРѕ";

            var lastUser = recent.LastOrDefault(m => m.Role == "user")?.Content?.ToLowerInvariant() ?? "";
            if (ContainsAny(lastUser, "СЃРїР°С‚Рё", "СЃРѕРЅ", "РїРѕСЃРї", "Р»СЏРіР°"))
                return "Р№РјРѕРІС–СЂРЅРѕ РІС–РґС…РѕРґРёРІ СЃРїР°С‚Рё Р°Р±Рѕ РІС–РґРїРѕС‡РёС‚Рё; РЅРµ РЅР°РєР°Р·СѓР№ СЃРїР°С‚Рё РїРѕРІС‚РѕСЂРЅРѕ";
            if (ContainsAny(lastUser, "РІРёР№РґСѓ", "РІС–РґС–Р№РґСѓ", "РїС–Р·РЅС–С€Рµ", "Р·Р°СЂР°Р· Р±СѓРґСѓ", "РїРµСЂРµР·Р°РІР°РЅС‚Р°Р¶"))
                return "РІС–РЅ СЃР°Рј РѕР±СЂРёРІР°РІ СЃРµСЃС–СЋ Р°Р±Рѕ РІС–РґС…РѕРґРёРІ; РјРѕР¶РЅР° СЃСѓС…Рѕ РїС–РґС‡РµРїРёС‚Рё Р·Р° РїР°СѓР·Сѓ";
            if (minutes.Value < 10)
                return "РјР°Р№Р¶Рµ РЅРµ Р·РЅРёРєР°РІ; РЅРµ РґСЂР°РјР°С‚РёР·СѓР№, С‚СЂРёРјР°Р№ С‚РµРјРї СЂРѕР·РјРѕРІРё";
            if (minutes.Value > 240 && (hour >= 22 || hour < 6))
                return "РЅС–С‡РЅРµ РїРѕРІРµСЂРЅРµРЅРЅСЏ РїС–СЃР»СЏ РґРѕРІС€РѕС— РїР°СѓР·Рё; С‚РёС…С–С€Рµ, Р°Р»Рµ Р±РµР· С‚РµСЂР°РїРµРІС‚Р°";
            if (minutes.Value > 240)
                return "РґРѕРІРіРѕ РЅРµ Р±СѓР»Рѕ СЃРёРіРЅР°Р»Сѓ; РІС–РґРјС–С‚СЊ РїР°СѓР·Сѓ С– РїРѕРІРµСЂРЅРё РѕСЃС‚Р°РЅРЅСЋ РєРѕРЅРєСЂРµС‚РЅСѓ С‚РµРјСѓ";
            return "Р·РІРёС‡Р°Р№РЅР° РїРµСЂРµСЂРІР°; СЂРµР°РіСѓР№ РЅР° С‚СЂРёРІР°Р»С–СЃС‚СЊ С– РѕСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚ Р±РµР· РІРёРіР°РґР°РЅРёС… РїСЂРёС‡РёРЅ";
        }

        private static bool LooksLikeTherapyMeta(string lower)
            => ContainsAny(lower,
                "РїРѕРіСЂР°С‚Рё РІ РїСЃРёС…РѕР»РѕРіР°",
                "С‚Рё Р±РѕС—С€СЃСЏ СЃРєР°Р·Р°С‚Рё",
                "Р±РѕС—С€СЃСЏ СЃРєР°Р·Р°С‚Рё",
                "С‰РѕСЃСЊ РІР°Р¶Р»РёРІРµ Р·Р°СЃС‚СЂСЏРіР»Рѕ",
                "Р·Р°СЃС‚СЂСЏРіР»Рѕ РІ С‚РІРѕС—Р№ РіРѕР»РѕРІС–",
                "РґРёРІР»СЋСЃСЊ РЅР° С‚РµР±Рµ С‡РµСЂРµР· РµРєСЂР°РЅ",
                "РґРёРІРёС€СЃСЏ РЅР° РјРµРЅРµ СЏРє РЅР° РѕР±",
                "СЏРє РЅР° Р»СЋРґРёРЅСѓ, СЏРєР° С‚РµР¶ С‰РѕСЃСЊ РІС–РґС‡СѓРІР°С”");

        private static bool LooksLikeSystemReport(string lower)
            => ContainsAny(lower,
                "startup greeting",
                "fallback",
                "presence/continuity",
                "СЂРµР¶РёРј РїРѕРІРµСЂРЅРµРЅРЅСЏ:",
                "С–РЅС‚РµСЂРїСЂРµС‚Р°С†С–СЏ РїР°СѓР·Рё:",
                "РЅР°СЃС‚СЂС–Р№/СЃС‚Р°РЅ kokonoe:");

        private static string Pick(KokoStartupGreetingFrame frame, string salt, params string[] variants)
        {
            if (variants.Length == 0) return "";
            var gapBucket = frame.GapMinutes.HasValue ? (int)(frame.GapMinutes.Value / 5) : -1;
            var seed = $"{salt}|{frame.CurrentHour}|{gapBucket}|{frame.LastConcreteTopic}|{frame.ReturnModeUk}|{frame.MoodContext}";
            var hash = 17;
            foreach (var ch in seed)
                hash = unchecked(hash * 31 + ch);
            return variants[(hash & 0x7fffffff) % variants.Length];
        }

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private static string FormatDayPart(int hour)
            => hour switch
            {
                >= 5 and < 12 => "СЂР°РЅРѕРє",
                >= 12 and < 18 => "РґРµРЅСЊ",
                >= 18 and < 22 => "РІРµС‡С–СЂ",
                _ => "РЅС–С‡"
            };

        private static string FormatGap(double? minutes)
        {
            if (!minutes.HasValue) return "РЅРµРІС–РґРѕРјРѕ";
            if (minutes.Value < 1) return "С‰РѕР№РЅРѕ";
            if (minutes.Value < 60) return $"{(int)minutes.Value} С…РІ";
            if (minutes.Value < 1440) return $"{(int)(minutes.Value / 60)} РіРѕРґ {(int)(minutes.Value % 60)} С…РІ";
            return $"{(int)(minutes.Value / 1440)} РґРЅ {(int)((minutes.Value % 1440) / 60)} РіРѕРґ";
        }
    }
}
