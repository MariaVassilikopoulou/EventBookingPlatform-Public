
using AutoMapper;
using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;
using EventBookingPlatform.Interfaces;
using EventBookingPlatform.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EventBookingPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class EventsController : ControllerBase
    {
        private readonly IGenericRepository<Event> _repository;
        private readonly IMapper _mapper;
        public EventsController(IGenericRepository<Event> repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;

        }


        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _repository.GetAllAsync());


        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(string id)
        {
            var ev = await _repository.GetByIdAsync(id, id);
            if (ev == null)
                return NotFound();

            return Ok(ev);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEventDto dto)
        {
            var ev= _mapper.Map<Event>(dto);
            var created = await _repository.AddAsync(ev);

            return Created($"/api/events/{created.Id}", created);
        }


    }
}
