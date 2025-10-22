using EventBookingPlatform.Domain.Models;
using System.Security.Claims;

namespace EventBookingPlatform.Interfaces
{
    public interface ITokenProvider
    {
        // Creates the JWT token for the authenticated user
        string Create(User user);

        // Generates a refresh token (if needed for long-lived sessions)
        string GenerateRefreshToken();

        // Retrieves the claims principal from an expired token for refresh logic
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);

        // Reads a specific claim (like the User ID) from the token without full validation
        string? ReadTokenId(string token);
    }
}
