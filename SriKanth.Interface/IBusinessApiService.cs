using SriKanth.Model.BusinessModule.DTOs;
using SriKanth.Model.Login_Module.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface
{
	public interface IBusinessApiService
	{
		Task<List<StockItem>> GetSalesStockDetails();
		Task<OrderCreationDetails> GetOrderCreationDetailsAsync();
		Task<OrderCreationDetails> GetFilteredOrderCreationDetailsAsync(int userId);
		Task<ServiceResult> SubmitOrderAsync(int userId, OrderRequest request);
	}
}
