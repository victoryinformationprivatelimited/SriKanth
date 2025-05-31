using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SriKanth.Data;
using SriKanth.Interface;
using SriKanth.Interface.Data;
using SriKanth.Model;
using SriKanth.Model.BusinessModule.DTOs;
using SriKanth.Model.BusinessModule.Entities;
using SriKanth.Model.ExistingApis;
using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service
{
	public class BusinessApiService : IBusinessApiService
	{
		public readonly IExternalApiService _externalApiService;
		private readonly ILogger<BusinessApiService> _logger;
		private readonly ILoginData _loginData;
		private readonly IBusinessData _businessData;

		public BusinessApiService(IExternalApiService externalApiService, ILogger<BusinessApiService> logger, ILoginData loginData, IBusinessData businessData)
		{
			_externalApiService = externalApiService;
			_logger = logger;
			_loginData = loginData;
			_businessData = businessData;
		}
		public async Task<List<StockItem>> GetSalesStockDetails()
		{
			try
			{
				// Get all required data in parallel
				var inventoryTask = _externalApiService.GetInventoryBalanceAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPricesTask = _externalApiService.GetSalesPriceAsync();
				var locationsTask = _externalApiService.GetLocationsAsync();

				await Task.WhenAll(inventoryTask, itemsTask, salesPricesTask, locationsTask);

				var inventory = await inventoryTask;
				var items = await itemsTask;
				var salesPrices = await salesPricesTask;
				var locations = await locationsTask;

				// Validate data
				if (items?.value == null || inventory?.value == null ||
					salesPrices?.value == null || locations?.value == null)
				{
					throw new ApplicationException("Required data not available from APIs");
				}

				// Pre-fetch pictures safely
				var pictureTasks = new Dictionary<string, Task<string>>();
				foreach (var item in items.value.Where(i => i?.no != null && i.systemId != Guid.Empty))
				{
					pictureTasks[item.no] = _externalApiService.GetItemsPictureAsync(item.systemId);
				}
				await Task.WhenAll(pictureTasks.Values);

				// Create lookup dictionaries with null checks
				var inventoryLookup = inventory.value
					.Where(i => i?.itemNo != null)
					.GroupBy(i => i.itemNo)
					.ToDictionary(g => g.Key, g => g.ToList());

				var priceLookup = salesPrices.value
					.Where(p => p?.itemNo != null)
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice);

				var locationLookup = locations.value
					.Where(l => l?.code != null)
					.ToDictionary(l => l.code, l => l.name);

				// Transform data
				var stockList = new List<StockItem>();
				foreach (var item in items.value.Where(i => i?.no != null))
				{
					if (!inventoryLookup.TryGetValue(item.no, out var itemInventory))
						continue;

					priceLookup.TryGetValue(item.no, out var itemPrice);

					foreach (var inv in itemInventory.Where(i => i != null))
					{
						locationLookup.TryGetValue(inv.locationCode, out var locationName);

						pictureTasks.TryGetValue(item.no, out var pictureTask);
						var picture = pictureTask?.IsCompletedSuccessfully == true ? pictureTask.Result : null;

						stockList.Add(new StockItem
						{
							ItemCode = item.no,
							ItemName = item.description ?? string.Empty,
							Location = locationName ?? inv.locationCode ?? "Unknown",
							Stock = inv.inventory > 40 ? "40+" : inv.inventory.ToString(),
							UnitPrice = itemPrice,
							ItemCategory = item.itemCategoryCode ?? string.Empty,
							Category = item.parentCategoryCode ?? string.Empty,
							SubCategory = item.childCategoryCode ?? string.Empty,
							Description = item.description ?? string.Empty,
							Description2 = item.description2 ?? string.Empty,
							UnitOfMeasure = item.unitOfMeasure ?? string.Empty,
							Size = item.size ?? string.Empty,
							ReorderQuantity = item.reorderQuantity,
							Image = picture
						});
					}
				}

				return stockList;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred while getting stock list");
				throw new ApplicationException("Failed to retrieve stock details. Please try again later.", ex);
			}
		}

		public async Task<OrderCreationDetails> GetOrderCreationDetailsAsync()
		{
			try
			{
				_logger.LogInformation("Retrieving Order creation details");

				// Get all data in parallel
				var locationsTask = _externalApiService.GetLocationsAsync();
				var customersTask = _externalApiService.GetCustomerDetailsAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPriceTask = _externalApiService.GetSalesPriceAsync();

				await Task.WhenAll(locationsTask, customersTask, itemsTask, salesPriceTask);

				// Process locations
				var locations = await locationsTask;
				if (locations?.value == null || !locations.value.Any())
				{
					_logger.LogWarning("No location data found");
					throw new InvalidOperationException("No location data found.");
				}

				// Process customers
				var customers = await customersTask;
				if (customers?.value == null || !customers.value.Any())
				{
					_logger.LogWarning("No Customer data found");
					throw new InvalidOperationException("No Customer data found.");
				}

				// Process items and prices
				var items = await itemsTask;
				if (items?.value == null || !items.value.Any())
				{
					_logger.LogWarning("No Items data found");
					throw new InvalidOperationException("No Items data found.");
				}

				var salesPrices = await salesPriceTask;
				var priceLookup = salesPrices?.value?
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice)
					?? new Dictionary<string, decimal>();

				// Create fixed payment types
				var paymentTypes = new List<PaymentType>
				{
					new PaymentType { Code = "CASH", Name = "Cash" },
					new PaymentType { Code = "CARD", Name = "Credit Card" },
					new PaymentType { Code = "BANK", Name = "Bank Transfer" },
					new PaymentType { Code = "CHEQUE", Name = "Cheque" }
				};

				// Transform data
				var details = new OrderCreationDetails
				{
					Locations = locations.value.Select(l => new Location
					{
						LocationCode = l.code,
						LocationName = l.name
					}).ToList(),

					Customers = customers.value.Select(c => new OrderCustomer
					{
						CustomerCode = c.no,
						CustomerName = c.name,
						DueAmount = c.creditLimitLCY - c.balanceLCY,
						CreditAllowed = c.creditAllowed,
						CreditLimit = c.creditLimitLCY,
						BalanceCredit = c.balanceLCY
					}).ToList(),

					PaymentTypes = paymentTypes,

					Items = items.value.Select(i => new OrderItemDetails
					{
						ItemCode = i.no,
						ItemName = i.description,
						Unitprice = priceLookup.TryGetValue(i.no, out var price) ? price.ToString() : "0",
						SubstituteItems = i.itemsubstitutions?.FirstOrDefault() == null ? null : new SubstituteItem
						{
							ItemCode = i.itemsubstitutions.First().substituteNo,
							ItemName = i.itemsubstitutions.First().description,
							UnitPrice = priceLookup.TryGetValue(i.itemsubstitutions.First().substituteNo, out var subPrice)
								? subPrice
								: 0
						}
					}).ToList()
				};

				_logger.LogInformation($"Retrieved order creation details: " +
									 $"{details.Locations.Count} locations, " +
									 $"{details.Customers.Count} customers, " +
									 $"{details.Items.Count} items");

				return details;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while retrieving order creation details");
				throw new InvalidOperationException("Error while retrieving order creation details", ex);
			}

		}

		public async Task<OrderCreationDetails> GetFilteredOrderCreationDetailsAsync(int userId)
		{
			try
			{
				_logger.LogInformation($"Retrieving filtered order details for user:{userId}");
				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning($"User with UserId:{userId} not found ");
					throw new InvalidOperationException("User Not found");
				}
				string userLocationCode = user.LocationCode;
				string salesPersonCode = user.SalesPersonCode;
				// Get all data in parallel
				var locationsTask = _externalApiService.GetLocationsAsync();
				var customersTask = _externalApiService.GetCustomerDetailsAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPriceTask = _externalApiService.GetSalesPriceAsync();

				await Task.WhenAll(locationsTask, customersTask, itemsTask, salesPriceTask);

				// Process locations - filter by user's location
				var locations = await locationsTask;
				var filteredLocations = locations?.value?
					.Where(l => l.code == userLocationCode)
					.ToList() ?? new List<LocationDetail>();

				if (!filteredLocations.Any())
				{
					_logger.LogWarning($"No location data found for code: {userLocationCode}");
					throw new InvalidOperationException("No valid location data found for user.");
				}

				// Process customers - filter by salesperson code
				var customers = await customersTask;
				var filteredCustomers = customers?.value?
					.Where(c => c.salespersonCode == salesPersonCode)
					.ToList() ?? new List<Customer>();

				// Process items and prices (no filtering)
				var items = await itemsTask;
				if (items?.value == null || !items.value.Any())
				{
					_logger.LogWarning("No Items data found");
					throw new InvalidOperationException("No Items data found.");
				}

				var salesPrices = await salesPriceTask;
				var priceLookup = salesPrices?.value?
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice)
					?? new Dictionary<string, decimal>();

				// Create fixed payment types
				var paymentTypes = new List<PaymentType>
				{
					new PaymentType { Code = "CASH", Name = "Cash" },
					new PaymentType { Code = "CARD", Name = "Credit Card" },
					new PaymentType { Code = "BANK", Name = "Bank Transfer" },
					new PaymentType { Code = "CHEQUE", Name = "Cheque" }
				};

				// Transform data
				var details = new OrderCreationDetails
				{
					Locations = filteredLocations.Select(l => new Location
					{
						LocationCode = l.code,
						LocationName = l.name
					}).ToList(),

					Customers = filteredCustomers.Select(c => new OrderCustomer
					{
						CustomerCode = c.no,
						CustomerName = c.name,
						DueAmount = c.balanceLCY,
						CreditAllowed = c.creditAllowed,
						CreditLimit = c.creditLimitLCY,
						BalanceCredit = c.creditLimitLCY - c.balanceLCY
					}).ToList(),

					PaymentTypes = paymentTypes,

					Items = items.value.Select(i => new OrderItemDetails
					{
						ItemCode = i.no,
						ItemName = i.description,
						Unitprice = priceLookup.TryGetValue(i.no, out var price) ? price.ToString() : "0",
						SubstituteItems = i.itemsubstitutions?.FirstOrDefault() == null ? null : new SubstituteItem
						{
							ItemCode = i.itemsubstitutions.First().substituteNo,
							ItemName = i.itemsubstitutions.First().description,
							UnitPrice = priceLookup.TryGetValue(i.itemsubstitutions.First().substituteNo, out var subPrice)
								? subPrice
								: 0
						}
					}).ToList()
				};

				_logger.LogInformation($"Retrieved filtered order details: " +
									 $"{details.Locations.Count} locations, " +
									 $"{details.Customers.Count} customers, " +
									 $"{details.Items.Count} items");

				return details;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error while retrieving filtered order details for userId: {userId}");
				throw new InvalidOperationException("Error while retrieving filtered order details", ex);
			}
		}

		public async Task<ServiceResult> SubmitOrderAsync(int userId, OrderRequest request)
		{
			try
			{
				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning($"User with UserId: {userId} not found.");
					return new ServiceResult { Success = false, Message = "User not found" };
				}

				var creditValidation = await ValidateCustomerCredit(request.CustomerCode, request.TotalAmount);
				if (!creditValidation.Success)
				{
					return creditValidation;
				}

				var inventoryValidation = await ValidateInventory(request.Items, request.LocationCode);
				if (!inventoryValidation.Success)
				{
					return inventoryValidation;
				}

				var order = new Order
				{
					CustomerCode = request.CustomerCode,
					LocationCode = request.LocationCode,
					OrderDate = DateTime.UtcNow,
					Status = "Pending",
					TotalAmount = request.TotalAmount,
					SalesPersonCode = user.SalesPersonCode,
					PaymentMethodCode = request.PaymentMethodCode,
				};
				await _businessData.AddOrderAsync(order);
				var orderItems = request.Items.Select(item => new OrderItem
				{
					ItemCode = item.ItemCode,
					OrderNumber = order.OrderNumber,
					Description = item.Description,
					Quantity = item.Quantity,
					UnitPrice = item.UnitPrice,
					DiscountPercent = item.DiscountPercent
				}).ToList();
				await _businessData.AddOrderItemsAsync(orderItems);
				return new ServiceResult { Success = true, Message = "Order submitted successfully" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Order submission failed");
				return new ServiceResult { Success = false, Message = "Order submission failed. Please try again." };
			}
		}

		private async Task<ServiceResult> ValidateCustomerCredit(string customerCode, decimal orderTotal)
		{
			var customersResponse = await _externalApiService.GetCustomerDetailsAsync();

			if (customersResponse?.value == null || !customersResponse.value.Any())
			{
				return new ServiceResult { Success = false, Message = "Customer data not available" };
			}

			var customer = customersResponse.value.FirstOrDefault(c => c.no == customerCode);

			if (customer == null)
			{
				return new ServiceResult { Success = false, Message = $"Customer {customerCode} not found" };
			}

			if (!customer.creditAllowed && orderTotal > 0)
			{
				return new ServiceResult { Success = false, Message = "Customer not allowed credit purchases" };
			}

			if (customer.creditAllowed && (customer.balanceLCY + orderTotal) > customer.creditLimitLCY)
			{
				return new ServiceResult { Success = false, Message = "Order exceeds credit limit" };
			}

			return new ServiceResult { Success = true, Message = "Credit validation passed" };
		}


		private async Task<ServiceResult> ValidateInventory(List<OrderItemRequest> items, string locationCode)
		{
			var inventoryResponse = await _externalApiService.GetInventoryBalanceAsync();

			if (inventoryResponse?.value == null || !inventoryResponse.value.Any())
			{
				return new ServiceResult { Success = false, Message = "Inventory data not available" };
			}

			var filteredInventory = inventoryResponse.value
				.Where(i => i.locationCode == locationCode)
				.ToList();

			if (!filteredInventory.Any())
			{
				return new ServiceResult { Success = false, Message = $"No inventory data found for location: {locationCode}" };
			}

			var inventoryLookup = filteredInventory.ToDictionary(i => i.itemNo);

			foreach (var item in items)
			{
				if (!inventoryLookup.TryGetValue(item.ItemCode, out var inventoryItem))
				{
					return new ServiceResult
					{
						Success = false,
						Message = $"Item {item.ItemCode} not found in inventory at location {locationCode}"
					};
				}

				if (inventoryItem.inventory < item.Quantity)
				{
					return new ServiceResult
					{
						Success = false,
						Message = $"Insufficient stock for item {item.ItemCode} (Available: {inventoryItem.inventory}, Requested: {item.Quantity})"
					};
				}
			}

			return new ServiceResult { Success = true, Message = "Inventory validation passed" };
		}

	}
}
