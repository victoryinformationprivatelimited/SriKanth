using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SriKanth.Service
{
		/// <summary>
		/// Provides methods for validating email addresses, phone numbers, and WhatsApp numbers.
		/// </summary>
		public class ValidationService
		{
			// Regex pattern for validating email addresses
			private static readonly Regex EmailRegex = new Regex(
				@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
				RegexOptions.Compiled | RegexOptions.IgnoreCase
			);
			// Regex pattern for validating phone numbers in E.164 format
			private static readonly Regex PhoneNumberRegex = new Regex(
				@"^\+?[1-9]\d{1,14}$", // E.164 format
				RegexOptions.Compiled
			);

			// Regex pattern for validating WhatsApp numbers in E.164 format (same as phone numbers)
			private static readonly Regex WhatsAppNumberRegex = new Regex(
				@"^\+?[1-9]\d{1,14}$", // E.164 format
				RegexOptions.Compiled
			);

			// Method to validate an email address
			/// <summary>
			/// Validates an email address.
			/// </summary>
			/// <param name="email">The email address to validate.</param>
			/// <param name="error">An output parameter that returns an error message if validation fails.</param>
			/// <returns>True if the email address is valid; otherwise, false.</returns>
			public bool IsValidEmail(string email, out string error)
			{
				// Check if the email is empty or null
				if (string.IsNullOrWhiteSpace(email))
				{
					error = "Email address cannot be empty.";
					return false;
				}

				// Validate the email format using the regex pattern
				if (!EmailRegex.IsMatch(email))
				{
					error = "Invalid email format.";
					return false;
				}

				error = string.Empty;
				return true;// Return true if valid
			}

			// Method to validate a phone number
			/// <summary>
			/// Validates a phone number.
			/// </summary>
			/// <param name="phoneNumber">The phone number to validate.</param>
			/// <param name="error">An output parameter that returns an error message if validation fails.</param>
			/// <returns>True if the phone number is valid; otherwise, false.</returns>
			public bool IsValidPhoneNumber(string phoneNumber, out string error)
			{
				// Check if the phone number is empty or null
				if (string.IsNullOrWhiteSpace(phoneNumber))
				{
					error = "Phone number cannot be empty.";
					return false;
				}

				// Validate the phone number format using the regex pattern
				if (!PhoneNumberRegex.IsMatch(phoneNumber))
				{
					error = "Invalid phone number format. Must be in E.164 format.";
					return false;
				}

				error = string.Empty;
				return true; // Return true if valid
			}

			// Method to validate a WhatsApp number (same validation as phone number)
			/// <summary>
			/// Validates a WhatsApp number.
			/// </summary>
			/// <param name="whatsappNumber">The WhatsApp number to validate.</param>
			/// <param name="error">An output parameter that returns an error message if validation fails.</param>
			/// <returns>True if the WhatsApp number is valid; otherwise, false.</returns>
			public bool IsValidWhatsAppNumber(string whatsappNumber, out string error)
			{
				// Check if the WhatsApp number is empty or null
				if (string.IsNullOrWhiteSpace(whatsappNumber))
				{
					error = "WhatsApp number cannot be empty.";
					return false;
				}

				// Validate the WhatsApp number format using the regex pattern
				if (!WhatsAppNumberRegex.IsMatch(whatsappNumber))
				{
					error = "Invalid WhatsApp number format. Must be in E.164 format.";
					return false;
				}

				error = string.Empty;
				return true; // Return true if valid
			}
		}
}
