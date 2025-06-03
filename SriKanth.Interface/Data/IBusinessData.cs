using SriKanth.Model.BusinessModule.Entities;

namespace SriKanth.Data
{
	public interface IBusinessData
	{
		Task AddOrderAsync(Order order);
		Task AddOrderItemsAsync(IEnumerable<OrderItem> orderItems);
		Task<List<Order>> GetListOfOrdersAsync(string salesPersonCode, OrderStatus orderStatus);
		Task<List<OrderItem>> GetOrderItemsByOrderNumbersAsync(List<int> orderNumbers);
		Task<Order> GetOrderByIdAsync(int orderNumber);
		Task UpdateOrderStatusAsync(Order order);
		Task<List<OrderItem>> GetOrderItemsAsync(int orderNumber);

	}
}