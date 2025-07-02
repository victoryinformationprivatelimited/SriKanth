using SriKanth.Model.ExistingApis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.SalesModule
{
	public interface IExternalApiService
	{
		Task<string> GetAccessTokenAsync();
		Task<T> GetDataFromApiAsync<T>(string apiUrl);
		Task<T> PostDataToApiAsync<T>(string apiUrl, object data);
		Task<CustomerApiResponse> GetCustomersAsync();
		Task<CustomerApiResponse> GetCustomersFilterAsync(string? filterField = null, string? filterValue = null);
		Task<SalesPeopleApiResponse> GetSalesPeopleAsync();
		Task<SalesPeopleApiResponse> GetSalesPeopleFilterAsync(string? filterField = null, string? filterValue = null);
		Task<LocationApiResponse> GetLocationsAsync();
		Task<LocationApiResponse> GetLocationsFilterAsync(string? filterField = null, string? filterValue = null);
		Task<ItemApiResponse> GetItemsWithSubstitutionsAsync();
		Task<ItemApiResponse> GetItemsWithSubstitutionsFilterAsync(string? filterField = null, string? filterValue = null);
		Task<string> GetItemsPictureAsync(Guid systemId);
		Task<InventoryApiResponse> GetInventoryBalanceAsync();
		Task<InventoryApiResponse> GetInventoryBalanceFilterAsync(string? filterField = null, string? filterValue = null);
		Task<SalesPriceApiResponse> GetSalesPriceAsync();
		Task<SalesPriceApiResponse> GetSalesPriceFilterAsync(string? filterField = null, string? filterValue = null);
		Task<SalesIntegrationResponse> PostSalesOrderAsync(SalesOrderRequest salesOrder);
		Task<InvoiceApiResponse> GetInvoiceDetailsAsync();
		Task<InvoiceApiResponse> GetInvoiceDetailsFilterAsync(string? filterField = null, string? filterValue = null);
		Task<PostedInvoiceApiResponse> GetPostedInvoiceDetailsAsync();
		Task<PostedInvoiceApiResponse> GetPostedInvoiceDetailsFilterAsync(string? filterField = null, string? filterValue = null);
	}
}
