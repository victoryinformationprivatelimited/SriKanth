using HRIS.Model.Employee_Module.Entities;
using Microsoft.EntityFrameworkCore;
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
		public async Task<List<Order>> GetListOfOrdersAsync(string salesPersonCode, OrderStatus orderStatus)
		{
			return await _context.Order
				.Where(o => o.SalesPersonCode == salesPersonCode && o.Status == orderStatus)
				.ToListAsync();
		}
		public async Task<List<Order>> GetAllOrdersAsync(OrderStatus orderStatus)
		{
			return await _context.Order
				.Where(o => o.Status== orderStatus)
				.ToListAsync();
		}
		public async Task<List<OrderItem>> GetOrderItemsByOrderNumbersAsync(List<int> orderNumbers)
		{
			return await _context.OrderItem
				.Where(oi => orderNumbers.Contains(oi.OrderNumber))
				.ToListAsync();
		}
		public async Task<List<Order>> GetListOfOrdersByOrderNumbersAsync(List<int> orderNumbers)
		{
			if (orderNumbers == null || !orderNumbers.Any())
			{
				return new List<Order>();
			}

			return await _context.Order
				.Where(o => orderNumbers.Contains(o.OrderNumber))
				.ToListAsync();
		}

		public async Task<Order> GetOrderByIdAsync(int orderNumber)
		{
			return await _context.Order.FirstOrDefaultAsync(m => m.OrderNumber == orderNumber);
		}
		public async Task UpdateOrderStatusAsync(Order order)
		{
			_context.Order.Update(order);
			await _context.SaveChangesAsync();
		}
		public async Task<List<OrderItem>> GetOrderItemsAsync(int orderNumber)
		{
			return await _context.OrderItem
				.Where(o => o.OrderNumber == orderNumber )
				.ToListAsync();
		}
		public async Task AddDocumentAsync(UserDocumentStorage userDocument)
		{
			await _context.UserDocumentStorage.AddAsync(userDocument);
			await _context.SaveChangesAsync();
		}
		public async Task<List<UserDocumentStorage>> GetUserDocumentsAsync(int userId)
		{
			return await _context.UserDocumentStorage
				.Where(d => d.UserId == userId)
				.ToListAsync();
		}
		public async Task<UserDocumentStorage?> GetUserDocumenByUrlAsync(string url)
		{
			return await _context.UserDocumentStorage.FirstOrDefaultAsync(m => m.DocumentReference == url);
		}

		public async Task RemoveDocumentAsync(UserDocumentStorage userDocument)
		{
			_context.UserDocumentStorage.Remove(userDocument);
			await _context.SaveChangesAsync();
		}
		public async Task<List<Order>> GetAllOrdersByLocationsAsync(List<string> locations, OrderStatus orderStatus)
		{
			return await _context.Order
				.Where(o => locations.Contains(o.LocationCode) && o.Status == orderStatus)
				.ToListAsync();
		}
	}
}
