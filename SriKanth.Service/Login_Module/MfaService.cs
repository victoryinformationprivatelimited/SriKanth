using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SriKanth.Interface.Login_Module;
using SriKanth.Interface;
using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SriKanth.Interface.Notification;
using SriKanth.Interface.Data;
using SriKanth.Model;

namespace SriKanth.Service.Login_Module
{
    public class MfaService : IMfaService
	{
		private readonly IConfiguration _configuration;
		public readonly IEncryptionService _encryption; // Service for encrypting MFA codes
		private readonly ILogger<MfaService> _logger;
		private readonly IJwtTokenService _jwtTokenService; // Service to handle JWT token generation
		private readonly IMessageService _messageService; // Service for generating and sending notification messages
		private readonly INotificationService _notificationService; // Service for sending notifications
		private readonly ILoginData _userData;
		private readonly SriKanthDbContext _dbContext;
		
		/// <summary>
		/// Constructor to initialize the MFAService with necessary dependencies.
		/// </summary>
		public MfaService(IConfiguration configuration, IEncryptionService encryption, ILogger<MfaService> logger, INotificationService notificationService,  IMessageService messageService, IJwtTokenService jwtTokenService, ILoginData userData, SriKanthDbContext dbContext)
		{
			_configuration = configuration;
			_encryption = encryption;
			_logger = logger;
			_messageService = messageService;
			_notificationService = notificationService;
			_jwtTokenService = jwtTokenService;
			_userData = userData;
			_dbContext = dbContext;
		}

		/// <summary>
		/// Sends an MFA code to the user via SMS or Email.
		/// </summary>
		/// <param name="user">The user to whom the MFA code will be sent.</param>
		/// <param name="mfaType">The type of MFA (either 'Sms' or 'Email').</param>
		/// <returns>A boolean indicating whether the MFA code was successfully sent.</returns>
		public async Task<bool> SendMfaCodeAsync(User user, string mfaType)
		{
			try
			{
				// Generates a random 6-digit MFA code and sets its expiry time to 2 minutes
				var mfaCode = new Random().Next(100000, 999999).ToString();
				var expiry = DateTime.Now.AddMinutes(2);
				string hashedmfa = _encryption.EncryptData(mfaCode); // Encrypts the MFA code

				// Creates a new user token for storing the MFA code
				var userToken = new UserToken
				{
					UserID = user.UserID,
					Token = hashedmfa,
					TokenType = "MFA",
					CreatedAt = DateTime.Now,
					ExpiresAt = expiry,
					IsUsed = false,
					IsRevoked = false,
					Purpose = "MFA Verification"
				};

				await _userData.AddUserTokenAsync(userToken);

				// Generates the message to be sent with the MFA code
				var emailmessage = await _messageService.GenerateMfaMessage(user, mfaCode);
				var smsmessage = await _messageService.GenerateSMSMessage(user, mfaCode);

				var notificationRequest = new NotificationRequest
				{
					Subject = "MFA Verification Code"
				};
				// Sends the MFA code via SMS or Email based on user preference and verification
				if (mfaType == "Sms" && user.IsPhoneNumberVerified && !string.IsNullOrWhiteSpace(user.PhoneNumber))
				{
					// Decrypt the phone number
					string decryptedPhoneNumber = _encryption.DecryptData(user.PhoneNumber);

					// Ensure the phone number has the correct format with the country code
					//string formattedPhoneNumber = FormatPhoneNumber(decryptedPhoneNumber, "+94");

					notificationRequest.Message = smsmessage;
					notificationRequest.ToPnums.Add(decryptedPhoneNumber);
					notificationRequest.NotificationTypes.Add(NotificationRequest.NotificationType.SMS);

				}
				else if (mfaType == "Email" && user.IsEmailVerified && !string.IsNullOrWhiteSpace(user.Email))
				{
					notificationRequest.Message = emailmessage;
					notificationRequest.Emails.Add(_encryption.DecryptData(user.Email));
					notificationRequest.NotificationTypes.Add(NotificationRequest.NotificationType.Email);
				}
				else
				{
					// Log a warning if neither SMS nor Email could be used for MFA
					_logger.LogWarning("No valid contact method available for user {UserId} for MFA code.", user.UserID);
					return false;
				}
				await _notificationService.SendNotificationAsync(notificationRequest);
				var sendToken = new SendToken
				{
					UserID = user.UserID,
					UserTokenID = userToken.TokenID,
					MFADeviceID = 111, // Static value for MFA device ID
					SendAt = DateTime.Now,
					SendSuccessful = true // MFA was successfully sent
				};

				// Adds the sendToken record to the database and saves changes
				_userData.AddSendTokenAsync(sendToken);

				_logger.LogInformation("MFA code sent to user {UserId}.", user.UserID);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while sending MFA code for user {UserId}.", user.UserID);
				return false;
			}
		}

