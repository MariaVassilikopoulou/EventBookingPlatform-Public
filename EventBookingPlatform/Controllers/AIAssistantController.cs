using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;
using EventBookingPlatform.Interfaces;
using EventBookingPlatform.Services;
using EventBookingPlatform.Services.AIServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using System.Security.Claims;

namespace EventBookingPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AIAssistantController : ControllerBase
    {
        private readonly AIAssistantService _aiService;
        private readonly IGenericRepository<Event> _repository;
        private readonly IGenericRepository<Booking> _bookingRepository;
        private readonly EmbeddingService _embeddingService;
        private readonly IBookingService _bookingService;

        public AIAssistantController(
            AIAssistantService aiService,
            IGenericRepository<Event> repository,
            IGenericRepository<Booking> bookingRepository,
            EmbeddingService embeddingService,
            IBookingService bookingService)
        {
            _aiService = aiService;
            _repository = repository;
            _bookingRepository = bookingRepository;
            _embeddingService = embeddingService;
            _bookingService = bookingService;
        }


        [HttpGet("test-ai")]
        [AllowAnonymous]
        public async Task<IActionResult> TestAI()
        {
            var prompt = new EventAIPrompt
            {
                Name = "Mediterranean Cooking Workshop",
                Description = "Learn to cook traditional Mediterranean dishes.",
                Location = "Gothenburg",
                Date = DateTime.UtcNow.AddDays(7),
                SeatsAvailable = 20,
                Price = 50,
                UserQuestion = "Is this workshop suitable for beginners?"
            };

            var answer = await _aiService.AskAboutEventAsync(prompt);
            return Ok(new { answer });
        }



        [HttpPost("ask-about-event")]
        [AllowAnonymous]
        public async Task<IActionResult> AskAboutEvent([FromBody] UserQuestionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UserQuestion))
                return BadRequest("Missing question");
            var events = (await _repository.GetAllAsync()).ToList();
            if (!events.Any())
                return BadRequest("No events available");
            //var keywords = dto.UserQuestion.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
             var keywords = dto.UserQuestion
                            .ToLower()
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Where(w => w.Length > 2)
                            .ToList();

           /* var relevantEvents = events.Where(e => keywords.Any(k => e.Name.Contains(k, StringComparison.OrdinalIgnoreCase)||
                                              e.Location.Contains(k ?? "", StringComparison.OrdinalIgnoreCase))).ToList();*/
                var relevantEvents = events
                                    .Where(e => keywords.Any(k =>
                                        e.Name.ToLower().Contains(k) ||
                                        e.Location.ToLower().Contains(k) 
                                       /* e.Description.ToLower().Contains(k)*/
                                    ))
                                    .ToList();                              
