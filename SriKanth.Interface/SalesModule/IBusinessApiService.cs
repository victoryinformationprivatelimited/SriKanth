using SriKanth.Model.BusinessModule.DTOs;
using SriKanth.Model.BusinessModule.Entities;
using SriKanth.Model.Login_Module.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.SalesModule
{
	public interface IBusinessApiService
	{
		Task<List<StockItem>> GetSalesStockDetails();
		Task<string> GetImageByItemNo(string itemNo);
		Task<OrderCreationDetails> GetOrderCreationDetailsAsync();
		Task<OrderCreationDetails> GetFilteredOrderCreationDetailsAsync(int userId);
		Task<List<Location>> GetFilteredLocationsAsync(int userId);
		Task<List<OrderCustomer>> GetFilteredCustomersAsync(int userId);
		Task<List<OrderItemDetails>> GetFilteredItemsAsync(int userId);
		Task<CustomerInvoiceReturn> GetCustomerInvoicesAsync(int userId);
		Task<CustomerWiseInvoices> GetCustomerInvoiceDetailsAsync(string customerCode);
		Task<decimal> GetSingleCustomerDueAmountAsync(string customerNo);
	}
}