		/// <summary>
		/// Validates the MFA code entered by the user.
		/// </summary>
		/// <param name="userId">The ID of the user trying to validate MFA.</param>
		/// <param name="enteredMfaCode">The MFA code entered by the user.</param>
		/// <returns>A LoginResult object indicating whether the validation was successful.</returns>
		public async Task<LoginResult> ValidateMfaAsync(int userId, string enteredMfaCode)
		{
			try
			{
				_logger.LogInformation("MFA validation attempt started for user with ID: {UserId}.", userId);
				// Encrypts the entered MFA code for comparison
				string hashedenteredMfa = _encryption.EncryptData(enteredMfaCode);
				//MFA token that has not been used and is not expired
				var userToken = await _userData.GetUserTokenAsync(userId, hashedenteredMfa);
				var user = await _userData.GetUserByIdAsync(userId); // Finds the user by their ID
																	 // Marks the MFA token as used and updates the user's login details
				if (userToken != null)
				{
					userToken.IsUsed = true;
					userToken.LastUsedAt = DateTime.Now;

					user.FailedLoginCount = 0;
					user.LastLoginAt = DateTime.Now;

					await _dbContext.SaveChangesAsync();
					// Generates access and refresh tokens for the user
					var Accesstoken = await _jwtTokenService.GenerateJwtToken(user);
					var Refreshtoken = await _jwtTokenService.GenerateRefreshToken(user);

					_logger.LogInformation("MFA validation succeeded for user with ID: {UserId}.", userId);
					return new LoginResult { Success = true, AccessToken = Accesstoken, RefreshToken = Refreshtoken, Message = "MFA authentication was successful." };
				}
				// Handles failed login attempts by incrementing the failed login count
				user.FailedLoginCount++;
				if (user.FailedLoginCount >= 4)
				{
					// Locks the user's account after multiple failed attempts and sends a locked account email
					user.IsActive = false;
					await _dbContext.SaveChangesAsync();
					_logger.LogWarning("Account locked due to multiple failed MFA attempts for user with username: {Username}.", user.Username ?? "Unknown");
					await LockedEmailAsync(user);
					return new LoginResult { Success = false, UserLocked = true, Message = "Multiple Failed Attempts" };
				}

				await _dbContext.SaveChangesAsync();
				_logger.LogWarning("Invalid or expired MFA code entered by user with ID: {UserId}.", userId);
				return new LoginResult { Success = false, Message = "Invalid or expired MFA code." };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred during MFA validation for user with ID: {UserId}.", userId);
				return new LoginResult { Success = false, Message = "An error occurred during MFA validation. Please try again." };
			}
		}

		/// <summary>
		/// Sends an email notification to the user when their account is locked due to multiple failed login attempts.
		/// </summary>
		/// <param name="user">The user whose account has been locked.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		private async Task LockedEmailAsync(User user)
		{
			// Generate the locked account message
			var message = await _messageService.GenerateLockedMessage(user);

			// Create the notification request for sending the email
			var notificationRequest = new NotificationRequest
			{
				Subject = "Account Locked - Multiple Failed Login Attempts",
				Message = message,
			};
			notificationRequest.Emails.Add(_encryption.DecryptData(user.Email));
			notificationRequest.NotificationTypes.Add(NotificationRequest.NotificationType.Email);
			// Send the email notification
			await _notificationService.SendNotificationAsync(notificationRequest);
		}

		private string FormatPhoneNumber(string phoneNumber, string countryCode)
		{
			// Remove any spaces, dashes, or parentheses from the phone number
			phoneNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

			// Check if the phone number starts with the country code
			if (!phoneNumber.StartsWith(countryCode))
			{
				// If it starts with a leading zero, remove it
				if (phoneNumber.StartsWith("0"))
				{
					phoneNumber = phoneNumber.Substring(1);
				}

				// Prepend the country code
				phoneNumber = countryCode + phoneNumber;
			}

			return phoneNumber;
		}



	}
}
