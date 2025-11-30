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

        //[HttpPost("ask-about-event")]
        //[AllowAnonymous]

        //public async Task<IActionResult> AskAboutEvent([FromBody] EventAIPrompt prompt)
        //{
        //    if (prompt == null || string.IsNullOrWhiteSpace(prompt.UserQuestion))
        //        return BadRequest("Missing or invalid event prompt data in the request body.");

        //    var answer = await _aiService.AskAboutEventAsync(prompt);
        //    return Ok(new { answer });
        //}


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
            var relevantEvents = events
                .Where(e => keywords.Any(k => e.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                                             || e.Location.Contains(k ?? "", StringComparison.OrdinalIgnoreCase)))
                .ToList();

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
                //Name = events[0].Name,
                ////Description = firstEvent.Description,
                //Location = events[0].Location,
                //Date = events[0].Date,
                //SeatsAvailable = events[0].AvailableSeats,
                //Price = events[0].Price,
                //UserQuestion = dto.UserQuestion,
                //TotalEvents = events.Count(),
                //AllEventDetails = string.Join("\n", eventInfo)
                UserQuestion = dto.UserQuestion,
                TotalEvents = events.Count,
                AllEventDetails = string.Join("\n", eventInfo)

            };

            var answer = await _aiService.AskAboutEventAsync(prompt);
            return Ok(new { answer });
        }

        // DTO for frontend request
        public class UserQuestionDto
        {
            public string UserQuestion { get; set; }
        
        }

        }
    }



