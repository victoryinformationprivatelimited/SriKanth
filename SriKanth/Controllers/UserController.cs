using HRIS.API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SriKanth.Interface;
using SriKanth.Interface.Login_Module;
using SriKanth.Model.Login_Module.DTOs;

namespace SriKanth.API.Controllers
{
	/// <summary>
	/// Controller for handling user authentication, authorization, and management operations
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class UserController : ControllerBase
	{
		private readonly IUserService _userService; // User service for handling user-related operations
		private readonly IMfaService _mfaService; // Service for handling Multi-Factor Authentication
		private readonly IConfiguration _configuration; // Configuration for application settings
		private readonly IJwtTokenService _jwtTokenService; // Service for handling JWT tokens
		private readonly IEncryptionService _encryptionService;

		/// <summary>
		/// Initializes a new instance of the <see cref="UserController"/> class.
		/// </summary>
		/// <param name="userService">Service for user-related operations</param>
		/// <param name="configuration">Application configuration settings</param>
		/// <param name="mfaService">Service for Multi-Factor Authentication</param>
		/// <param name="jwtTokenService">Service for JWT token operations</param>
		public UserController(IUserService userService, IConfiguration configuration,
							IMfaService mfaService, IJwtTokenService jwtTokenService, IEncryptionService encryptionService)
		{
			_userService = userService;
			_configuration = configuration;
			_mfaService = mfaService;
			_jwtTokenService = jwtTokenService;
			_encryptionService = encryptionService;
		}

		/// <summary>
		/// Authenticates a user and returns access tokens or MFA requirement
		/// </summary>
		/// <param name="loginRequest">User credentials including username and password</param>
		/// <returns>
		/// Returns JWT tokens if authentication succeeds without MFA,
		/// or MFA requirements if MFA is enabled for the user
		/// </returns>
		[HttpPost("login")]
		[EnableRateLimiting("LoginLimit")]
		public async Task<IActionResult> Login([FromBody] RequestLogin loginRequest)
		{
			try
			{
				// Validate the request model
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				// Attempt user authentication
				var result = await _userService.LoginAsync(loginRequest);

				// Handle failed authentication
				if (!result.Success)
				{
					return Unauthorized(new { userlocked = result.UserLocked, message = result.Message });
				}

				// Return tokens if MFA not required
				if (!result.RequiresMfa)
				{
					return Ok(new { Acctoken = result.AccessToken, RefToken = result.RefreshToken });
				}

				// Return MFA requirements if needed
				return Ok(new { Success = result.Success, Message = result.Message, UserId = result.UserId });
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error:{ex.Message}");
			}
		}

		/// <summary>
		/// Generates new access and refresh tokens using a valid refresh token
		/// </summary>
		/// <param name="request">Contains the refresh token</param>
		/// <returns>New access and refresh tokens if validation succeeds</returns>
		[HttpPost("refresh-token")]
		public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
		{
			// Validate refresh token presence
			if (request?.RefreshToken == null || string.IsNullOrEmpty(request.RefreshToken))
			{
				return BadRequest(new { message = "Refresh token is required." });
			}

			try
			{
				// Attempt to refresh tokens
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

		/// <summary>
		/// Validates a Multi-Factor Authentication code
		/// </summary>
		/// <param name="mfaRequest">Contains user ID and MFA code</param>
		/// <returns>JWT tokens if validation succeeds</returns>
		[HttpPost("validate-mfa")]
		[EnableRateLimiting("LoginLimit")]
		public async Task<IActionResult> ValidateMfa([FromBody] MFAValidationRequest mfaRequest)
		{
			// Validate the request model
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			// Validate MFA code
			var result = await _mfaService.ValidateMfaAsync(mfaRequest.UserId, mfaRequest.MfaCode);

			// Handle failed validation
			if (!result.Success)
			{
				return Unauthorized(new { message = result.Message });
			}

			// Return tokens on successful validation
			return Ok(new { Success = true, Acctoken = result.AccessToken, RefToken = result.RefreshToken });
		}

		/// <summary>
		/// Initiates a password reset process for a user
		/// </summary>
		/// <param name="loginreq">User credentials for password reset</param>
		/// <returns>Confirmation of reset initiation</returns>
		[HttpPost("reset-password")]
		[EnableRateLimiting("LoginLimit")]
		public async Task<IActionResult> PasswordReset([FromBody] RequestLogin loginreq)
		{
			// Validate the request model
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			// Initiate password reset
			var result = await _userService.PasswordResetAsync(loginreq);

			// Handle failed reset request
			if (!result.Success)
			{
				return Unauthorized(new { message = result.Message });
			}

			return Ok(new { UserId = result.UserId, message = result.Message });
		}

		/// <summary>
		/// Sets a new password for a user after reset confirmation
		/// </summary>
		/// <param name="passwordreset">New password information</param>
		/// <returns>Confirmation of password change</returns>
		[HttpPost("new-password")]
		public async Task<IActionResult> SetnewPassword([FromBody] PasswordReset passwordreset)
		{
			// Validate the request model
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			// Store new password
			var result = await _userService.StoreNewpasswordAsync(passwordreset);

			// Handle failed password change
			if (!result.Success)
			{
				return Unauthorized(new { message = result.Message });
			}

			return Ok(new { message = result.Message });
		}

		/// <summary>
		/// Creates a new user account
		/// </summary>
		/// <param name="userDetails">Complete user information</param>
		/// <returns>Confirmation of user creation</returns>
		[HttpPost("AddUser")]
		//[Authorize(Roles = "Admin")] // Only Admins can add new users
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> AddNewUser([FromBody] UserDetails userDetails)
		{
			try
			{
				// Validate the request model
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				// Create new user
				var result = await _userService.CreateUserAsync(userDetails);

				// Handle failed user creation
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

		/// <summary>
		/// Updates an existing user's information
		/// </summary>
		/// <param name="userId">ID of the user to update</param>
		/// <param name="userDetails">Updated user information</param>
		/// <returns>Confirmation of user update</returns>
		[HttpPut("UpdateUser")]
		[Authorize(Roles = "Admin")]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> UpdateUserInfo(int userId, [FromBody] UserDetails userDetails)
		{
			try
			{
				// Validate the request model
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				// Update user information
				var result = await _userService.UpdateUserAsync(userId, userDetails);

				// Handle failed update
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

		/// <summary>
		/// Retrieves details required for user creation (roles, permissions, etc.)
		/// </summary>
		/// <returns>List of user creation options and templates</returns>
		[HttpGet("GetUserCreationDetails")]
		public async Task<IActionResult> GetUserCreationDetails()
		{
			try
			{
				// Get user creation metadata
				var userData = await _userService.GetUserCreationDetailsAsync();
				return Ok(userData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		/// <summary>
		/// Retrieves detailed information for a specific user
		/// </summary>
		/// <param name="userId">ID of the user to retrieve</param>
		/// <returns>Complete user details</returns>
		[HttpGet("GetUserDetailsById")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetUserDetailsById(int userId)
		{
			try
			{
				// Get user details by ID
				var userData = await _userService.GetUserDetailsByIdAsync(userId);
				return Ok(userData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		/// <summary>
		/// Retrieves a list of all users in the system
		/// </summary>
		/// <returns>List of all users with basic information</returns>
		[HttpGet("GetAllUsers")]
		[Authorize(Roles = "Admin")]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetAllUsers()
		{
			try
			{
				// Get all users
				var userData = await _userService.GetListOfUsersAsync();
				return Ok(userData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpPost("encrypt")]
		public IActionResult EncryptText([FromBody] EncryptTextRequest request)
		{
			try
			{
				if (request == null || string.IsNullOrWhiteSpace(request.Text))
				{
					return BadRequest(new { message = "Text is required." });
				}

				var result = _encryptionService.EncryptData(request.Text);
				return Ok(new { encrypted = result });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
			}
		}
		
	}
	public class EncryptTextRequest
	{
		public string Text { get; set; }
	}
}