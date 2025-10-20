
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _repository.DeleteAsync(id,id);
            if (!success) return NotFound();
            return NoContent();

        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateEventDto dto)
        {
            var existing = await _repository.GetByIdAsync(id, id);
            if (existing == null) return NotFound();

            _mapper.Map(dto, existing);

            var updated = await _repository.UpdateAsync(existing, existing.PartitionKey);
            return Ok(updated);
        }



    }
}
