using SriKanth.Model.BusinessModule.Entities;

namespace SriKanth.Data
{
	public interface IBusinessData
	{
		Task AddOrderAsync(Order order);
		Task AddOrderItemsAsync(IEnumerable<OrderItem> orderItems);
	}
}