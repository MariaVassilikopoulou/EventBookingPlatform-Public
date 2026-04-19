using EventBookingPlatform.Domain.Models;

namespace EventBookingPlatform.Services.AIServices
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _embeddingUrl;

        public EmbeddingService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new Exception("OpenAI:ApiKey is missing");
            var endpoint = configuration["OpenAI:Endpoint"]
                ?? throw new Exception("OpenAI:Endpoint is missing");
            var embeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
            var apiVersion = configuration["OpenAI:ApiVersion"]
                ?? throw new Exception("OpenAI:ApiVersion is missing");

            _embeddingUrl = $"{endpoint}openai/deployments/{embeddingModel}/embeddings?api-version={apiVersion}";
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        /// <summary>
        /// Calls Azure OpenAI to generate an embedding vector for the given text.
        /// Returns null if the call fails.
        /// </summary>
        public async Task<float[]?> GenerateEmbeddingAsync(string text)
        {
            try
            {
                var requestBody = new { input = text };
                var response = await _httpClient.PostAsJsonAsync(_embeddingUrl, requestBody);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Embedding] API error {response.StatusCode}: {error}");
                    return null;
                }
                var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
                return result?.data?[0]?.embedding;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Embedding] GenerateEmbeddingAsync failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Builds a text representation of an event for embedding.
        /// </summary>
        public static string BuildEventText(Event ev) =>
            $"{ev.Name}. {ev.Description ?? ""}. Located in {ev.Location}.".Trim();

        /// <summary>
        /// Returns the top-K events most semantically similar to the query embedding.
        /// Only considers events that already have an embedding stored.
        /// </summary>
        public List<Event> FindSimilarEvents(float[] queryEmbedding, List<Event> events, int topK = 3)
        {
            return events
                .Where(e => e.Embedding != null && e.Embedding.Length > 0)
                .Select(e => new { Event = e, Score = CosineSimilarity(queryEmbedding, e.Embedding!) })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Event)
                .ToList();
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0f;
            float dot = 0f, magA = 0f, magB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            return (magA == 0 || magB == 0) ? 0f : dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
        }
    }

    // === Response DTOs ===
    public class EmbeddingResponse
    {
        public List<EmbeddingData> data { get; set; } = new();
    }

    public class EmbeddingData
    {
        public float[] embedding { get; set; } = Array.Empty<float>();
    }
}
