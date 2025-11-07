using EventBookingPlatform.Domain.Models;
using System.Security.Claims;

namespace EventBookingPlatform.Interfaces
{
    public interface ITokenProvider
    {
       
        string Create(User user);

      
        string GenerateRefreshToken();

        // Retrieves the claims principal from an expired token for refresh logic
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);

       
        string? ReadTokenId(string token);
    }
}
