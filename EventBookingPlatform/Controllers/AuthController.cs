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
           
            var user = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                IsAdmin = false 
            };

           
            var initialAdminEmail = _configuration["Admin:InitialAdminEmail"];
            if (!string.IsNullOrEmpty(initialAdminEmail) && dto.Email.Equals(initialAdminEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.IsAdmin = true;
            }

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (result.Succeeded)
            {
                
                return await GenerateAuthTokenResponse(user);
            }

            
            return BadRequest(result.Errors.Select(e => e.Description).ToList());
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
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
            
            var token = _tokenProvider.Create(user);

           
            var refreshToken = _tokenProvider.GenerateRefreshToken();

            

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
