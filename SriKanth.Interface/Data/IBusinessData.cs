﻿using HRIS.Model.Employee_Module.Entities;
using SriKanth.Model.BusinessModule.Entities;

namespace SriKanth.Data
{
	public interface IBusinessData
	{
		Task AddOrderAsync(Order order);
		Task AddOrderItemsAsync(IEnumerable<OrderItem> orderItems);
		Task<List<Order>> GetListOfOrdersAsync(string salesPersonCode, OrderStatus orderStatus);
		Task<List<Order>> GetAllOrdersAsync(OrderStatus orderStatus);
		Task<List<OrderItem>> GetOrderItemsByOrderNumbersAsync(List<int> orderNumbers);
		Task<List<Order>> GetListOfOrdersByOrderNumbersAsync(List<int> orderNumbers);
		Task<Order> GetOrderByIdAsync(int orderNumber);
		Task UpdateOrderStatusAsync(Order order);
		Task<List<OrderItem>> GetOrderItemsAsync(int orderNumber);
		Task AddDocumentAsync(UserDocumentStorage userDocument);
		Task<List<UserDocumentStorage>> GetUserDocumentsAsync(int userId);
		Task<UserDocumentStorage?> GetUserDocumenByUrlAsync(string url);
		Task RemoveDocumentAsync(UserDocumentStorage userDocument);
		Task<List<Order>> GetAllOrdersByLocationsAsync(List<string> locations, OrderStatus orderStatus);
	}
}