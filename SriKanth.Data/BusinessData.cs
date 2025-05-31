using SriKanth.Model;
using SriKanth.Model.BusinessModule.Entities;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Data
{
	public class BusinessData : IBusinessData
	{

		private readonly SriKanthDbContext _context;
		public BusinessData(SriKanthDbContext dbContext)
		{
			_context = dbContext;
		}
		public async Task AddOrderAsync(Order order)
		{
			await _context.Order.AddAsync(order);
			await _context.SaveChangesAsync();
		}
		public async Task AddOrderItemsAsync(IEnumerable<OrderItem> orderItems)
		{
			await _context.OrderItem.AddRangeAsync(orderItems);
			await _context.SaveChangesAsync();
		}

	}
}
