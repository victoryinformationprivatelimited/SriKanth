using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Ocsp;
using SriKanth.Interface;
using SriKanth.Interface.Data;
using SriKanth.Interface.Login_Module;
using SriKanth.Interface.SalesModule;
using SriKanth.Model;
using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service.Login_Module
{
	public class UserService : IUserService
	{
		private readonly IEncryptionService _encryption;
		private readonly ILoginData _userData;
		private readonly ILogger<UserService> _logger;
		private readonly TrackLogin _trackLogin;
		private readonly IMfaService _mfaService;
		private readonly IJwtTokenService _jwtTokenService;
		private readonly SriKanthDbContext _dbContext;
		private readonly IConfiguration _configuration;
		public readonly IExternalApiService _externalApiService;
		public UserService(IEncryptionService encryption, IConfiguration configuration, ILoginData loginData, ILogger<UserService> logger,TrackLogin trackLogin, IMfaService mfaService, IJwtTokenService jwtTokenService, SriKanthDbContext dbContext, IExternalApiService externalApiService) 
		{
			_encryption = encryption;
			_userData = loginData;
			_logger = logger;
			_trackLogin = trackLogin;
			_mfaService = mfaService;
			_dbContext = dbContext;
			_jwtTokenService = jwtTokenService;
			_configuration = configuration;
			_externalApiService = externalApiService;
		}

		/// <summary>
		/// Handles user login attempts, including password verification, account lockout for multiple failed attempts, 
		/// multi-factor authentication (MFA) checks, and JWT token generation upon successful login.
		/// </summary>
		/// <param name="loginRequest">Request object containing login details such as username or email and password.</param>
		/// <returns>LoginResult object indicating success, whether MFA is required, and any error messages.</returns>
		public async Task<LoginResult> LoginAsync(RequestLogin loginRequest)
		{
			try
			{
				_logger.LogInformation("Login attempt started for the user with username or email: {UsernameOrEmail}.", loginRequest.UsernameOrEmail);

				// Fetch user by username or email
				var user = await _userData.GetUserByUsernameOrEmailAsync(_encryption.EncryptData(loginRequest.UsernameOrEmail));

				if (user != null)
				{
					user.RememberMe = loginRequest.Rememberme;
				}

				// If the user doesn't exist or the password is incorrect
				if (user == null || !VerifyPassword(loginRequest.Password, user.PasswordHash))
				{
					// Increment failed login count and lock account if necessary
					if (user != null)
					{
						user.FailedLoginCount++;
						if (user.FailedLoginCount >= 4)
						{
							user.IsLocked = true;
							await _userData.UpdateUserAsync(user);
							_logger.LogWarning("Account locked due to multiple failed login attempts for user: {FullName}.", user.FirstName + " " + user.LastName);
							return new LoginResult { Success = false, UserLocked = true, Message = "Account locked due to multiple failed attempts. Please contact the administrator." };
						}
						await _userData.UpdateUserAsync(user);
						await _trackLogin.TrackLoginAsync(user, false, "Password", false, null, "Incorrect Password");
					}
					_logger.LogWarning("Invalid login attempt for user: {FullName}.", loginRequest.UsernameOrEmail);
					return new LoginResult { Success = false, Message = "Invalid username or password." };
				}

				// Check if the user's account is locked
				if (user.IsLocked)
				{
					await _trackLogin.TrackLoginAsync(user, false, "Password", false, null, "User Locked");
					_logger.LogWarning("Login attempt for a locked account: {FullName}.", user.FirstName + " " + user.LastName);
					return new LoginResult { Success = false, UserLocked = true, Message = "Account is locked .Please Contact Admin" };
				}

				// Check if multi-factor authentication (MFA) is enabled and send a code if required
				if (await IsMfaEnabledAndGetType(user) is (true, var mfatype))
				{
					string message = string.Empty;
					string email = user.Email != null ? _encryption.DecryptData(user.Email) : null;
					string number = user.PhoneNumber != null ? _encryption.DecryptData(user.PhoneNumber) : null;

					if (mfatype == "Email" && user.IsEmailVerified)
					{
						message = $"MFA code sent to Verified Email: {email}";
					}
					else if (mfatype == "Sms" && user.IsPhoneNumberVerified)
					{
						message = $"MFA code sent to Verified Phone Number: {number}";
					}
					else
					{
						// Handles failed login attempts by incrementing the failed login count
						user.FailedLoginCount++;
						if (user.FailedLoginCount >= 4)
						{
							// Locks the user's account after multiple failed attempts and sends a locked account email
							user.IsActive = false;
							await _userData.UpdateUserAsync(user);
							_logger.LogWarning("Account locked due to multiple failed login attempts for user: {FullName}.", user.FirstName + " " + user.LastName);
							return new LoginResult { Success = false, UserLocked = true, Message = "Multiple Failed Attempts" };
						}
						await _userData.UpdateUserAsync(user);
						await _trackLogin.TrackLoginAsync(user, false, "Password", false, null, "Incorrect MFA Type");
						return new LoginResult { Success = false, Message = "No valid MFA method available for the user." };
					}

					// Send the MFA code and track the result
					bool result = await _mfaService.SendMfaCodeAsync(user, mfatype);
					if (result)
					{
						await _trackLogin.TrackLoginAsync(user, true, "Password", true, mfatype, null);
						_logger.LogInformation("MFA code successfully sent to user: {UserId}.", user.UserID);
						return new LoginResult { Success = true, RequiresMfa = true, UserId = user.UserID, Message = message };
					}
					else
					{
						await _trackLogin.TrackLoginAsync(user, false, "Password", true, mfatype, "MFA Sent Failed");
						_logger.LogError("Failed to send MFA code to user: {UserId}.", user.UserID);
						return new LoginResult { Success = false, Message = "An error occurred while sending the MFA code. Please try again." };

					}
				}
				// If no MFA is required, reset failed login count and generate access tokens
				user.FailedLoginCount = 0;
				user.LastLoginAt = DateTime.Now;
				await _userData.UpdateUserAsync(user);
				_logger.LogInformation("Updated the last login time for user: {UserId}.", user.UserID);

				// Track login attempt
				await _trackLogin.TrackLoginAsync(user, true, "Password", false, null, null);

				//Genereate Tokens
				var Accesstoken = await _jwtTokenService.GenerateJwtToken(user);
				var RefreshToken = await _jwtTokenService.GenerateRefreshToken(user);

				_logger.LogInformation("Login successful without MFA for user: {UserId}.", user.UserID);
				return new LoginResult { Success = true, RequiresMfa = false, AccessToken = Accesstoken, RefreshToken = RefreshToken, Message = "Successfully Logged" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred during login for the user with username or email: {UsernameOrEmail}.", loginRequest.UsernameOrEmail);
				return new LoginResult { Success = false, Message = "An error occurred during login. Please try again." };
			}
		}

		public async Task<LoginResult> PasswordResetAsync(RequestLogin loginReq)
		{
			try
			{
				// Attempt to find the user by email or username.
				var user = await _userData.GetUserByUsernameOrEmailAsync(loginReq.UsernameOrEmail);

				// If the user is not found, log a warning and return a failure result.
				if (user == null)
				{
					_logger.LogWarning("User with email or phone {UsernameOrEmail} not found for password reset", loginReq.UsernameOrEmail);
					return new LoginResult { Success = false, Message = "User not found" };
				}

				_logger.LogInformation("Initiates Password Reset for User {UsernameOrEmail}", loginReq.UsernameOrEmail);

				// Check if MFA is enabled and determine the MFA type (Email or SMS).
				if (await IsMfaEnabledAndGetType(user) is (true, var mfatype))
				{
					string message = string.Empty;
					string email = user.Email != null ? _encryption.DecryptData(user.Email) : null;
					string number = user.PhoneNumber != null ? _encryption.DecryptData(user.PhoneNumber) : null;

					// Check if MFA type is email and the email is verified.
					if (mfatype == "Email" && user.IsEmailVerified)
					{
						message = $"MFA code sent to Verified Email: {email}";
					}
					// Check if MFA type is SMS and the phone number is verified.
					else if (mfatype == "Sms" && user.IsPhoneNumberVerified)
					{
						message = $"MFA code sent to Verified Phone Number: {number}";
					}
					// If no valid MFA method is available, return a failure result.
					else
					{
						return new LoginResult { Success = false, Message = "No valid MFA method available." };
					}

					// Send the MFA code and check if it was sent successfully.
					bool result = await _mfaService.SendMfaCodeAsync(user, mfatype);
					if (result)
					{
						_logger.LogInformation("Password Reset code sent to user {UserId}.", user.UserID);
						return new LoginResult { Success = true, RequiresMfa = true, UserId = user.UserID, Message = message };
					}

					_logger.LogWarning("An error occurred while sending MFA code.");
					return new LoginResult { Success = false, Message = "Unable to send MFA Code" };
				}

				_logger.LogWarning("MFA not enabled for user {FullName}.", user.FirstName + " " + user.LastName);
				return new LoginResult { Success = false, Message = "MFA not enabled for this user" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred during MFA password reset for {UsernameOrEmail}.", loginReq.UsernameOrEmail);
				return new LoginResult { Success = false, Message = "An error occurred. Please try again." };
			}
		}

		public async Task<LoginResult> StoreNewpasswordAsync(PasswordReset passwordreset)
		{
			try
			{
				// Check if the new password matches the confirmed password.
				if (passwordreset.NewPassword != passwordreset.ConfirmPassword)
				{
					return new LoginResult { Success = false, Message = "New Password and Confirm Password do not match." };
				}

				// Attempt to find the user by their user ID.
				var user = await _userData.GetUserByIdAsync(passwordreset.UserId);
				if (user == null)
				{
					// Return a failure result if the user is not found.
					return new LoginResult { Success = false, Message = "User not found." };
				}

				// Hash the new password and update the user's password in the database.
				user.PasswordHash = _encryption.EncryptData(passwordreset.NewPassword);
				await _dbContext.SaveChangesAsync();

				_logger.LogInformation("Password reset successfully for user {UserId}.", user.UserID);
				return new LoginResult { Success = true, Message = "Password reset successfully." };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while resetting password for user {UserId}.", passwordreset.UserId);
				return new LoginResult { Success = false, Message = "An error occurred. Please try again." };
			}
		}

		public async Task<bool> LockAccountAsync(RequestLogin loginRequest)
		{
			try
			{
				// Retrieve the user by email or username
				var user = await _userData.GetUserByUsernameOrEmailAsync(loginRequest.UsernameOrEmail);
				// If the user is not found, log a warning and return false
				if (user == null)
				{
					_logger.LogWarning("User with email or phone {EmailOrPhoneNumber} not found.", loginRequest.UsernameOrEmail);
					return false;
				}

				// Lock the user's account
				user.IsActive = false;
				user.IsLocked = true;
				await _userData.UpdateUserAsync(user);
				_logger.LogInformation("Account locked for user {UserId}.", user.UserID);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while locking the account for {UsernameOrEmail}.", loginRequest.UsernameOrEmail);
				return false;
			}
		}

		public async Task<bool> UnlockAccountAsync(RequestLogin loginRequest)
		{
			try
			{
				// Retrieve the user by email or username
				var user = await _userData.GetUserByUsernameOrEmailAsync(loginRequest.UsernameOrEmail);
				// If the user is not found, log a warning and return false
				if (user == null)
				{
					_logger.LogWarning("User with email or phone {EmailOrPhoneNumber} not found.", loginRequest.UsernameOrEmail);
					return false;
				}
				// Unlock the user's account
				user.IsActive = true;
				user.IsLocked = false;
				await _userData.UpdateUserAsync(user);
				_logger.LogInformation("Account Unlocked for user {UserId}.", user.UserID);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while unlocking the account for {UsernameOrEmail}.", loginRequest.UsernameOrEmail);
				return false;
			}
		}
		
		

		
		public bool VerifyPassword(string enteredPassword, string storedPasswordHash)
		{
			try
			{
				string loginPassword = _encryption.EncryptData(enteredPassword);

				if (loginPassword != storedPasswordHash)
				{
					return false;
				}
				return true;
			}

			catch (Exception ex)
			{
				Console.WriteLine($"Error during password verification: {ex.Message}");
				return false;
			}
		}
		public async Task<ServiceResult> CreateUserAsync(UserDetails userDetails)
		{
			try
			{
				_logger.LogInformation("Attempting to create new user with Username: {Username} or Email: {Email}", userDetails.Username, userDetails.Email);

				// Check if a user already exists with the given username or email
				var existingUser = await _userData.CheckUserByUsernameOrEmailAsync(_encryption.EncryptData(userDetails.Username), _encryption.EncryptData(userDetails.Email));
				if (existingUser != null)
				{
					_logger.LogWarning("Username or Email already exists.");
					return new ServiceResult { Success = false, Message = "Username or Email already exists." };
				}

				// Check if passwords match
				if (userDetails.Password != userDetails.ReEnteredPassword)
				{
					_logger.LogWarning("Passwords do not match for user creation.");
					return new ServiceResult { Success = false, Message = "Passwords do not match." };
				}

				// Hash the password
				string hashedPassword =  _encryption.EncryptData(userDetails.Password);

				// Create user entity
				var user = new User
				{
					Username = _encryption.EncryptData(userDetails.Username),
					FirstName = userDetails.FirstName,
					LastName = userDetails.LastName,
					PasswordHash = hashedPassword,
					UserRoleId = userDetails.UserRoleId,
					SalesPersonCode = userDetails.SalesPersonCode,
					Email = _encryption.EncryptData(userDetails.Email),
					PhoneNumber = _encryption.EncryptData(userDetails.PhoneNumber),
					IsActive = userDetails.IsActive,
					CreatedAt = DateTime.UtcNow
				};
				await _userData.CreateUserAsync(user);
				var locations = new List<UserLocation>();
				foreach (var locationCode in userDetails.LocationCodes)
				{
					locations.Add(new UserLocation
					{
						UserId = user.UserID,
						LocationCode = locationCode
					});

				}

				await _userData.AddUserLocationsAsync(locations);
				var mfaUser = new MFASetting
				{
					UserID = user.UserID,
					IsMFAEnabled = userDetails.IsMfaEnabled, 
					PreferredMFAType = userDetails.MfaType
				};
						// Save to database
			
				await _userData.AddMfaSettingAsync(mfaUser);

				_logger.LogInformation("User created successfully.");
				return new ServiceResult { Success = true, Message = "User created successfully.",UserId = user.UserID };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while creating user with Username: {Username}", userDetails.Username);
				return new ServiceResult { Success = false, Message = "An unexpected error occurred. Please try again." };
			}
		}
		public async Task<ServiceResult> UpdateUserAsync(int userId, UserDetails userDetails)
		{
			try
			{
				_logger.LogInformation("Attempting to update user with UserId: {UserId}", userId);

				// Get existing user first
				var exUser = await _userData.GetUserByIdAsync(userId);
				var exMfa = await _userData.GetMfaSettingByIdAsync(userId);
				if (exUser == null)
				{
					_logger.LogWarning("User not found with UserId: {UserId}", userId);
					return new ServiceResult { Success = false, Message = "User not found." };
				}

				// Only check for duplicates if username or email has actually changed
				bool usernameChanged = !string.Equals(exUser.Username, _encryption.EncryptData(userDetails.Username), StringComparison.OrdinalIgnoreCase);
				bool emailChanged = !string.Equals(exUser.Email, _encryption.EncryptData(userDetails.Email), StringComparison.OrdinalIgnoreCase);

				if (usernameChanged || emailChanged)
				{
					var existingUser = await _userData.CheckUserByUsernameOrEmailExceptIdAsync(userId, userDetails.Username, userDetails.Email);
					if (existingUser != null)
					{
						_logger.LogWarning("Username or Email already exists for UserId: {UserId}", userId);
						return new ServiceResult { Success = false, Message = "Username or Email already exists." };
					}
				}

				// Only validate and hash password if it's provided (not null/empty)
				if (!string.IsNullOrWhiteSpace(userDetails.Password))
				{
					// Check if passwords match
					if (userDetails.Password != userDetails.ReEnteredPassword)
					{
						_logger.LogWarning("Passwords do not match for user update with UserId: {UserId}", userId);
						return new ServiceResult { Success = false, Message = "Passwords do not match." };
					}

					// Hash the new password
					string hashedPassword = _encryption.EncryptData(userDetails.Password);
					exUser.PasswordHash = hashedPassword;
				}
				// If password is null/empty, keep the existing password unchanged
				var userLocations = await _userData.GetUserLocationsByIdAsync(userId);
				// Update user properties
				exUser.Username = _encryption.EncryptData(userDetails.Username);
				exUser.FirstName = userDetails.FirstName;
				exUser.LastName = userDetails.LastName;
				exUser.UserRoleId = userDetails.UserRoleId;
				exUser.SalesPersonCode = userDetails.SalesPersonCode;
				exUser.Email = _encryption.EncryptData(userDetails.Email);
				exUser.PhoneNumber = _encryption.EncryptData(userDetails.PhoneNumber);
				exUser.IsActive = userDetails.IsActive; // Added missing property

				// Add audit fields if they exist in your User model
				// exUser.ModifiedDate = DateTime.UtcNow;
				// exUser.ModifiedBy = currentUserId; // if you track who modified

				exMfa.IsMFAEnabled = userDetails.IsMfaEnabled;
				exMfa.PreferredMFAType = userDetails.MfaType;
				// Save to database
				await _userData.UpdateUserAsync(exUser);
				await _userData.UpdateMfaTypeAsync(exMfa);
				await _userData.RemoveUserLocationsByIdAsync(userLocations);
				await _userData.AddUserLocationsAsync(userDetails.LocationCodes.Select(loc => new UserLocation
				{
					UserId = userId,
					LocationCode = loc
				}));
				_logger.LogInformation("User updated successfully with UserId: {UserId}", userId);
				return new ServiceResult { Success = true, Message = "User updated successfully.",UserId = exUser.UserID };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while updating user with UserId: {UserId}, Username: {Username}",
					userId, userDetails.Username);
				return new ServiceResult { Success = false, Message = "An unexpected error occurred. Please try again." };
			}
		}

		public async Task<UserCreationDetails> GetUserCreationDetailsAsync()
		{

			try
			{
				_logger.LogInformation("Retrieving user creation details");
				var salesPersons = await _externalApiService.GetSalesPeopleAsync();
				if (salesPersons?.value == null || !salesPersons.value.Any())
				{
					_logger.LogWarning("No sales person data found ");
					throw new InvalidOperationException("No sales person data found.");
				}

				var locations = await _externalApiService.GetLocationsAsync();
				if (locations?.value == null || !locations.value.Any())
				{
					_logger.LogWarning("No location data found ");
					throw new InvalidOperationException("No location data found.");
				}

				var roles = await _userData.GetRoleDetailsAsync();
				if (roles == null || !roles.Any())
				{
					_logger.LogWarning("No role data found ");
					throw new InvalidOperationException("No role data found.");
				}

				// Map data into response model
				var details = new UserCreationDetails
				{
					SalesPersons = salesPersons.value.Select(sp => new Poses
					{
						SalesPersonCode = sp.code,
						SalesPersonName = sp.name,
						Email = sp.eMail,
						Phone = sp.phoneNo
					}).ToList(),

					Locations = locations.value.Select(loc => new Location
					{
						LocationCode = loc.code,
						LocationName = loc.name
					}).ToList(),

					Roles = roles // Already in expected format
				};

				return details;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while retrieving user creation details");
				throw new InvalidOperationException("Error while retrieving user creation details");
			}
		}

		public async Task<UserDetails> GetUserDetailsByIdAsync(int userId)
		{

			try
			{
				_logger.LogInformation("Retrieving user details by userId");
				var user = await _userData.GetUserByIdAsync(userId);
				var mfa = await _userData.GetMFATypeAsync(userId);
				var userLocations = await _userData.GetUserLocationCodesAsync(userId);
				if (user == null)
				{
					_logger.LogWarning($"User with UserId:{userId} not found ");
					throw new InvalidOperationException("User Not found");
				}
				string decryptedPassword = _encryption.DecryptData(user.PasswordHash);

				var userDetails = new UserDetails
				{
					Username = _encryption.DecryptData(user.Username),
					Password = decryptedPassword,
					ReEnteredPassword = decryptedPassword,
					FirstName = user.FirstName,
					LastName = user.LastName,
					UserRoleId = user.UserRoleId,
					SalesPersonCode = user.SalesPersonCode,
					LocationCodes = userLocations,
					Email = _encryption.DecryptData(user.Email),
					PhoneNumber = _encryption.DecryptData(user.PhoneNumber),
					IsActive = user.IsActive,
					IsMfaEnabled = mfa.IsMFAEnabled,
					MfaType = mfa.PreferredMFAType
				};

				_logger.LogInformation("Successfully retrieved user details for UserId: {UserId}", userId);
				return userDetails;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while retrieving user details for UserId: {UserId}", userId);
				throw new InvalidOperationException($"An error occurred while retrieving user details for ID {userId}", ex);
			
			}
		}
		public async Task<List<UserReturn>> GetListOfUsersAsync()
		{

			try
			{
				_logger.LogInformation("Retrieving List of users ");
				var users = await _userData.GetAllUsersAsync();

				var userDetailsList = new List<UserReturn>();
				foreach (var user in users)
				{
					var decryptedPassword = _encryption.DecryptData(user.PasswordHash);
					userDetailsList.Add(new UserReturn
					{
						Username = _encryption.DecryptData(user.Username),
						FirstName = user.FirstName,
						LastName = user.LastName,
						UserRoleId = user.UserRoleId,
						SalesPersonCode = user.SalesPersonCode,
						IsActive = user.IsActive,
					});
				}

				_logger.LogInformation("Successfully retrieved user details list");
				return userDetailsList;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while retrieving user details");
				throw new InvalidOperationException($"An error occurred while retrieving user details", ex);

			}
		}
		/// <summary>
		/// Checks if Multi-Factor Authentication (MFA) is enabled for the user and retrieves the preferred MFA type.
		/// </summary>
		/// <param name="user">The user to check for MFA settings.</param>
		/// <returns>Returns a tuple indicating whether MFA is enabled and the preferred MFA type.</returns>
		private async Task<(bool IsEnabled, string PreferredMFAType)> IsMfaEnabledAndGetType(User user)
		{
			// Retrieve MFA settings for the user
			var mfa = await _userData.GetMFATypeAsync(user.UserID);

			// Check if MFA is not enabled or settings not found
			if (mfa == null || !mfa.IsMFAEnabled)
			{
				return (false, string.Empty); // Return false for MFA enabled status
			}

			return (true, mfa.PreferredMFAType); // Return true and the preferred MFA type
		}
	}

}
