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
	public interface IOrderDetailsApiService
	{
		Task<ServiceResult> SubmitOrderAsync(int userId, OrderRequest request);
		Task<List<OrderReturn>> GetOrdersListAsync(int userId, OrderStatus orderStatus);
		Task<ServiceResult> UpdateOrderStatusAsync(UpdateOrderRequest updateOrderRequest);
		Task<OrderStatusSummary> GetOrderStatusSummaryByUserAsync(int userId);
	}
}
