using Microsoft.AspNetCore.Identity;

namespace EventBookingPlatform.Domain.Models
{
    public class User : IdentityUser
    {
        public string FullName { get; set; }= string.Empty;
        public bool IsAdmin { get; set; }   
    }
}
