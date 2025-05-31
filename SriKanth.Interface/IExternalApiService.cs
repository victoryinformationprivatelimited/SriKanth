using SriKanth.Model.ExistingApis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface
{
	public interface IExternalApiService
	{
		Task<string> GetAccessTokenAsync();

		Task<T> GetDataFromApiAsync<T>(string apiUrl);

		Task<T> PostDataToApiAsync<T>(string apiUrl, object data);

		Task<CustomerApiResponse> GetCustomersAsync();

		Task<SalesPeopleApiResponse> GetSalesPeopleAsync();

		Task<LocationApiResponse> GetLocationsAsync();

		Task<ItemApiResponse> GetItemsWithSubstitutionsAsync();
		Task<string> GetItemsPictureAsync(Guid systemId);
		Task<InventoryApiResponse> GetInventoryBalanceAsync();
		Task<SalesPriceApiResponse> GetSalesPriceAsync();
		Task<CustomerApiResponse> GetCustomerDetailsAsync();
	}
}
