using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════════════
    // KOKO EMBEDDING SERVICE
    //
    // Семантичні embeddings через локальний Ollama (той самий що вже запущений
    // для LLM). Викликає /api/embeddings з моделлю nomic-embed-text або
    // будь-якою embed-моделлю що є в Ollama.
    //
    // Надає:
    //  • GetEmbeddingAsync(text) → float[] вектор (384 або 768 dims)
    //  • CosineSimilarity(a, b) → 0..1
    //  • SemanticSearch(query, candidates) → ранжовані результати
    //
    // Якщо Ollama недоступний — graceful degradation до keyword jaccard.
    // ══════════════════════════════════════════════════════════════════════════

    public class KokoEmbeddingService
    {
        private readonly string    _ollamaUrl;
        private readonly string    _model;
        private readonly HttpClient _http;
        private bool               _available = true;

        // Simple LRU cache для часто вживаних рядків
        private readonly Dictionary<string, (float[] vec, DateTime at)> _cache = new();
        private const int CacheMaxSize = 200;

        public bool IsAvailable => _available;

        public KokoEmbeddingService(string ollamaBaseUrl = "http://localhost:11434",
            string embeddingModel = "nomic-embed-text")
        {
            _ollamaUrl = ollamaBaseUrl.TrimEnd('/');
            _model     = embeddingModel;
            _http      = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        // ══════════════════════════════════════════════════════════════════
        // EMBEDDING
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Отримати embedding вектор для тексту. Повертає null при недоступності.</summary>
        public async Task<float[]?> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var key = text.Length > 200 ? text[..200] : text;
            if (_cache.TryGetValue(key, out var cached) &&
                (DateTime.Now - cached.at).TotalMinutes < 60)
                return cached.vec;

            if (!_available) return null;

            try
            {
                var payload = JsonConvert.SerializeObject(new { model = _model, prompt = text });
                var response = await _http.PostAsync(
                    $"{_ollamaUrl}/api/embeddings",
                    new StringContent(payload, Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    Log($"HTTP {response.StatusCode} від Ollama embeddings");
                    _available = false;
                    return null;
                }

                var json  = await response.Content.ReadAsStringAsync();
                var obj   = JsonConvert.DeserializeAnonymousType(json,
                    new { embedding = new float[0] });
                var vec   = obj?.embedding;
                if (vec == null || vec.Length == 0) return null;

                // Cache
                if (_cache.Count >= CacheMaxSize)
                {
                    var oldest = _cache.OrderBy(kv => kv.Value.at).First().Key;
                    _cache.Remove(oldest);
                }
                _cache[key] = (vec, DateTime.Now);
                return vec;
            }
            catch (Exception ex)
            {
                Log($"Embedding error: {ex.Message} — degrading to keyword search");
                _available = false;
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // SIMILARITY
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Cosine similarity між двома векторами. 0..1 (1 = ідентичні).</summary>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0) return 0f;
            float dot  = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot   += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            float denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
            return denom < 1e-8f ? 0f : dot / denom;
        }

        // ══════════════════════════════════════════════════════════════════
        // SEMANTIC SEARCH
        // ══════════════════════════════════════════════════════════════════

        public class SemanticMatch
        {
            public string Content   { get; set; } = "";
            public string Id        { get; set; } = "";
            public float  Score     { get; set; } = 0f;  // 0..1 cosine similarity
        }

        /// <summary>
        /// Семантичний пошук по списку документів.
        /// Якщо embeddings недоступні — fallback до Jaccard keyword similarity.
        /// </summary>
        public async Task<List<SemanticMatch>> SemanticSearchAsync(
            string query, IEnumerable<(string id, string text)> candidates, int topN = 5)
        {
            var queryVec = await GetEmbeddingAsync(query);

            if (queryVec != null)
                return await VectorSearchAsync(queryVec, candidates, topN);
            else
                return KeywordFallbackSearch(query, candidates, topN);
        }

        private async Task<List<SemanticMatch>> VectorSearchAsync(
            float[] queryVec, IEnumerable<(string id, string text)> candidates, int topN)
        {
            var results = new List<SemanticMatch>();
            foreach (var (id, text) in candidates)
            {
                var vec = await GetEmbeddingAsync(text);
                if (vec == null) continue;
                results.Add(new SemanticMatch
                {
                    Id      = id,
                    Content = text,
                    Score   = CosineSimilarity(queryVec, vec),
                });
            }
            return results.OrderByDescending(r => r.Score).Take(topN).ToList();
        }

        private static List<SemanticMatch> KeywordFallbackSearch(
            string query, IEnumerable<(string id, string text)> candidates, int topN)
        {
            var queryTokens = Tokenize(query);
            var results     = new List<SemanticMatch>();
            foreach (var (id, text) in candidates)
            {
                var docTokens = Tokenize(text);
                float score   = JaccardSimilarity(queryTokens, docTokens);
                results.Add(new SemanticMatch { Id = id, Content = text, Score = score });
            }
            return results.OrderByDescending(r => r.Score).Take(topN).ToList();
        }

        // ══════════════════════════════════════════════════════════════════
        // BATCH EMBEDDING (для попереднього розрахунку)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Генерує embeddings для списку текстів (ліміт rate-limiting).</summary>
        public async Task<Dictionary<string, float[]>> BatchEmbedAsync(
            IEnumerable<(string id, string text)> items, int delayMsPerItem = 50)
        {
            var result = new Dictionary<string, float[]>();
            foreach (var (id, text) in items)
            {
                var vec = await GetEmbeddingAsync(text);
                if (vec != null) result[id] = vec;
                if (delayMsPerItem > 0)
                    await Task.Delay(delayMsPerItem);
            }
            return result;
        }

        /// <summary>Знайти найближчий вектор у заздалегідь розрахованому індексі.</summary>
        public static List<SemanticMatch> SearchIndex(
            float[] queryVec,
            IEnumerable<(string id, string text, float[] vec)> index,
            int topN = 5,
            float minScore = 0.5f)
        {
            var results = new List<SemanticMatch>();
            foreach (var (id, text, vec) in index)
            {
                float score = CosineSimilarity(queryVec, vec);
                if (score >= minScore)
                    results.Add(new SemanticMatch { Id = id, Content = text, Score = score });
            }
            return results.OrderByDescending(r => r.Score).Take(topN).ToList();
        }

        // ══════════════════════════════════════════════════════════════════
        // UTILS
        // ══════════════════════════════════════════════════════════════════

        private static HashSet<string> Tokenize(string text) =>
            new(text.ToLowerInvariant()
                    .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\r', '\t' },
                           StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

        private static float JaccardSimilarity(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0f;
            int intersect = a.Count(w => b.Contains(w));
            int union     = a.Count + b.Count - intersect;
            return union == 0 ? 0f : (float)intersect / union;
        }

        /// <summary>Ping Ollama щоб перевірити доступність embedding моделі.</summary>
        public async Task<bool> PingAsync()
        {
            try
            {
                var vec = await GetEmbeddingAsync("ping");
                _available = vec != null;
                return _available;
            }
            catch
            {
                _available = false;
                return false;
            }
        }

        private static void Log(string msg) =>
            System.Diagnostics.Debug.WriteLine($"[KokoEmbeddingService] {msg}");
    }
}
