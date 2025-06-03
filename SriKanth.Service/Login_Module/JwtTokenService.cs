using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SriKanth.Interface;
using SriKanth.Interface.Data;
using SriKanth.Interface.Login_Module;
using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service.Login_Module
{
	public class JwtTokenService : IJwtTokenService
	{
		private readonly ILoginData _userData;
		private readonly IConfiguration _configuration;
		public readonly IEncryptionService _encryption;
		private readonly ILogger<JwtTokenService> _logger;

		/// <summary>
		/// Initializes a new instance of the JwtTokenService class.
		/// </summary>
		/// <param name="dbContext">The database context used for storing tokens.</param>
		/// <param name="configuration">Configuration for accessing JWT settings.</param>
		/// <param name="encryption">Service for encrypting token data.</param>
		/// <param name="logger">Logger to log important events.</param>
		public JwtTokenService(ILoginData userData, IConfiguration configuration, IEncryptionService encryption, ILogger<JwtTokenService> logger)
		{
			_userData = userData;
			_configuration = configuration;
			_encryption = encryption;
			_logger = logger;

		}

		/// <summary>
		/// Generates a JWT token for the specified user and stores it in the database.
		/// </summary>
		/// <param name="user">The user for whom the JWT token is generated.</param>
		/// <returns>The generated JWT token as a string.</returns>
		public async Task<string> GenerateJwtToken(User user)
		{
			try
			{
				string email = _encryption.DecryptData(user.Email);
				string uname = _encryption.DecryptData(user.Username);
				string number = _encryption.DecryptData(user.PhoneNumber);

				// Determine the token expiration based on the "RememberMe" flag.
				var tokenExpiration = user.RememberMe ? DateTime.UtcNow.AddDays(07) : DateTime.UtcNow.AddHours(1);

				var claims = new List<Claim>
				{
					new Claim(JwtRegisteredClaimNames.Email, email),
					new Claim(JwtRegisteredClaimNames.Name, uname),
					new Claim(ClaimTypes.MobilePhone, number),
					new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
				};
				// Define the token descriptor with claims and signing credentials.
				var tokenDescriptor = new SecurityTokenDescriptor
				{
					Subject = new ClaimsIdentity(claims),
					Audience = _configuration["Jwt:Audience"],
					Issuer = _configuration["Jwt:Issuer"],
					Expires = tokenExpiration,
					SigningCredentials = new SigningCredentials(
						new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])),
						SecurityAlgorithms.HmacSha512Signature)
				};

				// Create the JWT token using the token handler.
				var tokenHandler = new JwtSecurityTokenHandler();
				var token = tokenHandler.CreateToken(tokenDescriptor);
				var jwtToken = tokenHandler.WriteToken(token);

				// Encrypt the JWT token before storing it in the database.
				string hashedjwt = _encryption.EncryptData(jwtToken);

				// Create a new UserToken entry and save it to the database.
				var userToken = new UserToken
				{
					UserID = user.UserID,
					Token = hashedjwt,
					TokenType = "JWT",
					CreatedAt = DateTime.UtcNow,
					ExpiresAt = tokenExpiration,
					IsUsed = false,
					IsRevoked = false,
					Purpose = "Authentication"
				};
				await _userData.AddUserTokenAsync(userToken);
				_logger.LogInformation("JWT token generated and stored for user {UserId}.", user.UserID);
				return jwtToken;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while generating JWT token for user {UserId}.", user.UserID);
				throw;
			}
		}

		/// <summary>
		/// Generates a new refresh token for the specified user and stores it in the database.
		/// </summary>
		/// <param name="user">The user for whom the refresh token is generated.</param>
		/// <returns>The generated refresh token as a string.</returns>
		public async Task<string> GenerateRefreshToken(User user)
		 {
			// Generate a random 32-byte refresh token
			var randomNumber = new byte[32];
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(randomNumber);
			}

			// Convert the byte array to a base64 string
			var refreshToken = Convert.ToBase64String(randomNumber);
			// Encrypt the refresh token before storing it.
			string hashedRefToken = _encryption.EncryptData(refreshToken);
			// Define the expiration time for the refresh token (e.g., 7 days)
			var tokenExpiration = DateTime.UtcNow.AddDays(7);

			// Create a new UserToken entry and save it to the database.
			var userToken = new UserToken
			{
				UserID = user.UserID,
				Token = hashedRefToken,
				TokenType = "RefreshToken",
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = tokenExpiration,
				IsUsed = false,
				IsRevoked = false,
				Purpose = "RefreshToken"
			};

			// Save the token to the database
			await _userData.AddUserTokenAsync(userToken);
			return hashedRefToken;
		}

		/// <summary>
		/// Refreshes an expired access token using a valid refresh token.
		/// </summary>
		/// <param name="refreshToken">The refresh token used to refresh the access token.</param>
		/// <returns>A new access token and refresh token.</returns>
		public async Task<AuthenticatedResponse> RefreshToken(string hashedRefToken)
		{
			try
			{

				// Fetch the refresh token from the database asynchronously
				var storedRefreshToken = await _userData.GetUserTokenAsync(hashedRefToken);
				if (storedRefreshToken == null || storedRefreshToken.ExpiresAt < DateTime.UtcNow)
				{
					throw new SecurityTokenException("Invalid or expired refresh token.");
				}
				// Mark the refresh token as used
				await _userData.UpdateUserTokenUsedAsync(hashedRefToken);

				// Fetch the user associated with this token asynchronously
				var user = await _userData.GetUserByIdAsync(storedRefreshToken.UserID);
				if (user == null)
				{
					throw new Exception("User not found.");
				}

				// Generate a new access token
				var newAccessToken = await GenerateJwtToken(user);

				// Optionally, create a new refresh token as well
				var newRefreshToken = await GenerateRefreshToken(user);

				// Return the new access and refresh tokens
				return new AuthenticatedResponse
				{
					AccessToken = newAccessToken,
					RefreshToken = newRefreshToken
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while refreshing token.");
				throw;
			}
		}

		/// <summary>
		/// Retrieves the claims principal from an expired JWT token.
		/// </summary>
		/// <param name="token">The expired JWT token.</param>
		/// <returns>The ClaimsPrincipal extracted from the token.</returns>
		public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
		{
			// Define token validation parameters to ignore expiration.
			var tokenValidationParameters = new TokenValidationParameters
			{
				ValidateAudience = false,
				ValidateIssuer = false,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])),
				ValidateLifetime = false
			};

			var tokenHandler = new JwtSecurityTokenHandler();
			SecurityToken securityToken;
			var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
			var jwtSecurityToken = securityToken as JwtSecurityToken;

			// Verify the token algorithm.
			if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512Signature, StringComparison.InvariantCultureIgnoreCase))
				throw new SecurityTokenException("Invalid token");

			return principal;
		}
	}
}
