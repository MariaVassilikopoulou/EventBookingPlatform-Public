using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;
using EventBookingPlatform.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EventBookingPlatform.Controllers
{
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenProvider _tokenProvider;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ITokenProvider tokenProvider,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenProvider = tokenProvider;
            _configuration = configuration;
        }

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // Use Email as UserName, as is common in modern APIs
            var user = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                IsAdmin = false // Default to standard user
            };

            // **Admin Seeding Logic (Optional, but often needed for setup)**
            // If you want the very first user or a specific email to be an admin:
            var initialAdminEmail = _configuration["Admin:InitialAdminEmail"];
            if (!string.IsNullOrEmpty(initialAdminEmail) && dto.Email.Equals(initialAdminEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.IsAdmin = true;
            }

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (result.Succeeded)
            {
                // Sign the user in and generate the tokens
                return await GenerateAuthTokenResponse(user);
            }

            // Return detailed errors if registration failed
            return BadRequest(result.Errors.Select(e => e.Description).ToList());
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Login data missing!");
            }
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                return Unauthorized("Invalid credentials.");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return await GenerateAuthTokenResponse(user);
            }

            if (result.IsLockedOut)
            {
                return Unauthorized("User account locked.");
            }

            return Unauthorized("Invalid credentials.");
        }



        private async Task<IActionResult> GenerateAuthTokenResponse(User user)
        {
            // 1. Generate JWT (handled by your custom provider)
            var token = _tokenProvider.Create(user);

            // 2. Generate Refresh Token (optional, but good practice)
            var refreshToken = _tokenProvider.GenerateRefreshToken();

            // Note: In a real app, you would save the refresh token to the database here.

            return Ok(new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email!,
                Token = token,
                RefreshToken = refreshToken,
                IsAdmin = user.IsAdmin,
                FullName = user.FullName
            });
        }

    }


}
