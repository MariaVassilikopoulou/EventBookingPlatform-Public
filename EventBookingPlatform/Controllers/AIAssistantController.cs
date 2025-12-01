using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.Interfaces;
using EventBookingPlatform.Services.AIServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventBookingPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AIAssistantController : ControllerBase
    {
        private readonly AIAssistantService _aiService;
        private readonly IGenericRepository<Event> _repository;
        public AIAssistantController(AIAssistantService aiService, IGenericRepository<Event> repository)
        {
            _aiService = aiService;
            _repository = repository;
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
            var keywords = dto.UserQuestion.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var relevantEvents = events.Where(e => keywords.Any(k => e.Name.Contains(k, StringComparison.OrdinalIgnoreCase)||
                                              e.Location.Contains(k ?? "", StringComparison.OrdinalIgnoreCase))).ToList();

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
            var answer = await _aiService.AskAboutEventWithRecommendationsAsync(prompt, eventsToUse);
            return Ok(new { answer });
        }


        [HttpPost("chat")]
        [AllowAnonymous]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (request.Messages == null || !request.Messages.Any())
                return BadRequest("No messages provided.");
            var events= (await _repository.GetAllAsync()).ToList();
            if(!events.Any())
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
            var answer = await _aiService.AskChatWithEventsAsync(request.Messages, events);
            return Ok(new { answer });
        }
        
        public class UserQuestionDto
        {
            public string UserQuestion { get; set; }
        
        }

    }
}



