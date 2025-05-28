using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using SriKanth.Interface;
using SriKanth.Model.ExistingApis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SriKanth.Service
{
	public class ExternalApiService : IExternalApiService
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IConfiguration _configuration;
		private string _cachedToken;
		private DateTime _tokenExpiryTime;

		public ExternalApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
		{
			_httpClientFactory = httpClientFactory;
			_configuration = configuration;
		}

		public async Task<string> GetAccessTokenAsync()
		{
			// Return cached token if it's still valid
			if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryTime)
			{
				return _cachedToken;
			}

			var client = _httpClientFactory.CreateClient();

			var parameters = new Dictionary<string, string>
		{
			{ "client_id", _configuration["OAuth:ClientId"] },
			{ "client_secret", _configuration["OAuth:ClientSecret"] },
			{ "grant_type", "client_credentials" },
			{ "scope", _configuration["OAuth:Scope"] ?? "https://api.businesscentral.dynamics.com/.default" }
		};

			var tokenEndpoint = _configuration["OAuth:TokenEndpoint"]
				?? "https://login.microsoftonline.com/6dfae1d4-52b9-4fc7-9b7c-1014447db47b/oauth2/v2.0/token";

			var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(parameters));

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"Failed to obtain access token. Status: {response.StatusCode}");
			}

			var content = await response.Content.ReadAsStringAsync();
			var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);

			// Cache the token with expiration buffer
			_cachedToken = tokenResponse.access_token;
			_tokenExpiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60); // 1-minute buffer

			return _cachedToken;
		}

		public async Task<T> GetDataFromApiAsync<T>(string apiUrl)
		{
			var token = await GetAccessTokenAsync();
			var client = _httpClientFactory.CreateClient();

			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

			var response = await client.GetAsync(apiUrl);

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"API request failed. Status: {response.StatusCode}");
			}
			// Special handling for image requests
			if (typeof(T) == typeof(byte[]))
			{
				return (T)(object)await response.Content.ReadAsByteArrayAsync();
			}
			var content = await response.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<T>(content);
		}

		public async Task<T> PostDataToApiAsync<T>(string apiUrl, object data)
		{
			var token = await GetAccessTokenAsync();
			var client = _httpClientFactory.CreateClient();

			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

			var jsonContent = new StringContent(
				JsonSerializer.Serialize(data),
				Encoding.UTF8,
				"application/json");

			var response = await client.PostAsync(apiUrl, jsonContent);

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"API request failed. Status: {response.StatusCode}");
			}

			var content = await response.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<T>(content);
		}
		// Customer API
		public async Task<CustomerApiResponse> GetCustomersAsync()
		{
			string apiUrl = "https://api.businesscentral.dynamics.com/v2.0/dev/api/asttrum/sales/v1.0/" +
					   "companies(b4dd4bba-0a23-f011-9af7-000d3a087c80)/customers";
			return await GetDataFromApiAsync<CustomerApiResponse>(apiUrl);
		}

		// SalesPeople API
		public async Task<SalesPeopleApiResponse> GetSalesPeopleAsync()
		{
			string apiUrl = "https://api.businesscentral.dynamics.com/v2.0/dev/api/asttrum/sales/v1.0/companies(b4dd4bba-0a23-f011-9af7-000d3a087c80)/salesPeople";
			return await GetDataFromApiAsync<SalesPeopleApiResponse>(apiUrl);
		}

		// Locations API
		public async Task<LocationApiResponse> GetLocationsAsync()
		{
			string apiUrl = "https://api.businesscentral.dynamics.com/v2.0/dev/api/asttrum/sales/v1.0/companies(b4dd4bba-0a23-f011-9af7-000d3a087c80)/locations";
			return await GetDataFromApiAsync<LocationApiResponse>(apiUrl);
		}

		// Items API with substitutions
		public async Task<ItemApiResponse> GetItemsWithSubstitutionsAsync()
		{
			string apiUrl = "https://api.businesscentral.dynamics.com/v2.0/dev/api/asttrum/sales/v1.0/companies(b4dd4bba-0a23-f011-9af7-000d3a087c80)/items?$expand=itemsubstitutions";
			return await GetDataFromApiAsync<ItemApiResponse>(apiUrl);
		}
		public async Task<string> GetItemsPictureAsync(Guid systemId)
		{
			string apiUrl = $"https://api.businesscentral.dynamics.com/v2.0/dev/api/v2.0/companies(b4dd4bba-0a23-f011-9af7-000d3a087c80)/items({systemId})/picture/pictureContent";
			var imageBytes = await GetDataFromApiAsync<byte[]>(apiUrl);
			return Convert.ToBase64String(imageBytes);
		}
		public async Task<InventoryApiResponse> GetInventoryBalanceAsync()
		{
			string apiUrl = "https://api.businesscentral.dynamics.com/v2.0/dev/api/asttrum/sales/v1.0/companies(b4dd4bba-0a23-f011-9af7-000d3a087c80)/inventoryBalances";
			return await GetDataFromApiAsync<InventoryApiResponse>(apiUrl);
		}
		public async Task<SalesPriceApiResponse> GetSalesPriceAsync()
		{
			string apiUrl = "https://api.businesscentral.dynamics.com/v2.0/dev/api/asttrum/sales/v1.0/companies(b4dd4bba-0a23-f011-9af7-000d3a087c80)/salesPrices";
			return await GetDataFromApiAsync<SalesPriceApiResponse>(apiUrl);
		}

		public class TokenResponse
		{
			public string token_type { get; set; }
			public int expires_in { get; set; }
			public string access_token { get; set; }
		}
	}
}
