using AutoMapper;
using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;

namespace EventBookingPlatform.MappingProfiles
{
    public class MappingProfiles: Profile
    {
        public MappingProfiles() 
        {
          CreateMap<CreateEventDto,Event>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(_ => Guid.NewGuid().ToString()))
                .ForMember(dest => dest.AvailableSeats, opt => opt.MapFrom(src => src.TotalSeats));

            CreateMap<UpdateEventDto, Event>()
                .ForMember(dest=>dest.AvailableSeats, opt=>opt.MapFrom(src=> src.TotalSeats));


            CreateMap<CreateBookingDto, Booking>()
                   .ForMember(dest => dest.Id, opt => opt.MapFrom(_ => Guid.NewGuid().ToString()))
                   .ForMember(dest => dest.BookingDate, opt => opt.MapFrom(_ => DateTime.UtcNow))
                   .ForMember(dest => dest.PartitionKey, opt => opt.Ignore());

        }
    }
}
