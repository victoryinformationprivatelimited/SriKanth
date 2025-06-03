using HRIS.API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SriKanth.Interface.Login_Module;
using SriKanth.Model.Login_Module.DTOs;

namespace SriKanth.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class UserController : ControllerBase
	{
		private readonly IUserService _userService; // User service for handling user-related operations
		private readonly IMfaService _mfaService; // Service for handling Multi-Factor Authentication
		private readonly IConfiguration _configuration; // Configuration for application settings
		private readonly IJwtTokenService _jwtTokenService; // Service for handling JWT tokens

		/// <summary>
		/// Initializes a new instance of the <see cref="UserController"/> class.
		/// </summary>
		/// <param name="userService">The user service.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="mfaService">The MFA service.</param>
		public UserController(IUserService userService, IConfiguration configuration, IMfaService mfaService, IJwtTokenService jwtTokenService)
		{
			_userService = userService;
			_configuration = configuration;
			_mfaService = mfaService;
			_jwtTokenService = jwtTokenService;
		}

		// Endpoint for user login
		/// <summary>
		/// Endpoint for user login.
		/// </summary>
		/// <param name="loginRequest">The login request data.</param>
		/// <returns>Returns an action result indicating the login outcome.</returns>
		[HttpPost("login")]
		[EnableRateLimiting("LoginLimit")]
		public async Task<IActionResult> Login([FromBody] RequestLogin loginRequest)
		{
			try
			{   // Check if the model state is valid
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}
				// Attempt to log the user in
				var result = await _userService.LoginAsync(loginRequest);
				// Check if login was unsuccessful
				if (!result.Success)
				{
					return Unauthorized(new { userlocked = result.UserLocked, message = result.Message });
				}

				// If MFA is required
				if (!result.RequiresMfa)
				{
					return Ok(new { Acctoken = result.AccessToken, RefToken = result.RefreshToken });
				}
				return Ok(new { Success = result.Success, Message = result.Message, UserId = result.UserId, });
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error:{ex.Message}");
			}
		}

		/// <summary>
		/// Endpoint for refreshing tokens.
		/// </summary>
		/// <param name="refreshTokenRequest">The refresh token request data.</param>
		/// <returns>Returns an action result with the new access and refresh tokens.</returns>
		[HttpPost("refresh-token")]
		public async Task<IActionResult> RefreshToken( [FromBody] RefreshTokenRequest request)
		{
			// Validate the refresh token request
			if (request?.RefreshToken == null || string.IsNullOrEmpty(request.RefreshToken))
			{
				return BadRequest(new { message = "Refresh token is required." });
			}

			try
			{
				var response = await _jwtTokenService.RefreshToken(request.RefreshToken);
				return Ok(new
				{
					AccessToken = response.AccessToken,
					RefreshToken = response.RefreshToken
				});
			}
			catch (SecurityTokenException)
			{
				return Unauthorized(new { message = "Invalid or expired refresh token." });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "An error occurred while refreshing the token.", error = ex.Message });
			}
		}

		// Endpoint for MFA code validation
		/// <summary>
		/// Endpoint for MFA code validation.
		/// </summary>
		/// <param name="mfaRequest">The MFA validation request data.</param>
		/// <returns>Returns an action result indicating the validation outcome.</returns>
		[HttpPost("validate-mfa")]
		[EnableRateLimiting("LoginLimit")]
		public async Task<IActionResult> ValidateMfa([FromBody] MFAValidationRequest mfaRequest)
		{
			// Validate the request model state
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			// Attempt to validate the MFA code
			var result = await _mfaService.ValidateMfaAsync(mfaRequest.UserId, mfaRequest.MfaCode);
			// Check if validation was unsuccessful
			if (!result.Success)
			{
				return Unauthorized(new { message = result.Message });
			}

			// If MFA is successfully validated, return JWT token
			return Ok(new { Success = true, Acctoken = result.AccessToken, RefToken = result.RefreshToken });
		}

		/// <summary>
		/// Endpoint for password reset.
		/// </summary>
		/// <param name="loginreq">The login request data for password reset.</param>
		/// <returns>Returns an action result indicating the reset outcome.</returns>
		[HttpPost("reset-password")]
		[EnableRateLimiting("LoginLimit")]
		public async Task<IActionResult> PasswordReset([FromBody] RequestLogin loginreq)
		{
			// Validate the request model state
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			// Attempt to reset the password
			var result = await _userService.PasswordResetAsync(loginreq);

			if (!result.Success)
			{
				return Unauthorized(new { message = result.Message });
			}

			return Ok(new { UserId = result.UserId, message = result.Message });
		}


		/// <summary>
		/// Endpoint for setting a new password.
		/// </summary>
		/// <param name="passwordreset">The new password request data.</param>
		/// <returns>Returns an action result indicating the outcome.</returns>
		[HttpPost("new-password")]
		public async Task<IActionResult> SetnewPassword([FromBody] PasswordReset passwordreset)
		{
			// Validate the request model state
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			// Attempt to store the new password
			var result = await _userService.StoreNewpasswordAsync(passwordreset);

			if (!result.Success)
			{
				return Unauthorized(new { message = result.Message });
			}

			return Ok(new { message = result.Message });
		}

		[HttpPost("AddUser")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> AddNewUser([FromBody] UserDetails userDetails)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				var result = await _userService.CreateUserAsync(userDetails);

				if (!result.Success)
				{
					return BadRequest(new { message = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error:{ex.InnerException}");
			}
		}

		[HttpPut("UpdateUser")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> UpdateUserInfo(int userId, [FromBody] UserDetails userDetails)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				var result = await _userService.UpdateUserAsync(userId, userDetails);

				if (!result.Success)
				{
					return BadRequest(new { message = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error:{ex.InnerException}");
			}
		}

		[HttpGet("GetUserCreationDetails")]
		public async Task<IActionResult> GetUserCreationDetails()
		{
			try
			{
				var userData = await _userService.GetUserCreationDetailsAsync();
				return Ok(userData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetUserDetailsById")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetUserDetailsById(int userId)
		{
			try
			{
				var userData = await _userService.GetUserDetailsByIdAsync(userId);
				return Ok(userData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}
	}
}
