using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SriKanth.Interface.Data;
using SriKanth.Interface.Notification;
using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SriKanth.Model.Login_Module.DTOs.NotificationRequest;

namespace SriKanth.Service
{
    public class NotificationService : INotificationService
	{
		private readonly IEmailNotification _emailNotification;
		private readonly ISmsNotification _smsNotifcation;
		private readonly HttpClient _httpClient;
		private readonly IConfiguration _configuration;
		private readonly ILoginData _userData;
		private readonly ILogger<NotificationService> _logger;  // Logger for recording log events.

		public NotificationService(HttpClient httpClient, IConfiguration configuration, ILoginData userData, ILogger<NotificationService> logger, ISmsNotification smsNotifcation, IEmailNotification emailNotification)
		{
			_httpClient = httpClient;
			_configuration = configuration;
			_userData = userData;
			_logger = logger;
			_emailNotification = emailNotification;
			_smsNotifcation = smsNotifcation;
		}

		public async Task<object> SendNotificationAsync(NotificationRequest notificationRequest)
		{
			
			try
			{
				foreach (var type in notificationRequest.NotificationTypes)
				{
					NotificationResult result = new NotificationResult { IsSuccess = true, ErrorMessages = new List<string>() };

					switch (type)
					{
						case NotificationType.SMS:
							_logger.LogInformation("Sending SMS...");
							result = await _smsNotifcation.SendSms(notificationRequest);
							break;

						case NotificationType.Email:
							_logger.LogInformation("Sending Email...");
							result = await _emailNotification.SendEmail(notificationRequest);
							break;

						default:
							_logger.LogError($"Unknown notification type: {type}");
							continue;
					}

					if (!result.IsSuccess)
					{
						return false;
					}
				}
				return true;
			}

			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error sending notification");
				return false;
			}
		}

		public async Task<NotificationResult> StoreNotificationAsync(NotificationRequest.NotificationType type, NotificationRequest request)
		{
			var result = new NotificationResult { IsSuccess = true, ErrorMessages = new List<string>() };
			// Determine the recipients list based on the notification type.
			var recipients = type switch
			{
				NotificationRequest.NotificationType.Email => request.Emails,
				NotificationRequest.NotificationType.SMS => request.ToPnums,
				NotificationRequest.NotificationType.WhatsApp => request.ToWnums,
				NotificationRequest.NotificationType.InApp => request.UserIds,
			};

			try
			{
				foreach (var recipient in recipients)
				{
					var log = new SentNotification
					{
						Recipient = recipient,
						NotificationType = type.ToString(),
						Subject = request.Subject,
						Message = request.Message,
						SentAt = DateTime.UtcNow, // Log the time of sending
						IsSuccess = true // Mark as success initially.
					};

					await _userData.AddNotificatonLogAsync(log); // Add the log entry to the database.
				}
				// Save changes to the database.
				_logger.LogInformation("Notifications saved successfully to the database.");
			}
			catch (Exception ex)
			{

				_logger.LogError(ex, "An error occurred while storing notifications to the database.");

				// Update the result to indicate failure and add error message.
				result.IsSuccess = false;
				result.ErrorMessages.Add("An error occurred while storing notifications to the database.");
				// Mark the notification log as failed for each recipient.
				foreach (var recipient in recipients)
				{
					var log = new SentNotification
					{
						Recipient = recipient,
						NotificationType = type.ToString(),
						Subject = request.Subject,
						Message = request.Message,
						SentAt = DateTime.UtcNow,// Log the time of attempt.
						IsSuccess = false, // Mark as failed due to the exception.
					};
					// Add the failed log entry to the database.
					await _userData.AddNotificatonLogAsync(log);
				}
			}
			return result;
		}
	}
}