//The controller tries to find relevant events by matching keywords from the user’s question to event names and locations.
//If matches are found, it uses those; otherwise, it uses all events.
            var eventsToUse = relevantEvents.Any() ? relevantEvents : events;
            var eventInfo= events.Select((ev,idx)=>$@"
              Event {idx+1}:
              - Name: {ev.Name}
              - Location:{ev.Location}
              - Date:{ev.Date:yyyy-MM-dd}
              - Seats Availiable: {ev.AvailableSeats}
              - Price: {ev.Price}").ToList();
 
            var prompt = new EventAIPrompt
            {
                UserQuestion = dto.UserQuestion,
                TotalEvents = events.Count,
                AllEventDetails = string.Join("\n", eventInfo)
            };
            try
                {
                    var answer = await _aiService.AskAboutEventWithRecommendationsAsync(prompt, eventsToUse);

                    return Ok(new { answer });
                }
                catch (Exception ex)
                {
                  
                    Console.WriteLine($"AI error: {ex.Message}");

                  
                    return StatusCode(500, "AI is temporarily unavailable. Please try again in a moment.");
                }
        }


        [HttpPost("chat")]
        [AllowAnonymous]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (request.Messages == null || !request.Messages.Any())
                return BadRequest("No messages provided.");

            var events = (await _repository.GetAllAsync()).ToList();
            if (!events.Any())
            {
                return Ok(new
                {
                    answer = new AIResponseDto
                    {
                        AnswerText = "I'd love to help, but there are no events available right now.",
                        RecommendedEvents = new List<EventDto>()
                    }
                });
            }

            // Fetch booking history if user is authenticated
            List<Booking> userBookings = new();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"[AI Chat] userId from token: {userId ?? "null (unauthenticated)"}");
            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId")
                        .WithParameter("@userId", userId);
                    var bookings = await _bookingRepository.QueryAsync(query);
                    userBookings = bookings
                        .Where(b => b.Status != "Cancelled")
                        .ToList();
                    Console.WriteLine($"[AI Chat] Found {userBookings.Count} booking(s) for user.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Chat] Could not fetch booking history: {ex.Message}");
                }
            }

            var answer = await _aiService.AskChatWithEventsAsync(request.Messages, events, userBookings, userId);
            return Ok(new { answer });
        }
        
        [HttpPost("generate-description")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateDescription([FromBody] GenerateDescriptionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.EventName))
                return BadRequest("Event name is required.");
            var description = await _aiService.GenerateEventDescriptionAsync(dto);
            return Ok(new { description });
        }

        [HttpGet("recommended-for-you")]
        [Authorize]
        public async Task<IActionResult> RecommendedForYou()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            List<Booking> userBookings = new();
            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId")
                    .WithParameter("@userId", userId);
                var bookings = await _bookingRepository.QueryAsync(query);
                userBookings = bookings.Where(b => b.Status != "Cancelled").ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recommended] Could not fetch bookings: {ex.Message}");
                return Ok(new object[0]);
            }

            if (!userBookings.Any()) return Ok(new object[0]);

            var bookedEventIds = userBookings.Select(b => b.EventId).ToHashSet();
            var allEvents = (await _repository.GetAllAsync()).ToList();

            var bookedEmbeddings = allEvents
                .Where(e => bookedEventIds.Contains(e.Id) && e.Embedding != null && e.Embedding.Length > 0)
                .Select(e => e.Embedding!)
                .ToList();

            if (!bookedEmbeddings.Any()) return Ok(new object[0]);

            // Average embeddings → user preference vector
            var dim = bookedEmbeddings[0].Length;
            var avgEmbedding = new float[dim];
            foreach (var emb in bookedEmbeddings)
                for (int i = 0; i < dim; i++)
                    avgEmbedding[i] += emb[i] / bookedEmbeddings.Count;

            var upcomingUnbooked = allEvents
                .Where(e => e.Date >= DateTime.UtcNow && !bookedEventIds.Contains(e.Id))
                .ToList();

            var recommended = _embeddingService.FindSimilarEvents(avgEmbedding, upcomingUnbooked, topK: 4);

            var result = recommended.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                description = e.Description,
                date = e.Date,
                location = e.Location,
                price = e.Price,
                totalSeats = e.TotalSeats,
                availableSeats = e.AvailableSeats
            });

            return Ok(result);
        }

        [HttpPost("semantic-search")]
        [AllowAnonymous]
        public async Task<IActionResult> SemanticSearch([FromBody] SemanticSearchDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Query))
                return BadRequest("Query is required.");

            var allEvents = (await _repository.GetAllAsync())
                .Where(e => e.Date >= DateTime.UtcNow)
                .ToList();

            if (!allEvents.Any()) return Ok(new object[0]);

            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(dto.Query);

            List<Event> results;
            if (queryEmbedding != null)
            {
                results = _embeddingService.FindSimilarEvents(queryEmbedding, allEvents, topK: 6);
            }
            else
            {
                var q = dto.Query.ToLower();
                results = allEvents
                    .Where(e => e.Name.ToLower().Contains(q) ||
                                e.Location.ToLower().Contains(q) ||
                                (e.Description != null && e.Description.ToLower().Contains(q)))
                    .Take(6)
                    .ToList();
            }

            var result = results.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                description = e.Description,
                date = e.Date,
                location = e.Location,
                price = e.Price,
                totalSeats = e.TotalSeats,
                availableSeats = e.AvailableSeats
            });

            return Ok(result);
        }

        [HttpPost("execute-booking")]
        [Authorize]
        public async Task<IActionResult> ExecuteBooking([FromBody] ExecuteBookingDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var userName = User.FindFirst("fullName")?.Value ?? "Unknown";

            Console.WriteLine($"[ExecuteBooking] userId={userId}, eventId={dto?.EventId}, eventName={dto?.EventName}, seats={dto?.Seats}");

            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (dto == null || string.IsNullOrEmpty(dto.EventId))
                return BadRequest(new { message = "Invalid booking request." });

            var bookingDto = new CreateBookingDto
            {
                EventId = dto.EventId,
                EventName = dto.EventName,
                Seats = dto.Seats
            };

            var (success, message, booking) = await _bookingService.CreateBookingAsync(userId, userEmail, userName, bookingDto);

            Console.WriteLine($"[ExecuteBooking] success={success}, message={message}, bookingId={booking?.Id}");

            if (!success)
                return BadRequest(new { message });

            return Ok(new
            {
                message = $"You're booked for **{dto.EventName}**! Check your email for confirmation.",
                bookingId = booking!.Id
            });
        }

        public class UserQuestionDto
        {
            public string UserQuestion { get; set; }
        }

        public class SemanticSearchDto
        {
            public string Query { get; set; }
        }

        public class ExecuteBookingDto
        {
            public string EventId { get; set; }
            public string EventName { get; set; }
            public int Seats { get; set; }
        }

    }
}



