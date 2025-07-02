using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using SriKanth.Interface;
using SriKanth.Interface.SalesModule;
using SriKanth.Model.ExistingApis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SriKanth.Service.SalesModule
{
	/// <summary>
	/// Service class for handling all external API communications with Business Central
	/// </summary>
	public class ExternalApiService : IExternalApiService
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IConfiguration _configuration;
		private string _cachedToken;
		private DateTime _tokenExpiryTime;
		private readonly IEncryptionService _encryption;
		private readonly string _baseApiUrl;

		/// <summary>
		/// Initializes a new instance of the ExternalApiService class
		/// </summary>
		/// <param name="httpClientFactory">Factory for creating HttpClient instances</param>
		/// <param name="configuration">Application configuration</param>
		public ExternalApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IEncryptionService encryption)
		{
			_httpClientFactory = httpClientFactory;
			_configuration = configuration;
			_encryption = encryption;
			_baseApiUrl = BuildBaseApiUrl();
		}

		private string BuildBaseApiUrl()
		{
			var environmentName = _configuration["BusinessCentral:EnvironmentName"];
			var companyId = _configuration["BusinessCentral:CompanyId"];

			if (string.IsNullOrEmpty(environmentName) || string.IsNullOrEmpty(companyId))
			{
				throw new InvalidOperationException("BusinessCentral:EnvironmentName and BusinessCentral:CompanyId must be configured");
			}
			return $"https://api.businesscentral.dynamics.com/v2.0/{environmentName}/api/asttrum/sales/v1.0/companies({companyId})";
		}
		/// <summary>
		/// Retrieves an access token for Business Central API authentication
		/// </summary>
		/// <returns>Access token string</returns>
		/// <remarks>
		/// Implements token caching to avoid unnecessary requests to the token endpoint.
		/// Tokens are cached until they are about to expire (with a 1-minute buffer).
		/// </remarks>
		public async Task<string> GetAccessTokenAsync()
		{
			
			// Return cached token if it's still valid
			if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryTime)
			{
				return _cachedToken;
			}

			var client = _httpClientFactory.CreateClient();
			var clientId = _encryption.DecryptData(_configuration["OAuth:ClientId"]);
			var clientSecret = _encryption.DecryptData(_configuration["OAuth:ClientSecret"]);
			// Prepare OAuth2 token request parameters
			var parameters = new Dictionary<string, string>
			{
				{ "client_id", clientId},
				{ "client_secret", clientSecret},
				{ "grant_type", "client_credentials" },
				{ "scope", _configuration["OAuth:Scope"] ?? "https://api.businesscentral.dynamics.com/.default" }
			};

			// Get token endpoint from config or use default
			var tokenEndpoint = _configuration["OAuth:TokenEndpoint"]
				?? "https://login.microsoftonline.com/6dfae1d4-52b9-4fc7-9b7c-1014447db47b/oauth2/v2.0/token";

			// Request token from Azure AD
			var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(parameters));

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"Failed to obtain access token. Status: {response.StatusCode}");
			}

			// Parse token response
			var content = await response.Content.ReadAsStringAsync();
			var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);

			// Cache the token with expiration buffer (1 minute before actual expiry)
			_cachedToken = tokenResponse.access_token;
			_tokenExpiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60);

			return _cachedToken;
		}

		/// <summary>
		/// Generic method for making GET requests to the Business Central API
		/// </summary>
		/// <typeparam name="T">Type to deserialize the response into</typeparam>
		/// <param name="apiUrl">Full API endpoint URL</param>
		/// <returns>Deserialized response of type T</returns>
		/// <remarks>
		/// Handles both JSON responses and binary responses (for image requests)
		/// </remarks>
		public async Task<T> GetDataFromApiAsync<T>(string apiUrl)
		{
			// Get authentication token
			var token = await GetAccessTokenAsync();
			var client = _httpClientFactory.CreateClient();

			// Set authorization header
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

			// Make API request
			var response = await client.GetAsync(apiUrl);

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"API request failed. Status: {response.StatusCode}");
			}

			// Special handling for image requests (byte array responses)
			if (typeof(T) == typeof(byte[]))
			{
				return (T)(object)await response.Content.ReadAsByteArrayAsync();
			}

			// Standard JSON response handling
			var content = await response.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<T>(content);
		}

		/// <summary>
		/// Generic method for making POST requests to the Business Central API
		/// </summary>
		/// <typeparam name="T">Type to deserialize the response into</typeparam>
		/// <param name="apiUrl">Full API endpoint URL</param>
		/// <param name="data">Data object to serialize and send in the request body</param>
		/// <returns>Deserialized response of type T</returns>
		public async Task<T> PostDataToApiAsync<T>(string apiUrl, object data)
		{
			// Get authentication token
			var token = await GetAccessTokenAsync();
			using var client = _httpClientFactory.CreateClient();

			// Set authorization header
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

			// Configure JSON serialization options
			var options = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				PropertyNameCaseInsensitive = true
			};

			// Serialize request data
			var jsonContent = new StringContent(
				JsonSerializer.Serialize(data, options),
				Encoding.UTF8,
				"application/json");

			// Make API request
			var response = await client.PostAsync(apiUrl, jsonContent);

			// Read response content
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"API request failed. Status: {response.StatusCode}. Response: {responseContent}");
			}

			// Deserialize and return response
			return JsonSerializer.Deserialize<T>(responseContent, options);
		}


		/// <summary>
		/// Retrieves a list of customers from Business Central
		/// </summary>
		/// <returns>CustomerApiResponse containing customer data</returns>
		public async Task<CustomerApiResponse> GetCustomersAsync()
		{
			string apiUrl = $"{_baseApiUrl}/customers";
			return await GetDataFromApiAsync<CustomerApiResponse>(apiUrl);
		}
		public async Task<CustomerApiResponse> GetCustomersFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/customers";

			if (!string.IsNullOrEmpty(filterField) && !string.IsNullOrEmpty(filterValue))
			{
				// URL encode the filter value to handle spaces and special characters
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"?$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<CustomerApiResponse>(apiUrl);
		}

		/// <summary>
		/// Retrieves a list of sales people from Business Central
		/// </summary>
		/// <returns>SalesPeopleApiResponse containing sales person data</returns>
		public async Task<SalesPeopleApiResponse> GetSalesPeopleAsync()
		{
			string apiUrl = $"{_baseApiUrl}/salesPeople";
			return await GetDataFromApiAsync<SalesPeopleApiResponse>(apiUrl);
		}
		public async Task<SalesPeopleApiResponse> GetSalesPeopleFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/salesPeople";

			if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
			{
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"?$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<SalesPeopleApiResponse>(apiUrl);
		}


		/// <summary>
		/// Retrieves a list of locations from Business Central
		/// </summary>
		/// <returns>LocationApiResponse containing location data</returns>
		public async Task<LocationApiResponse> GetLocationsAsync()
		{
			string apiUrl = $"{_baseApiUrl}/locations";
			return await GetDataFromApiAsync<LocationApiResponse>(apiUrl);
		}

		public async Task<LocationApiResponse> GetLocationsFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/locations";

			if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
			{
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"?$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<LocationApiResponse>(apiUrl);
		}

		/// <summary>
		/// Retrieves items with their substitution items from Business Central
		/// </summary>
		/// <returns>ItemApiResponse containing item data with substitutions</returns>
		public async Task<ItemApiResponse> GetItemsWithSubstitutionsAsync()
		{
			string apiUrl = $"{_baseApiUrl}/items?$expand=itemsubstitutions";
			return await GetDataFromApiAsync<ItemApiResponse>(apiUrl);
		}

		public async Task<ItemApiResponse> GetItemsWithSubstitutionsFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/items?$expand=itemsubstitutions";

			if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
			{
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"&$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<ItemApiResponse>(apiUrl);
		}

		/// <summary>
		/// Retrieves an item's picture from Business Central
		/// </summary>
		/// <param name="systemId">GUID of the item</param>
		/// <returns>Base64 encoded string of the item picture</returns>
		public async Task<string?> GetItemsPictureAsync(Guid systemId)
		{
			try
			{
				string apiUrl = $"{_baseApiUrl}/items({systemId})/picture/pictureContent";
				var imageBytes = await GetDataFromApiAsync<byte[]>(apiUrl);
				return Convert.ToBase64String(imageBytes);
			}
			catch (Exception ex)
			{
				// Optionally log the error here if you have a logger
				// _logger.LogWarning(ex, "Failed to fetch picture for item {SystemId}", systemId);
				return null;
			}
		}

		/// <summary>
		/// Retrieves inventory balance information from Business Central
		/// </summary>
		/// <returns>InventoryApiResponse containing inventory data</returns>
		public async Task<InventoryApiResponse> GetInventoryBalanceAsync()
		{
			string apiUrl = $"{_baseApiUrl}/inventoryBalances";
			return await GetDataFromApiAsync<InventoryApiResponse>(apiUrl);
		}
		public async Task<InventoryApiResponse> GetInventoryBalanceFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/inventoryBalances";

			if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
			{
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"?$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<InventoryApiResponse>(apiUrl);
		}

		/// <summary>
		/// Retrieves sales price information from Business Central
		/// </summary>
		/// <returns>SalesPriceApiResponse containing price data</returns>
		public async Task<SalesPriceApiResponse> GetSalesPriceAsync()
		{
			string apiUrl = $"{_baseApiUrl}/salesPrices";
			return await GetDataFromApiAsync<SalesPriceApiResponse>(apiUrl);
		}

		public async Task<SalesPriceApiResponse> GetSalesPriceFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/salesPrices";

			if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
			{
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"?$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<SalesPriceApiResponse>(apiUrl);
		}

		/// <summary>
		/// Posts a sales order to Business Central
		/// </summary>
		/// <param name="salesOrder">Sales order data to post</param>
		/// <returns>SalesIntegrationResponse containing the API response</returns>
		public async Task<SalesIntegrationResponse> PostSalesOrderAsync(SalesOrderRequest salesOrder)
		{
			string apiUrl = $"{_baseApiUrl}/salesIntegrations?$expand=salesIntegrationLines";

			return await PostDataToApiAsync<SalesIntegrationResponse>(apiUrl, salesOrder);
		}

		/// <summary>
		/// Retrieves invoice details from Business Central
		/// </summary>
		/// <returns>InvoiceApiResponse containing invoice data</returns>
		public async Task<InvoiceApiResponse> GetInvoiceDetailsAsync()
		{
			string apiUrl = $"{_baseApiUrl}/postedInvoiceLines";
			return await GetDataFromApiAsync<InvoiceApiResponse>(apiUrl);
		}
		public async Task<InvoiceApiResponse> GetInvoiceDetailsFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/postedInvoiceLines";

			if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
			{
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"?$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<InvoiceApiResponse>(apiUrl);
		}

		/// <summary>
		/// Retrieves invoice details from Business Central
		/// </summary>
		/// <returns>InvoiceApiResponse containing invoice data</returns>
		public async Task<PostedInvoiceApiResponse> GetPostedInvoiceDetailsAsync()
		{
			string apiUrl = $"{_baseApiUrl}/postedInvoices?$expand=postedInvoiceLines";
			return await GetDataFromApiAsync<PostedInvoiceApiResponse>(apiUrl);
		}

		public async Task<PostedInvoiceApiResponse> GetPostedInvoiceDetailsFilterAsync(string? filterField = null, string? filterValue = null)
		{
			string apiUrl = $"{_baseApiUrl}/postedInvoices?$expand=postedInvoiceLines";

			if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
			{
				string encodedValue = Uri.EscapeDataString($"'{filterValue}'");
				apiUrl += $"&$filter={filterField} eq {encodedValue}";
			}

			return await GetDataFromApiAsync<PostedInvoiceApiResponse>(apiUrl);
		}

		/// <summary>
		/// Internal class for deserializing token responses from Azure AD
		/// </summary>
		public class TokenResponse
		{
			public string token_type { get; set; }
			public int expires_in { get; set; }
			public string access_token { get; set; }
		}
	}
}