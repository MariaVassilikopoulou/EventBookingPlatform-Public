using EventBookingPlatform.Domain.Models;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using SendGrid.Helpers.Mail;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

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
            _endpoint = configuration["OpenAI:Endpoint"]
                 ?? throw new Exception("OpenAI:Endpoint is missing");
            _model = configuration["OpenAI:Model"]
                 ?? "gpt-4o-mini"; 
            _apiVersion = configuration["OpenAI:ApiVersion"]
                ?? throw new Exception("OpenAI:ApiVersion is missing");
            _chatCompletionUrl = $"{_endpoint}openai/deployments/{_model}/chat/completions?api-version={_apiVersion}";
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
        }


        public async Task<string> AskAboutEventAsync(EventAIPrompt prompt)
        {
            var requestBody = new{messages = new[]{
            new { role = "system", content = "You are an event assistant. Give clear, friendly, helpful answers." },
            new { role = "user", content = BuildPrompt(prompt) }}};
            var response = await _httpClient.PostAsJsonAsync(_chatCompletionUrl, requestBody); 
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"OpenAI request failed: {response.StatusCode} - {text}");
                return "Sorry, I couldn't get a response.";
            }
            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            return result?.choices?[0]?.message?.content ?? "No response.";
        }


        private bool UserIsAskingForEvents(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return false;
            var keywords = new[]
            {
                "event", "events", "recommend", "show", "list", "kids",
                "cheapest", "expensive", "workshop", "concert", "cheap",
                "family", "things to do", "activities"
            };
            var lower = new string(userMessage.ToLower().Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            return keywords.Any(k => lower.Contains(k));
        }


        public async Task<AIResponseDto> AskChatWithEventsAsync(List<ChatMessage> messages, List<Event> events)
        {
            try
            {
                var eventInfo = events.Select((ev, idx) => $@"
                Event {idx + 1}:
                - Name: {ev.Name}
                - Location: {ev.Location}
                - Date: {ev.Date:yyyy-MM-dd}
                - Seats Available: {ev.AvailableSeats}
                - Price: {ev.Price}").ToList();

                var eventContext = $@"
                You are an event booking assistant. You have access to {events.Count} events.

                    EVENT DATA (for your reasoning, NOT for output):
                    {string.Join("\n", eventInfo)}

                    RULES FOR YOUR ANSWERS:
                    - NEVER list all events in text format.
                    - NEVER output the event details again (the frontend will show cards).
                    - ONLY answer in 1–3 sentences.
                    - If the user asks about kids, price, location, or dates, explain your reasoning briefly.
                    - Do NOT repeat the events. The backend will attach recommended events separately.

                    Your job: give a short helpful message, while the frontend displays event cards.
                    ";
                var messagesWithContext = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = eventContext }
                };
                messagesWithContext.AddRange(messages);
                var requestBody = new
                {
                    messages = messagesWithContext.Select(m => new
                    {
                        role = m.Role.ToLower(),
                        content = m.Content
                    })
                };

                var response = await _httpClient.PostAsJsonAsync(_chatCompletionUrl, requestBody);
                if (!response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"OpenAI request failed: {response.StatusCode} - {text}");
                    return new AIResponseDto
                    {
                        AnswerText = "Sorry, I couldn't get a response.",
                        RecommendedEvents = new List<EventDto>()
                    };
                }
                var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
                var answerText = result?.choices?[0]?.message?.content ?? "No response.";
                var lastUserMessage = messages.LastOrDefault(m => m.Role.ToLower() == "user")?.Content ?? "";
                var recommendedEvents = UserIsAskingForEvents(lastUserMessage)
                    ? SelectRelevantEvents(messages, events, answerText)
                    : new List<EventDto>();

                return new AIResponseDto
                {
                    AnswerText = answerText,
                    RecommendedEvents = recommendedEvents
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AskChatWithEventsAsync: {ex.Message}");
                return new AIResponseDto
                {
                    AnswerText = "Sorry, something went wrong. Please try again.",
                    RecommendedEvents = new List<EventDto>()
                };
            }
        }

        private List<EventDto> SelectRelevantEvents(List<ChatMessage> messages, List<Event> events, string aiResponse)
        {
            var lastUserMessage = messages.LastOrDefault(m => m.Role.ToLower() == "user")?.Content?.ToLower() ?? "";
            var aiResponseLower = aiResponse.ToLower();

            // === PRICE-BASED QUERIES ===
            bool askingForCheapest = lastUserMessage.Contains("cheap") ||
                                     lastUserMessage.Contains("affordable") ||
                                     lastUserMessage.Contains("budget") ||
                                     lastUserMessage.Contains("inexpensive") ||
                                     lastUserMessage.Contains("low cost") ||
                                     lastUserMessage.Contains("lowest price") ||
                                     lastUserMessage.Contains("save money") ||
                                     lastUserMessage.Contains("economical");

            bool askingForExpensive = lastUserMessage.Contains("expensive") ||
                                      lastUserMessage.Contains("luxury") ||
                                      lastUserMessage.Contains("premium") ||
                                      lastUserMessage.Contains("high-end") ||
                                      lastUserMessage.Contains("pricey") ||
                                      lastUserMessage.Contains("costly") ||
                                      lastUserMessage.Contains("most expensive") ||
                                      lastUserMessage.Contains("highest price");

            // === DATE-BASED QUERIES ===
            bool askingForDate = lastUserMessage.Contains("when") ||
                                 lastUserMessage.Contains("date") ||
                                 lastUserMessage.Contains("upcoming") ||
                                 lastUserMessage.Contains("soon") ||
                                 lastUserMessage.Contains("next") ||
                                 lastUserMessage.Contains("this month") ||
                                 lastUserMessage.Contains("this year") ||
                                 lastUserMessage.Contains("soonest") ||
                                 lastUserMessage.Contains("earliest") ||
                                 lastUserMessage.Contains("coming up");

            // === LOCATION-BASED QUERIES ===
            bool askingForLocation = lastUserMessage.Contains("where") ||
                                     lastUserMessage.Contains("location") ||
                                     lastUserMessage.Contains("near me") ||
                                     lastUserMessage.Contains("in gothenburg") ||
                                     lastUserMessage.Contains("in malmö") ||
                                     lastUserMessage.Contains("in sweden") ||
                                     lastUserMessage.Contains("city") ||
                                     lastUserMessage.Contains("place");

            // === CATEGORY-BASED QUERIES ===
            bool askingForMusic = lastUserMessage.Contains("music") ||
                                  lastUserMessage.Contains("concert") ||
                                  lastUserMessage.Contains("festival") ||
                                  lastUserMessage.Contains("rock") ||
                                  lastUserMessage.Contains("band") ||
                                  lastUserMessage.Contains("live music");

            bool askingForTech = lastUserMessage.Contains("tech") ||
                                 lastUserMessage.Contains("technology") ||
                                 lastUserMessage.Contains("expo") ||
                                 lastUserMessage.Contains("innovation") ||
                                 lastUserMessage.Contains("digital");

            bool askingForArt = lastUserMessage.Contains("art") ||
                                lastUserMessage.Contains("design") ||
                                lastUserMessage.Contains("creative") ||
                                lastUserMessage.Contains("exhibition") ||
                                lastUserMessage.Contains("gallery");

            bool askingForFood = lastUserMessage.Contains("food") ||
                                 lastUserMessage.Contains("cooking") ||
                                 lastUserMessage.Contains("cuisine") ||
                                 lastUserMessage.Contains("workshop") ||
                                 lastUserMessage.Contains("culinary") ||
                                 lastUserMessage.Contains("recipe") ||
                                 lastUserMessage.Contains("italian") ||
                                 lastUserMessage.Contains("chef");

            bool askingForPhotography = lastUserMessage.Contains("photo") ||
                                        lastUserMessage.Contains("photography") ||
                                        lastUserMessage.Contains("camera") ||
                                        lastUserMessage.Contains("masterclass");

            // === CAPACITY-BASED QUERIES ===
            bool askingForSmallEvents = lastUserMessage.Contains("small") ||
                                        lastUserMessage.Contains("intimate") ||
                                        lastUserMessage.Contains("cozy") ||
                                        lastUserMessage.Contains("few people");

            bool askingForLargeEvents = lastUserMessage.Contains("large") ||
                                        lastUserMessage.Contains("big") ||
                                        lastUserMessage.Contains("huge") ||
                                        lastUserMessage.Contains("massive") ||
                                        lastUserMessage.Contains("popular") ||
                                        lastUserMessage.Contains("many people");

            bool askingForAvailability = lastUserMessage.Contains("available") ||
                                         lastUserMessage.Contains("seats left") ||
                                         lastUserMessage.Contains("still open") ||
                                         lastUserMessage.Contains("can i book") ||
                                         lastUserMessage.Contains("tickets available");

            IEnumerable<Event> filteredEvents = events;

            // === APPLY FILTERS ===

            // Price filters
            if (askingForCheapest)
            {
                filteredEvents = events.OrderBy(e => e.Price);
            }
            else if (askingForExpensive)
            {
                filteredEvents = events.OrderByDescending(e => e.Price);
            }
            // Date filters
            else if (askingForDate)
            {
                filteredEvents = events.OrderBy(e => e.Date);
            }
            // Category filters
            else if (askingForMusic)
            {
                filteredEvents = events.Where(e =>
                    e.Name.ToLower().Contains("music") ||
                    e.Name.ToLower().Contains("rock") ||
                    e.Name.ToLower().Contains("festival") ||
                    e.Name.ToLower().Contains("concert")
                ).OrderBy(e => e.Date);
            }
            else if (askingForTech)
            {
                filteredEvents = events.Where(e =>
                    e.Name.ToLower().Contains("tech") ||
                    e.Name.ToLower().Contains("expo")
                ).OrderBy(e => e.Date);
            }
            else if (askingForArt)
            {
                filteredEvents = events.Where(e =>
                    e.Name.ToLower().Contains("art") ||
                    e.Name.ToLower().Contains("design")
                ).OrderBy(e => e.Date);
            }
            else if (askingForFood)
            {
                filteredEvents = events.Where(e =>
                    e.Name.ToLower().Contains("cooking") ||
                    e.Name.ToLower().Contains("cuisine") ||
                    e.Name.ToLower().Contains("food")
                ).OrderBy(e => e.Date);
            }
            else if (askingForPhotography)
            {
                filteredEvents = events.Where(e =>
                    e.Name.ToLower().Contains("photo")
                ).OrderBy(e => e.Date);
            }
            // Size filters
            else if (askingForSmallEvents)
            {
                filteredEvents = events.Where(e => e.TotalSeats <= 100).OrderBy(e => e.TotalSeats);
            }
            else if (askingForLargeEvents)
            {
                filteredEvents = events.Where(e => e.TotalSeats >= 500).OrderByDescending(e => e.TotalSeats);
            }
            // Availability filters
            else if (askingForAvailability)
            {
                filteredEvents = events.Where(e => e.AvailableSeats > 0).OrderByDescending(e => e.AvailableSeats);
            }
            // Location-specific filters
            else if (askingForLocation)
            {
                // Extract location from message
                var locationKeywords = new[] { "gothenburg", "malmö", "helsingborg", "trelleborg", "berlin", "los angeles", "sweden", "germany", "usa" };
                var mentionedLocation = locationKeywords.FirstOrDefault(loc => lastUserMessage.Contains(loc));

                if (!string.IsNullOrEmpty(mentionedLocation))
                {
                    filteredEvents = events.Where(e =>
                        e.Location.ToLower().Contains(mentionedLocation)
                    ).OrderBy(e => e.Date);
                }
            }
            else
            {
                // Default: Try to match event names mentioned in AI response or user query
                var relevantEvents = events.Where(e =>
                    aiResponseLower.Contains(e.Name.ToLower()) ||
                    lastUserMessage.Contains(e.Name.ToLower()) ||
                    lastUserMessage.Contains(e.Location.ToLower())
                ).ToList();

                if (relevantEvents.Any())
                {
                    filteredEvents = relevantEvents;
                }
                else
                {
                    // If no specific match, show upcoming events
                    filteredEvents = events.OrderBy(e => e.Date);
                }
            }
            // Return top 3 events
            return filteredEvents
                .Take(3)
                .Select(ev => new EventDto
                {
                    Name = ev.Name,
                    Location = ev.Location,
                    Date = ev.Date,
                    Price = ev.Price,
                    SeatsAvailable = ev.AvailableSeats
                })
                .ToList();
        }

        public async Task<string> AskChatAsync(List<ChatMessage> messages)
        {
            var requestBody = new
            {
                messages = messages
            };
            var response = await _httpClient.PostAsJsonAsync(_chatCompletionUrl, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"OpenAI request failed: {response.StatusCode} - {text}");
                return "Sorry, I couldn't get a response.";
            }
            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            return result?.choices?[0]?.message?.content ?? "No response.";
        }


        public async Task<AIResponseDto> AskAboutEventWithRecommendationsAsync(EventAIPrompt prompt, List<Event> eventsToUse)
        {
            var answerText = await AskAboutEventAsync(prompt);

            var includeEvents = UserIsAskingForEvents(prompt.UserQuestion);

            var topEvents = includeEvents
                ? eventsToUse
                    .Take(3)
                    .Select(ev => new EventDto
                    {
                        Name = ev.Name,
                        Location = ev.Location,
                        Date = ev.Date,
                        Price = ev.Price,
                        SeatsAvailable = ev.AvailableSeats
                    })
                    .ToList()
                : new List<EventDto>();

            
            return new AIResponseDto
            {
                AnswerText = answerText,
                RecommendedEvents = topEvents
            };
        }


        private string BuildPrompt(EventAIPrompt p)
        {
            return $@"
                  Here is event information:
                  User question: {p.UserQuestion}
                - Total number of events stored: {p.TotalEvents}
                  Details of all events:{p.AllEventDetails}
                  Answer in a friendly and helpful way.If the user asks for cheapest, most expensive, by date, or for recommendations, compute it from the list above.";
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

    public class ChatRequest
    {
        public List<ChatMessage> Messages { get; set; }
    }

    public class ChatMessage
    {
        public string Role { get; set; }  
        public string Content { get; set; }
    }


    public class AIResponseDto
    {
        public string AnswerText { get; set; }
        public List<EventDto> RecommendedEvents { get; set; }
    }

    public class EventDto
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public int SeatsAvailable { get; set; }
    }

}


