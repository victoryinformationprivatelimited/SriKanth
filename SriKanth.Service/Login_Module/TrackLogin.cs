using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SriKanth.Interface.Data;
using SriKanth.Interface.Login_Module;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service.Login_Module
{
	public class TrackLogin
	{
		private readonly IConfiguration _configuration;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILogger<TrackLogin> _logger;
		private readonly ILoginData _userData;

		/// <summary>
		/// Constructor to initialize the TrackLogin service with the required dependencies.
		/// </summary>
		/// <param name="dbContextFactory">Factory to get the database context.</param>
		/// <param name="configuration">Configuration to access app settings.</param>
		/// <param name="httpContextAccessor">Accessor to retrieve HTTP context (e.g., for IP address and User-Agent).</param>
		/// <param name="logger">Logger to log any errors or information.</param>
		public TrackLogin(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<TrackLogin> logger, ILoginData userData)
		{

			_configuration = configuration;
			_httpContextAccessor = httpContextAccessor;
			_logger = logger;
			_userData = userData;

		}

		/// <summary>
		/// Tracks a login attempt for the specified user, recording details like IP, device, location, success, MFA usage, etc.
		/// </summary>
		/// <param name="user">The user attempting to log in.</param>
		/// <param name="isSuccessful">Whether the login was successful or not.</param>
		/// <param name="loginMethod">The method used for login (e.g., password, SSO).</param>
		/// <param name="mfaUsed">Indicates whether MFA was used during login.</param>
		/// <param name="mfaMethod">Specifies the MFA method (email, SMS, etc.).</param>
		/// <param name="failureReason">The reason for login failure, if applicable.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		public async Task TrackLoginAsync(User user, bool isSuccessful, string loginMethod, bool? mfaUsed = null, string mfaMethod = null, string failureReason = null)
		{
			try
			{
				// Extract IP Address
				var ipAddress = GetClientIpAddress();

				// Extract User-Agent and parse device details
				var userAgent = GetUserAgent();
				var deviceDetails = GetDeviceDetails(userAgent);
				var geoLocation = await GetGeoLocationAsync(ipAddress);
				var sessionId = GetSessionId();
				// Create a LoginTracking record with gathered data
				var loginTracking = new LoginTrack
				{
					UserID = user.UserID,
					LoginMethod = loginMethod,
					LoginTime = DateTime.Now,
					IPAddress = ipAddress ?? "Unknown IP",
					DeviceType = deviceDetails?.DeviceType ?? "Unknown Device",
					OperatingSystem = deviceDetails?.OperatingSystem ?? "Unknown OS",
					Browser = deviceDetails?.Browser ?? "Unknown Browser",
					Country = geoLocation?.Country ?? "Unknown Country",
					City = geoLocation?.City ?? "Unknown City",
					IsSuccessful = isSuccessful,
					MFAUsed = mfaUsed,
					MFAMethod = mfaMethod,
					SessionID = sessionId ?? "Unknown Session",
					FailureReason = failureReason
				};
				// Add the login tracking record to the database
				await _userData.AddLoginTrackAsync(loginTracking);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error tracking login for user {user.UserID}: {ex.Message}");
				// Optionally, rethrow or handle error
			}
		}

		/// <summary>
		/// Retrieves the client's IP address from the HTTP context.
		/// </summary>
		/// <returns>Client's IP address as a string.</returns>
		private string GetClientIpAddress()
		{
			try
			{
				var httpContext = _httpContextAccessor.HttpContext;
				return httpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault()
					   ?? httpContext?.Connection.RemoteIpAddress?.ToString();
			}
			catch (Exception ex)
			{
				return "Unknown IP";
			}
		}

		/// <summary>
		/// Retrieves the User-Agent string from the HTTP context.
		/// </summary>
		/// <returns>User-Agent string representing the client's device/browser details.</returns>
		private string GetUserAgent()
		{
			try
			{
				var httpContext = _httpContextAccessor.HttpContext;
				return httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown User-Agent";
			}
			catch (Exception ex)
			{
				return "Unknown User-Agent";
			}
		}


		/// <summary>
		/// Retrieves the session ID from the HTTP context.
		/// </summary>
		/// <returns>The session ID as a string.</returns>
		private string GetSessionId()
		{
			try
			{
				var httpContext = _httpContextAccessor.HttpContext;
				return httpContext?.Session?.Id ?? "Unknown Session";
			}
			catch (Exception ex)
			{
				return "Unknown Session";
			}
		}

		/// <summary>
		/// Parses the User-Agent string to extract device details like device type, OS, and browser.
		/// </summary>
		/// <param name="userAgent">The User-Agent string to parse.</param>
		/// <returns>An object containing parsed device details.</returns>
		private DeviceDetails GetDeviceDetails(string userAgent)
		{
			try
			{
				if (string.IsNullOrEmpty(userAgent)) return null;

				// Use a parser to extract device, OS, and browser information
				var uaParser = UAParser.Parser.GetDefault();
				var clientInfo = uaParser.Parse(userAgent);

				return new DeviceDetails
				{
					DeviceType = clientInfo.Device.Family ?? "Unknown Device",
					OperatingSystem = clientInfo.OS.Family ?? "Unknown OS",
					Browser = clientInfo.UA.Family ?? "Unknown Browser"
				};
			}
			catch (Exception ex)
			{
				return new DeviceDetails
				{
					DeviceType = "Unknown Device",
					OperatingSystem = "Unknown OS",
					Browser = "Unknown Browser"
				};
			}
		}

		/// <summary>
		/// Retrieves geolocation information (country and city) for the provided IP address using an external API.
		/// </summary>
		/// <param name="ipAddress">The IP address to get geolocation data for.</param>
		/// <returns>A GeoLocation object with country and city details.</returns>
		private async Task<GeoLocation> GetGeoLocationAsync(string ipAddress)
		{
			try
			{
				if (string.IsNullOrEmpty(ipAddress)) return null;

				// Get the API key from configuration
				var apiKey = _configuration["IPSecretKey"]; // Replace with your IP Geolocation API Key
				var url = $"https://ipinfo.io/{ipAddress}?token={apiKey}";
				// Fetch geolocation data from the external API
				using (var httpClient = new HttpClient())
				{
					var response = await httpClient.GetStringAsync(url);
					_logger.LogWarning("API Response: " + response);
					var json = JObject.Parse(response);
					return new GeoLocation
					{
						Country = json["country"]?.ToString(),
						City = json["city"]?.ToString()

					};
				}
			}
			catch (Exception ex)
			{
				return new GeoLocation
				{
					Country = "Unknown Country",
					City = "Unknown City"
				};
			}
		}
	}

	/// <summary>
	/// Represents details about the device used during login.
	/// </summary>
	public class DeviceDetails
	{
		public string DeviceType { get; set; }
		public string OperatingSystem { get; set; }
		public string Browser { get; set; }
	}

	/// <summary>
	/// Represents geolocation information including country and city.
	/// </summary>
	public class GeoLocation
	{
		public string Country { get; set; }
		public string City { get; set; }
	}
}

