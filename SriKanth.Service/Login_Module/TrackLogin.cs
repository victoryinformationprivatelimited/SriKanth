using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SriKanth.Interface.Data;
using SriKanth.Interface.Login_Module;
using SriKanth.Model.Login_Module.Entities;
using System.Net;

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
			var httpContext = _httpContextAccessor.HttpContext;
			if (httpContext == null) return "Unknown IP";

			// 1) Prefer X-Forwarded-For if present (may contain a comma-separated list)
			var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
			if (!string.IsNullOrEmpty(forwardedFor))
			{
				// pick first public IP in the list (left-most is original client in standard proxy chains)
				var ipList = forwardedFor.Split(',')
					.Select(ip => ip.Trim())
					.Where(ip => IPAddress.TryParse(ip, out _))
					.ToList();

				foreach (var ip in ipList)
				{
					if (!IsLocalOrPrivateIP(ip))
						return ip;
				}

				// if no public IP found, return first valid entry
				if (ipList.Any())
					return ipList.First();
			}

			// 2) Fallback to X-Real-IP header
			var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
			if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
				return realIp;

			// 3) Last fallback - use the connection remote IP (after UseForwardedHeaders, this may be the original client)
			var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
			if (!string.IsNullOrEmpty(remoteIp))
				return remoteIp;

			return "Unknown IP";
		}

		private string GetUserAgent()
		{
			var httpContext = _httpContextAccessor.HttpContext;
			if (httpContext == null) return "Unknown User-Agent";
			var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
			return string.IsNullOrEmpty(userAgent) ? "Unknown User-Agent" : userAgent;
		}

		private string GetSessionId()
		{
			try
			{
				var httpContext = _httpContextAccessor.HttpContext;
				if (httpContext == null)
				{
					return "Unknown Session";
				}

				// Check if session is configured and available

				if (httpContext.Session != null)
				{
					var sessionId = httpContext.Session.Id; ;

					if (!string.IsNullOrEmpty(sessionId))
						return sessionId;
				}


				// Try to get from authentication cookies
				var cookies = httpContext.Request.Cookies;
				// Check for common session/auth cookies
				var sessionCookies = new[]
				{
					".AspNetCore.Session",
					".AspNetCore.Identity.Application",
					".AspNetCore.Antiforgery",
					"ASP.NET_SessionId",
					"JSESSIONID",
					"PHPSESSID"
				};

				foreach (var cookieName in sessionCookies)
				{
					var cookieValue = cookies[cookieName];
					if (!string.IsNullOrEmpty(cookieValue))
					{
						return cookieValue;
					}
				}

				// Try to get from custom headers (if your frontend sends any)
				var customSessionHeaders = new[]
				{
					"X-Session-ID",
					"X-Request-ID",
					"X-Correlation-ID",
					"Authorization"
				};

				foreach (var headerName in customSessionHeaders)
				{
					var headerValue = httpContext.Request.Headers[headerName].FirstOrDefault();
					if (!string.IsNullOrEmpty(headerValue))
					{
						// For Authorization header, just take first 20 chars for tracking
						return headerName == "Authorization" ?
							headerValue.Substring(0, Math.Min(20, headerValue.Length)) :
							headerValue;
					}
				}

				// Try to create a session identifier from request characteristics
				var userAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault() ?? "";
				var acceptLanguage = httpContext.Request.Headers["Accept-Language"].FirstOrDefault() ?? "";
				var acceptEncoding = httpContext.Request.Headers["Accept-Encoding"].FirstOrDefault() ?? "";

				if (!string.IsNullOrEmpty(userAgent))
				{
					// Create a hash-based session ID from request characteristics
					var combinedString = $"{userAgent}|{acceptLanguage}|{acceptEncoding}|{DateTime.UtcNow:yyyyMMdd}";
					var hash = combinedString.GetHashCode();
					var pseudoSessionId = $"PSI_{Math.Abs(hash):X8}";
					return pseudoSessionId;
				}

				// Final fallback - generate tracking ID
				var trackingId = $"Track_{Guid.NewGuid():N}";
				return trackingId;
			}
			catch (Exception ex)
			{
				return "Unknown Session";
			}
		}

		private DeviceDetails GetDeviceDetails(string userAgent)
		{
			if (string.IsNullOrEmpty(userAgent))
			{
				return new DeviceDetails
				{
					DeviceType = "Unknown Device",
					OperatingSystem = "Unknown OS",
					Browser = "Unknown Browser"
				};
			}

			try
			{
				var uaParser = UAParser.Parser.GetDefault();
				var clientInfo = uaParser.Parse(userAgent);

				return new DeviceDetails
				{
					DeviceType = GetDeviceType(clientInfo, userAgent),
					OperatingSystem = GetOperatingSystem(clientInfo, userAgent),
					Browser = GetBrowser(clientInfo, userAgent)
				};
			}
			catch
			{
				return ParseUserAgentManually(userAgent);
			}
		}

		private string GetDeviceType(UAParser.ClientInfo clientInfo, string userAgent)
		{
			var deviceFamily = clientInfo?.Device.Family;
			if (!string.IsNullOrEmpty(deviceFamily) && deviceFamily != "Other") return deviceFamily;

			if (userAgent.Contains("Mobile") || userAgent.Contains("Android")) return "Mobile";
			if (userAgent.Contains("Tablet") || userAgent.Contains("iPad")) return "Tablet";
			return "Desktop";
		}

		private string GetOperatingSystem(UAParser.ClientInfo clientInfo, string userAgent)
		{
			var osFamily = clientInfo?.OS.Family;
			var osVersion = clientInfo?.OS.Major;

			if (!string.IsNullOrEmpty(osFamily) && osFamily != "Other")
				return !string.IsNullOrEmpty(osVersion) ? $"{osFamily} {osVersion}" : osFamily;

			if (userAgent.Contains("Windows NT 10.0")) return "Windows 10";
			if (userAgent.Contains("Windows NT 6.3")) return "Windows 8.1";
			if (userAgent.Contains("Windows NT 6.1")) return "Windows 7";
			if (userAgent.Contains("Mac OS X")) return "macOS";
			if (userAgent.Contains("Android")) return "Android";
			if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) return "iOS";
			if (userAgent.Contains("Linux")) return "Linux";

			return "Unknown OS";
		}

		private string GetBrowser(UAParser.ClientInfo clientInfo, string userAgent)
		{
			var browserFamily = clientInfo?.UA.Family;
			var browserVersion = clientInfo?.UA.Major;

			if (!string.IsNullOrEmpty(browserFamily) && browserFamily != "Other")
				return !string.IsNullOrEmpty(browserVersion) ? $"{browserFamily} {browserVersion}" : browserFamily;

			if (userAgent.Contains("Edg/")) return "Microsoft Edge";
			if (userAgent.Contains("Chrome/")) return "Google Chrome";
			if (userAgent.Contains("Firefox/")) return "Mozilla Firefox";
			if (userAgent.Contains("Safari/") && !userAgent.Contains("Chrome")) return "Safari";
			if (userAgent.Contains("Opera/")) return "Opera";

			return "Unknown Browser";
		}

		private DeviceDetails ParseUserAgentManually(string userAgent)
		{
			return new DeviceDetails
			{
				DeviceType = GetDeviceType(null, userAgent),
				OperatingSystem = GetOperatingSystem(null, userAgent),
				Browser = GetBrowser(null, userAgent)
			};
		}

		private async Task<GeoLocation> GetGeoLocationAsync(string ipAddress)
		{
			try
			{
				if (string.IsNullOrEmpty(ipAddress) || ipAddress == "Unknown IP")
					return new GeoLocation { Country = "Unknown Country", City = "Unknown City" };

				if (IsLocalOrPrivateIP(ipAddress))
					return new GeoLocation { Country = "Local Network", City = "Local Network" };

				var apiKey = _configuration["IPSecretKey"];
				if (string.IsNullOrEmpty(apiKey))
					return new GeoLocation { Country = "API Key Missing", City = "API Key Missing" };

				var url = $"https://ipinfo.io/{ipAddress}?token={apiKey}";
				using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
				{
					var response = await httpClient.GetStringAsync(url);
					var json = JObject.Parse(response);

					return new GeoLocation
					{
						Country = json["country"]?.ToString() ?? "Unknown Country",
						City = json["city"]?.ToString() ?? "Unknown City"
					};
				}
			}
			catch
			{
				return new GeoLocation { Country = "Error", City = "Error" };
			}
		}

		private bool IsLocalOrPrivateIP(string ipAddress)
		{
			if (string.IsNullOrEmpty(ipAddress)) return true;

			return ipAddress == "127.0.0.1" ||
				   ipAddress == "::1" ||
				   ipAddress.StartsWith("192.168.") ||
				   ipAddress.StartsWith("10.") ||
				   ipAddress.StartsWith("172.16.") ||
				   ipAddress.StartsWith("172.17.") ||
				   ipAddress.StartsWith("172.18.") ||
				   ipAddress.StartsWith("172.19.") ||
				   ipAddress.StartsWith("172.2") ||
				   ipAddress.StartsWith("172.30.") ||
				   ipAddress.StartsWith("172.31.");
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

