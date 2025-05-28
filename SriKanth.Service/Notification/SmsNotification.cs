using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SriKanth.Interface.Notification;
using SriKanth.Model.Login_Module.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service.Notification
{
	public class SmsNotification : ISmsNotification
	{
		private readonly ILogger<SmsNotification> _logger;
		private readonly IConfiguration _configuration;
		private readonly HttpClient _httpClient;// Configuration to access settings like Twilio credentials
		private const string LoginUrl = "https://esms.dialog.lk/api/v2/user/login";
		private const string SmsUrl = "https://e-sms.dialog.lk/api/v2/sms";

		/// <summary>
		/// Initializes a new instance of the <see cref="SmsNotification"/> class.
		/// </summary>
		/// <param name="loggerFactory">Factory for creating loggers.</param>
		/// <param name="configuration">Configuration service for accessing settings.</param>
		/// <param name="httpClient">HttpClient for making HTTP requests.</param>
		public SmsNotification(ILogger<SmsNotification> logger, IConfiguration configuration, HttpClient httpClient)
		{

			_logger = logger;
			_configuration = configuration;
			_httpClient = httpClient;
		}

		/// <summary>
		/// Retrieves an access token required to authenticate with the SMS service.
		/// </summary>
		/// <returns>The access token as a string.</returns>
		/// <exception cref="Exception">Thrown when authentication fails.</exception>
		private async Task<string> GetAccessTokenAsync()
		{
			string Username = _configuration["Values:Uname"];
			string Password = _configuration["Values:Password"];
			var loginRequest = new
			{
				username = Username,
				password = Password
			};

			var response = await _httpClient.PostAsJsonAsync(LoginUrl, loginRequest);

			if (response.IsSuccessStatusCode)
			{
				var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
				return result?.Token; // Replace "Token" with the actual property name from the response.
			}

			throw new Exception("Failed to authenticate with SMS service.");
		}

		/// <summary>
		/// Sends an SMS notification to the specified phone numbers in the notification request.
		/// </summary>
		/// <param name="notificationRequest">The notification request containing phone numbers and the message.</param>
		/// <returns>A task representing the asynchronous operation, with a <see cref="NotificationResult"/> indicating success or failure.</returns>
		public async Task<NotificationResult> SendSms(NotificationRequest notificationRequest)
		{
			_logger.LogInformation("Processing a Sms notification request.");
			var validator = new ValidationService();
			string Error;
			var result = new NotificationResult { IsSuccess = true, ErrorMessages = new List<string>() };

			// Iterate over each phone number in the request to send SMS
			try
			{
				//  Get the access token
				var token = await GetAccessTokenAsync();
				_httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

				// Prepare the SMS request body with validated and formatted phone numbers
				var formattedNumbers = notificationRequest.ToPnums
			   .Where(num => validator.IsValidPhoneNumber(num, out _)) // Ensure valid format
			   .Select(num => new { mobile = num.StartsWith("+") ? num.Substring(1) : num }) // Remove '+' from the start
			   .ToArray();

				if (formattedNumbers.Length == 0)
				{
					// Log and exit if no valid phone numbers remain
					result.IsSuccess = false;
					result.ErrorMessages.Add("No valid phone numbers to send SMS.");
					_logger.LogWarning("No valid phone numbers found after validation.");
					return result;
				}
				// Create SMS request body
				var smsRequest = new
				{
					msisdn = formattedNumbers,
					message = notificationRequest.Message,
					transaction_id = new Random().Next(100000, 999999) // Generate a unique transaction ID
				};

				// Send SMS request
				var response = await _httpClient.PostAsJsonAsync(SmsUrl, smsRequest);

				if (response.IsSuccessStatusCode)
				{
					_logger.LogInformation("SMS successfully sent to: {0}", string.Join(", ", notificationRequest.ToPnums));
				}
				else
				{
					var errorResponse = await response.Content.ReadAsStringAsync();
					Console.WriteLine(errorResponse);
					result.IsSuccess = false;
					result.ErrorMessages.Add($"Failed to send SMS: {errorResponse}");
					_logger.LogError("Failed to send SMS to {0}: {1}", string.Join(", ", notificationRequest.ToPnums), errorResponse);
				}
			}
			catch (Exception ex)
			{
				// Log the error and return failure in case of an exception
				result.IsSuccess = false;
				string errorMessage = $"Error sending SMS ";
				result.ErrorMessages.Add(errorMessage);
				_logger.LogError(ex, $"{errorMessage}: {ex.Message}");

			}
			return result;

		}
		/// <summary>
		///  response from the SMS service login, containing the access token.
		/// </summary>
		public class LoginResponse
		{
			public string Token { get; set; } // Adjust property name according to response structure
		}
	}
}
