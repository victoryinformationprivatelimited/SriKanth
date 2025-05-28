using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using SriKanth.Interface.Notification;
using SriKanth.Model.Login_Module.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service.Notification
{
	public class EmailNotification : IEmailNotification
	{
		private readonly ILogger<EmailNotification> _logger;
		private readonly IConfiguration _configuration;

		/// <summary>
		/// Initializes a new instance of the <see cref="EmailNotification"/> class.
		/// </summary>
		/// <param name="loggerFactory">The logger factory to create a logger.</param>
		/// <param name="configuration">The configuration to access email settings.</param>
		public EmailNotification(IConfiguration configuration, ILogger<EmailNotification> logger)
		{
			_logger = logger;
			_configuration = configuration;


		}

		/// <summary>
		/// Sends an email notification based on the provided <see cref="NotificationRequest"/>.
		/// </summary>
		/// <param name="notificationRequest">The notification request containing email details.</param>
		/// <returns>A task representing the asynchronous operation, with a <see cref="NotificationResult"/> indicating success or failure.</returns>
		public async Task<NotificationResult> SendEmail(NotificationRequest notificationRequest)
		{
			var validator = new ValidationService();
			var result = new NotificationResult { IsSuccess = true, ErrorMessages = new List<string>() };

			string subject = notificationRequest.Subject;
			string body = notificationRequest.Message;

			foreach (var toEmail in notificationRequest.Emails)
			{
				// Validate the email
				bool isEmailValid = validator.IsValidEmail(toEmail, out string error);
				if (!isEmailValid)
				{
					// If email is invalid, set failure in result and log the error
					result.IsSuccess = false;
					string errorMessage = $"Invalid Email: {toEmail}";
					result.ErrorMessages.Add(errorMessage);
					_logger.LogError($"{errorMessage}: {error}");
					continue; // Continue to next email instead of returning early
				}

				try
				{
					// Prepare the email
					var email = new MimeMessage();
					email.From.Add(MailboxAddress.Parse(_configuration["Values:FromEmail"]));
					email.To.Add(MailboxAddress.Parse(toEmail));
					email.Subject = subject;
					email.Body = new TextPart(TextFormat.Html) { Text = body };

					// Send the email using SMTP
					using var smtp = new MailKit.Net.Smtp.SmtpClient();
					await smtp.ConnectAsync(_configuration["Values:SmtpHost"],
						int.Parse(_configuration["Values:SmtpPort"]),
						MailKit.Security.SecureSocketOptions.StartTls);
					await smtp.AuthenticateAsync(_configuration["Values:SmtpUser"], _configuration["Values:SmtpPass"]);
					await smtp.SendAsync(email);
					await smtp.DisconnectAsync(true);

					_logger.LogInformation("Email sent successfully to {toEmail}", toEmail);
				}
				catch (Exception ex)
				{
					// Log the error and set failure in the result
					result.IsSuccess = false;
					string errorMessage = $"Error sending email to {toEmail}: {ex.Message}";
					result.ErrorMessages.Add(errorMessage);
					_logger.LogError(ex, $"{errorMessage}: {ex.Message}");// Return the result immediately with the error
				}
			}

			return result;
		}
	}
}
