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

        private readonly EmbeddingService _embeddingService;
        private readonly HttpClient _ticketmasterClient;
        private readonly string _ticketmasterApiKey;

        public AIAssistantService(IConfiguration configuration, EmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
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
            _ticketmasterClient = new HttpClient();
            _ticketmasterApiKey = configuration["Ticketmaster:ApiKey"] ?? "";
        }

        // === Main Public Methods ===
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

        private string BuildPrompt(EventAIPrompt p)
        {
            return $@"
                  Here is event information:
                  User question: {p.UserQuestion}
                - Total number of events stored: {p.TotalEvents}
                  Details of all events:{p.AllEventDetails}
                  Answer in a friendly and helpful way.If the user asks for cheapest, most expensive, by date, or for recommendations, compute it from the list above.";
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

        public async Task<AIResponseDto> AskChatWithEventsAsync(
            List<ChatMessage> messages,
            List<Event> events,
            List<Booking>? userBookings = null,
            string? authenticatedUserId = null)
        {
            try
            {
                var eventInfo = events.Select((ev, idx) => $@"
                Event {idx + 1}:
                - ID: {ev.Id}
                - Name: {ev.Name}
                - Location: {ev.Location}
                - Date: {ev.Date:yyyy-MM-dd}
                - Seats Available: {ev.AvailableSeats} / {ev.TotalSeats}
                - Price: {ev.Price:C}
                - Status: {(ev.AvailableSeats == 0 ? "SOLD OUT" : ev.AvailableSeats <= 10 ? $"LOW AVAILABILITY ({ev.AvailableSeats} seats left)" : "Available")}").ToList();

                var today = DateTime.UtcNow;
                var isAuthenticated = !string.IsNullOrEmpty(authenticatedUserId);

                var userHistorySection = "";
                if (userBookings != null && userBookings.Any())
                {
                    var bookedNames = string.Join(", ", userBookings.Select(b => b.EventName));
                    userHistorySection = $@"
                USER BOOKING HISTORY:
                This user has previously booked: {bookedNames}.
                Use this to personalise recommendations — favour similar event types and avoid suggesting events they have already booked, unless they ask about them.
                ";
                }

                var bookingSection = isAuthenticated
                    ? @"
                BOOKING — CRITICAL RULES:
                - You have a book_event FUNCTION TOOL. When the user asks to book an event (words like ""book"", ""reserve"", ""sign me up"", ""I want to book""), you MUST call the book_event tool immediately. Do NOT write text saying you will book — call the function.
                - Use the exact event ID from the event data above.
                - Default to 1 seat unless the user specifies a number.
                - You also have a check_availability tool for live seat counts. Call it when the user asks about availability.
                - You also have a search_city_events tool. Use it whenever the user asks what's happening in a specific city (e.g. ""What's on in Stockholm?"", ""Events in Gothenburg this weekend"").
                - When listing events from search_city_events, format each one on its own line as: **Event Name** — Date at Venue ([Buy tickets](url)). If a url is not available for an event, omit the link for that event only.
                - After listing external events, ALWAYS mention the most relevant FlowEvent platform events the user can book right now."
                    : @"
                CITY EVENTS — CRITICAL:
                - You have a search_city_events tool. When the user asks about events in ANY city, you MUST call the search_city_events tool immediately. Do NOT say there are no events — call the tool first and use its results.
                - When listing events from search_city_events, format each one on its own line as: **Event Name** — Date at Venue ([Buy tickets](url)). If no url, omit the link.
                - After listing external events, always add: ""Log in to FlowEvent to book events like these instantly — no forms, no redirects.""
                BOOKING:
                - The user is not logged in. If they ask to book a FlowEvent event, tell them to log in first.";

                // Separate variable to avoid nested interpolated string literals (C# < 11 limitation)
                var responseFormatSection = isAuthenticated
                    ? "RESPONSE STYLE: Reply in plain conversational text. Never say you will book something — call the book_event function immediately."
                    : @"RESPONSE FORMAT — CRITICAL: you MUST always respond with valid JSON and nothing else, in exactly this shape:
{
  ""answer"": ""your response here"",
  ""suggestions"": [""short follow-up 1"", ""short follow-up 2"", ""short follow-up 3""]
}
For suggestions, use city-based queries like: ""What's on in Stockholm?"", ""Music in Gothenburg"", ""Events in Malmö"".";

                var eventContext = $@"
                You are an event booking assistant for FlowEvent. Today is {today:dddd, MMMM d yyyy}.

                EVENT DATA (use for reasoning only — do NOT list them in your reply):
                {string.Join("\n", eventInfo)}
                {userHistorySection}
                {bookingSection}

                RULES FOR YOUR ANSWERS:
                - NEVER list all events in text format.
                - NEVER output raw event details (the frontend shows cards).
                - Answer in 1–3 sentences only.
                - NEVER recommend events whose date has already passed (before {today:yyyy-MM-dd}).
                - If the user mentions a city or location, call search_city_events first (if available), then refer to FlowEvent events in that location.
                - If an event has 10 or fewer seats remaining, always mention it: e.g. ""Only X seats left — book soon!"".
                - If an event is happening within the next 48 hours, always flag urgency: e.g. ""This is happening very soon!"".
                - If the user asks about timing (this weekend, this month, next week, tonight), filter events by date accordingly.
                - Do NOT repeat event details. The backend attaches recommended event cards separately.

                {responseFormatSection}
                ";

                var searchCityEventsTool = new
                {
                    type = "function",
                    function = new
                    {
                        name = "search_city_events",
                        description = "Search for real-world events happening in a specific city using Ticketmaster. Use this when the user asks what's on in a city or mentions a location with a date range.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                city      = new { type = "string", description = "City name, e.g. Gothenburg" },
                                date_from = new { type = "string", description = "Start date ISO YYYY-MM-DD. Default: today." },
                                date_to   = new { type = "string", description = "End date ISO YYYY-MM-DD. Default: 30 days from today." },
                                category  = new { type = "string", description = "Optional: Music, Sports, Arts, Family, etc." }
                            },
                            required = new[] { "city" }
                        }
                    }
                };

                var tools = isAuthenticated ? new object[]
                {
                    new
                    {
                        type = "function",
                        function = new
                        {
                            name = "book_event",
                            description = "Book an event for the current user. Only call this when the user explicitly asks to book a specific event.",
                            parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    event_id = new { type = "string", description = "The exact ID of the event from the event data" },
                                    event_name = new { type = "string", description = "The name of the event" },
                                    seats = new { type = "integer", description = "Number of seats to book. Default to 1 if not specified." }
                                },
                                required = new[] { "event_id", "event_name", "seats" }
                            }
                        }
                    },
                    new
                    {
                        type = "function",
                        function = new
                        {
                            name = "check_availability",
                            description = "Check the number of available seats for a specific event.",
                            parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    event_id = new { type = "string", description = "The ID of the event to check" }
                                },
                                required = new[] { "event_id" }
                            }
                        }
                    },
                    searchCityEventsTool
                } : new object[] { searchCityEventsTool };

                var conversationMessages = new List<object>
                {
                    new { role = "system", content = eventContext }
                };
                conversationMessages.AddRange(messages.Select(m => (object)new
                {
                    role = m.Role.ToLower(),
                    content = m.Content
                }));

                // Always send tools (unauth gets search_city_events, auth gets all three)
                // response_format cannot be combined with tools — JSON format enforced via system prompt for unauth
                var requestJson = System.Text.Json.JsonSerializer.Serialize(new { messages = conversationMessages, tools, tool_choice = "auto" });

                using var requestContent = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_chatCompletionUrl, requestContent);
                if (!response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"OpenAI request failed: {response.StatusCode} - {text}");
                    return new AIResponseDto { AnswerText = "Sorry, I couldn't get a response.", RecommendedEvents = new() };
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
                var choice = result?.choices?[0];

                // Handle tool calls
                if (choice?.finish_reason == "tool_calls" && choice.message?.tool_calls != null)
                {
                    foreach (var toolCall in choice.message.tool_calls)
                    {
                        if (toolCall.function.name == "book_event")
                        {
                            var args = System.Text.Json.JsonSerializer.Deserialize<BookEventArgs>(toolCall.function.arguments);
                            if (args == null) break;

                            var ev = events.FirstOrDefault(e => e.Id == args.event_id);
                            var answerText = ev != null
                                ? $"I found **{args.event_name}** — {ev.Date:MMMM d, yyyy} in {ev.Location}. It's {ev.Price:C} per seat. Want me to book {args.seats} seat{(args.seats > 1 ? "s" : "")} for you?"
                                : $"I found **{args.event_name}**. Shall I go ahead and book {args.seats} seat{(args.seats > 1 ? "s" : "")} for you?";

                            return new AIResponseDto
                            {
                                AnswerText = answerText,
                                RecommendedEvents = new(),
                                Suggestions = new() { "Yes, book it!", "No thanks", "Show me more events" },
                                PendingBooking = new PendingBooking
                                {
                                    EventId = args.event_id,
                                    EventName = args.event_name,
                                    Seats = args.seats,
                                    Price = ev?.Price ?? 0,
                                    EventDate = ev?.Date.ToString("MMMM d, yyyy") ?? ""
                                }
                            };
                        }

                        if (toolCall.function.name == "check_availability")
                        {
                            var args = System.Text.Json.JsonSerializer.Deserialize<CheckAvailabilityArgs>(toolCall.function.arguments);
                            var ev = events.FirstOrDefault(e => e.Id == args?.event_id);
                            var toolResult = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                available_seats = ev?.AvailableSeats ?? 0,
                                event_name = ev?.Name ?? "Unknown"
                            });

                            var followUpMessages = new List<object>(conversationMessages)
                            {
                                new
                                {
                                    role = "assistant",
                                    content = (string?)null,
                                    tool_calls = choice.message.tool_calls.Select(tc => new
                                    {
                                        id = tc.id,
                                        type = tc.type,
                                        function = new { name = tc.function.name, arguments = tc.function.arguments }
                                    }).ToArray()
                                },
                                new { role = "tool", tool_call_id = toolCall.id, content = toolResult }
                            };

                            var followUpJson = System.Text.Json.JsonSerializer.Serialize(new { messages = followUpMessages });
                            using var followUpContent = new StringContent(followUpJson, System.Text.Encoding.UTF8, "application/json");
                            var followUpResponse = await _httpClient.PostAsync(_chatCompletionUrl, followUpContent);

                            if (!followUpResponse.IsSuccessStatusCode) break;
                            var followUpResult = await followUpResponse.Content.ReadFromJsonAsync<OpenAIResponse>();
                            choice = followUpResult?.choices?[0];
                            break; // fall through to plain-text parsing below
                        }

                        if (toolCall.function.name == "search_city_events")
                        {
                            var args = System.Text.Json.JsonSerializer.Deserialize<SearchCityEventsArgs>(toolCall.function.arguments);
                            Console.WriteLine($"[Ticketmaster] Searching city={args?.city}, from={args?.date_from}, to={args?.date_to}, category={args?.category}");
                            var toolResult = await SearchTicketmasterAsync(args?.city, args?.date_from, args?.date_to, args?.category);

                            var followUpMessages = new List<object>(conversationMessages)
                            {
                                new
                                {
                                    role = "assistant",
                                    content = (string?)null,
                                    tool_calls = choice.message.tool_calls.Select(tc => new
                                    {
                                        id = tc.id,
                                        type = tc.type,
                                        function = new { name = tc.function.name, arguments = tc.function.arguments }
                                    }).ToArray()
                                },
                                new { role = "tool", tool_call_id = toolCall.id, content = toolResult }
                            };

                            // Unauth users need response_format in follow-up so the AI returns JSON with suggestions
                            var followUpJson = isAuthenticated
                                ? System.Text.Json.JsonSerializer.Serialize(new { messages = followUpMessages })
                                : System.Text.Json.JsonSerializer.Serialize(new { messages = followUpMessages, response_format = new { type = "json_object" } });
                            using var followUpContent = new StringContent(followUpJson, System.Text.Encoding.UTF8, "application/json");
                            var followUpResponse = await _httpClient.PostAsync(_chatCompletionUrl, followUpContent);

                            if (!followUpResponse.IsSuccessStatusCode) break;
                            var followUpResult = await followUpResponse.Content.ReadFromJsonAsync<OpenAIResponse>();
                            choice = followUpResult?.choices?[0];
                            break;
                        }
                    }
                }

                // Parse response — authenticated users get plain text, unauthenticated get JSON
                var rawContent = choice?.message?.content ?? "";
                string finalAnswer;
                List<string> suggestions = new();

                if (isAuthenticated)
                {
                    // Plain text response expected — no JSON parsing
                    finalAnswer = string.IsNullOrWhiteSpace(rawContent) ? "I'm here to help!" : rawContent;
                }
                else
                {
                    AIJsonResponse parsedResponse;
                    try
                    {
                        parsedResponse = System.Text.Json.JsonSerializer.Deserialize<AIJsonResponse>(rawContent)
                            ?? new AIJsonResponse { answer = "No response.", suggestions = new() };
                    }
                    catch
                    {
                        parsedResponse = new AIJsonResponse { answer = rawContent, suggestions = new() };
                    }
                    finalAnswer = parsedResponse.answer ?? "No response.";
                    suggestions = parsedResponse.suggestions ?? new();
                }

                var lastUserMessage = messages.LastOrDefault(m => m.Role.ToLower() == "user")?.Content ?? "";
                var recommendedEvents = UserIsAskingForEvents(lastUserMessage)
                    ? await GetRecommendedEventsAsync(lastUserMessage, messages, events, finalAnswer)
                    : new List<EventDto>();

                return new AIResponseDto
                {
                    AnswerText = finalAnswer,
                    RecommendedEvents = recommendedEvents,
                    Suggestions = suggestions
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

        private class BookEventArgs
        {
            public string event_id { get; set; }
            public string event_name { get; set; }
            public int seats { get; set; }
        }

        private class CheckAvailabilityArgs
        {
            public string event_id { get; set; }
        }

        private class SearchCityEventsArgs
        {
            public string? city { get; set; }
            public string? date_from { get; set; }
            public string? date_to { get; set; }
            public string? category { get; set; }
        }

        private static readonly Dictionary<string, (double lat, double lon)> _cityCoords = new(StringComparer.OrdinalIgnoreCase)
        {
            // Sweden
            ["gothenburg"]  = (57.7089, 11.9746),
            ["göteborg"]    = (57.7089, 11.9746),
            ["stockholm"]   = (59.3293, 18.0686),
            ["malmö"]       = (55.6050, 13.0038),
            ["malmo"]       = (55.6050, 13.0038),
            ["uppsala"]     = (59.8586, 17.6389),
            ["linköping"]   = (58.4108, 15.6214),
            ["linkoping"]   = (58.4108, 15.6214),
            ["helsingborg"] = (56.0465, 12.6945),
            ["örebro"]      = (59.2741, 15.2066),
            ["orebro"]      = (59.2741, 15.2066),
            ["västerås"]    = (59.6099, 16.5448),
            ["vasteras"]    = (59.6099, 16.5448),
            ["norrköping"]  = (58.5877, 16.1924),
            ["norrkoping"]  = (58.5877, 16.1924),
            ["jönköping"]   = (57.7826, 14.1618),
            ["jonkoping"]   = (57.7826, 14.1618),
            // Nordics
            ["copenhagen"]  = (55.6761, 12.5683),
            ["oslo"]        = (59.9139, 10.7522),
            ["helsinki"]    = (60.1699, 24.9384),
            // Europe
            ["london"]      = (51.5074, -0.1278),
            ["berlin"]      = (52.5200, 13.4050),
            ["paris"]       = (48.8566,  2.3522),
            ["amsterdam"]   = (52.3676,  4.9041),
        };

        private async Task<string> SearchTicketmasterAsync(string? city, string? dateFrom, string? dateTo, string? category)
        {
            if (string.IsNullOrEmpty(_ticketmasterApiKey) || string.IsNullOrEmpty(city))
                return "{\"events\": []}";

            var from = dateFrom ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
            var to   = dateTo   ?? DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

            string locationParam;
            if (_cityCoords.TryGetValue(city.Trim(), out var coords))
                locationParam = $"&latlong={coords.lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{coords.lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radius=50&unit=km";
            else
                locationParam = $"&city={Uri.EscapeDataString(city)}";

            var url = "https://app.ticketmaster.com/discovery/v2/events.json" +
                      $"?apikey={_ticketmasterApiKey}" +
                      locationParam +
                      $"&startDateTime={from}T00:00:00Z&endDateTime={to}T23:59:59Z" +
                      "&size=5&sort=date,asc" +
                      (string.IsNullOrEmpty(category) ? "" : $"&classificationName={Uri.EscapeDataString(category)}");

            try
            {
                var response = await _ticketmasterClient.GetAsync(url);
                Console.WriteLine($"[Ticketmaster] HTTP {(int)response.StatusCode} for city={city}");
                if (!response.IsSuccessStatusCode) return "{\"events\": []}";

                var json = await response.Content.ReadAsStringAsync();
                return MapTicketmasterResponse(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ticketmaster] Request error: {ex.Message}");
                return "{\"events\": []}";
            }
        }

        private static string MapTicketmasterResponse(string json)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("_embedded", out var embedded) ||
                    !embedded.TryGetProperty("events", out var eventsArr))
                {
                    Console.WriteLine("[Ticketmaster] No events in response.");
                    return "{\"events\": []}";
                }

                var events = new List<object>();
                foreach (var ev in eventsArr.EnumerateArray())
                {
                    var name = ev.TryGetProperty("name", out var n) ? n.GetString() : "Unknown";

                    string? date = null;
                    if (ev.TryGetProperty("dates", out var dates) &&
                        dates.TryGetProperty("start", out var start) &&
                        start.TryGetProperty("localDate", out var ld))
                        date = ld.GetString();

                    string? url = ev.TryGetProperty("url", out var u) ? u.GetString() : null;

                    string? venue = null, city = null;
                    if (ev.TryGetProperty("_embedded", out var evEmb) &&
                        evEmb.TryGetProperty("venues", out var venues) &&
                        venues.GetArrayLength() > 0)
                    {
                        var v = venues[0];
                        if (v.TryGetProperty("address", out var addr) && addr.TryGetProperty("line1", out var line1))
                            venue = line1.GetString();
                        if (v.TryGetProperty("city", out var vc) && vc.TryGetProperty("name", out var cn))
                            city = cn.GetString();
                    }

                    decimal? minPrice = null;
                    if (ev.TryGetProperty("priceRanges", out var pr) && pr.GetArrayLength() > 0 &&
                        pr[0].TryGetProperty("min", out var minVal))
                        minPrice = minVal.GetDecimal();

                    events.Add(new { name, date, venue, city, minPrice, url });
                }

                Console.WriteLine($"[Ticketmaster] Parsed {events.Count} event(s).");
                return System.Text.Json.JsonSerializer.Serialize(new { events });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ticketmaster] Parse error: {ex.Message}");
                return "{\"events\": []}";
            }
        }

        private async Task<List<EventDto>> GetRecommendedEventsAsync(
            string userMessage, List<ChatMessage> messages, List<Event> events, string aiResponse)
        {
            // Try semantic search first — only for events that already have embeddings stored
            var eventsWithEmbeddings = events.Where(e => e.Embedding != null && e.Embedding.Length > 0).ToList();
            if (eventsWithEmbeddings.Any())
            {
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(userMessage);
                if (queryEmbedding != null)
                {
                    var similar = _embeddingService.FindSimilarEvents(queryEmbedding, eventsWithEmbeddings, topK: 3);
                    if (similar.Any())
                    {
                        Console.WriteLine($"[RAG] Semantic search returned {similar.Count} event(s) for: \"{userMessage}\"");
                        return similar.Select(ev => new EventDto
                        {
                            Name = ev.Name,
                            Location = ev.Location,
                            Date = ev.Date,
                            Price = ev.Price,
                            SeatsAvailable = ev.AvailableSeats
                        }).ToList();
                    }
                }
            }

            // Fall back to keyword matching for events without embeddings
            Console.WriteLine($"[RAG] Falling back to keyword matching (no embeddings available).");
            return SelectRelevantEvents(messages, events, aiResponse);
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

            IEnumerable<Event> filteredEvents = events.Where(e => e.Date >= DateTime.UtcNow);

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

        public async Task<string> GenerateEventDescriptionAsync(GenerateDescriptionDto dto)
        {
            try
            {
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = "You are an event marketing copywriter. Write compelling, specific event descriptions that make people want to attend. Focus on the atmosphere, who it's for, and why they shouldn't miss it. Be enthusiastic but concise. Return only the description text with no extra formatting or labels." },
                        new { role = "user", content = $"Write a 2-3 sentence marketing description for this event:\nName: {dto.EventName}\nLocation: {dto.Location}\nDate: {dto.Date}\nPrice: {dto.Price} SEK" }
                    }
                };
                var response = await _httpClient.PostAsJsonAsync(_chatCompletionUrl, requestBody);
                if (!response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"OpenAI generate-description failed: {response.StatusCode} - {text}");
                    return "Could not generate description. Please write one manually.";
                }
                var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
                return result?.choices?[0]?.message?.content ?? "No response.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateEventDescriptionAsync: {ex.Message}");
                return "Could not generate description. Please write one manually.";
            }
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
    }

    // === DTOs and Helper Classes ===
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
        public string finish_reason { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string? content { get; set; }
        public List<ToolCall>? tool_calls { get; set; }
    }

    public class ToolCall
    {
        public string id { get; set; }
        public string type { get; set; }
        public ToolCallFunction function { get; set; }
    }

    public class ToolCallFunction
    {
        public string name { get; set; }
        public string arguments { get; set; }
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
        public List<string> Suggestions { get; set; } = new();
        public PendingBooking? PendingBooking { get; set; }
    }

    public class PendingBooking
    {
        public string EventId { get; set; }
        public string EventName { get; set; }
        public int Seats { get; set; }
        public decimal Price { get; set; }
        public string EventDate { get; set; }
    }

    public class GenerateDescriptionDto
    {
        public string EventName { get; set; }
        public string Location { get; set; }
        public string Date { get; set; }
        public string Price { get; set; }
    }

    public class AIJsonResponse
    {
        public string answer { get; set; }
        public List<string> suggestions { get; set; } = new();
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


