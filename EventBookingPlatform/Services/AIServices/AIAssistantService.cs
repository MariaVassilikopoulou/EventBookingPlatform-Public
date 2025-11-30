namespace EventBookingPlatform.Services.AIServices
{
    public class AIAssistantService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _model;
        private readonly string _chatCompletionUrl;
        private readonly string _apiVersion;


        public AIAssistantService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();

            _apiKey = configuration["OpenAI:ApiKey"]
                 ?? throw new Exception("OpenAI:ApiKey is missing");

            // Base Endpoint (e.g., https://aiagenthelper.openai.azure.com/)
            _endpoint = configuration["OpenAI:Endpoint"]
                 ?? throw new Exception("OpenAI:Endpoint is missing");

            _model = configuration["OpenAI:Model"]
                 ?? "gpt-4o-mini"; // This is the deployment name on Azure

            _apiVersion = configuration["OpenAI:ApiVersion"]
                ?? throw new Exception("OpenAI:ApiVersion is missing");

            // Construct the full Azure OpenAI URL
            // Format: {endpoint}/openai/deployments/{model}/chat/completions?api-version={apiVersion}
            _chatCompletionUrl = $"{_endpoint}openai/deployments/{_model}/chat/completions?api-version={_apiVersion}";

            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            // Azure OpenAI is specific, you may also need to set the Content-Type header
            // For PostAsJsonAsync, this is usually handled, but good to be aware.
        }


        public async Task<string> AskAboutEventAsync(EventAIPrompt prompt)
        {
            var requestBody = new
            {
                messages = new[]
                {
            new { role = "system", content = "You are an event assistant. Give clear, friendly, helpful answers." },
            new { role = "user", content = BuildPrompt(prompt) }
        }
            };

            // Use the new, correct URL for the request
            var response = await _httpClient.PostAsJsonAsync(_chatCompletionUrl, requestBody); 

            // The rest of your error handling and parsing is now correct and will catch 
            // issues *after* a successful connection is made.
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"OpenAI request failed: {response.StatusCode} - {text}");
                return "Sorry, I couldn't get a response.";
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            return result?.choices?[0]?.message?.content ?? "No response.";
        }

        private string BuildPrompt(EventAIPrompt p)
        {
            return $@"
                        Here is event information:
                        User question: {p.UserQuestion}
                        Name: {p.Name}
                        
                        Location: {p.Location}
                        
                       
                        Price: {p.Price}

                        - Total number of events stored: {p.TotalEvents}
                        
                        Details of all events:{p.AllEventDetails}
                        Answer in a friendly and helpful way.
                        ";
        }
    }
    public class EventAIPrompt
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }
        public int SeatsAvailable { get; set; }
        public decimal Price { get; set; }
        public string UserQuestion { get; set; }
        public int TotalEvents { get; set; }
        public string AllEventDetails { get; set; }
    }

    public class OpenAIResponse
    {
        public List<Choice> choices { get; set; }
    }

    public class Choice
    {
        public Message message { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}


