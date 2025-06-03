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
		private readonly IExternalApiService _externalApiService;
		private readonly ILogger<BusinessApiService> _logger;
		private readonly ILoginData _loginData;
		private readonly IBusinessData _businessData;

		public BusinessApiService(
			IExternalApiService externalApiService,
			ILogger<BusinessApiService> logger,
			ILoginData loginData,
			IBusinessData businessData)
		{
			_externalApiService = externalApiService;
			_logger = logger;
			_loginData = loginData;
			_businessData = businessData;
		}

		public async Task<List<StockItem>> GetSalesStockDetails()
		{
			_logger.LogDebug("Entering GetSalesStockDetails method");
			try
			{
				_logger.LogInformation("Beginning to retrieve sales stock details");

				var inventoryTask = _externalApiService.GetInventoryBalanceAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPricesTask = _externalApiService.GetSalesPriceAsync();
				var locationsTask = _externalApiService.GetLocationsAsync();

				await Task.WhenAll(inventoryTask, itemsTask, salesPricesTask, locationsTask);

				var inventory = await inventoryTask;
				var items = await itemsTask;
				var salesPrices = await salesPricesTask;
				var locations = await locationsTask;

				if (items?.value == null || inventory?.value == null ||
					salesPrices?.value == null || locations?.value == null)
				{
					_logger.LogWarning("One or more required API responses returned null data");
					throw new ApplicationException("Required data not available from APIs");
				}

				var pictureTasks = items.value
					.Where(i => i?.no != null && i.systemId != Guid.Empty)
					.ToDictionary(
						item => item.no,
						item => _externalApiService.GetItemsPictureAsync(item.systemId)
					);

				await Task.WhenAll(pictureTasks.Values);

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

				var stockList = new List<StockItem>();
				foreach (var item in items.value.Where(i => i?.no != null))
				{
					if (!inventoryLookup.TryGetValue(item.no, out var itemInventory))
						continue;

					priceLookup.TryGetValue(item.no, out var itemPrice);

					foreach (var inv in itemInventory.Where(i => i != null))
					{
						locationLookup.TryGetValue(inv.locationCode, out var locationName);

						var picture = pictureTasks.TryGetValue(item.no, out var task) && task.IsCompletedSuccessfully
							? task.Result
							: null;

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

				_logger.LogInformation("Successfully retrieved {StockItemCount} stock items", stockList.Count);
				return stockList;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve stock details");
				throw new ApplicationException("Failed to retrieve stock details. Please try again later.", ex);
			}
		}

		public async Task<OrderCreationDetails> GetOrderCreationDetailsAsync()
		{
			try
			{
				_logger.LogInformation("Beginning to retrieve order creation details");

				var locationsTask = _externalApiService.GetLocationsAsync();
				var customersTask = _externalApiService.GetCustomerDetailsAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPriceTask = _externalApiService.GetSalesPriceAsync();

				await Task.WhenAll(locationsTask, customersTask, itemsTask, salesPriceTask);

				var locations = await locationsTask;
				if (locations?.value == null || !locations.value.Any())
				{
					_logger.LogWarning("Location data not available");
					throw new InvalidOperationException("No location data found.");
				}

				var customers = await customersTask;
				if (customers?.value == null || !customers.value.Any())
				{
					_logger.LogWarning("Customer data not available");
					throw new InvalidOperationException("No Customer data found.");
				}

				var items = await itemsTask;
				if (items?.value == null || !items.value.Any())
				{
					_logger.LogWarning("Item data not available");
					throw new InvalidOperationException("No Items data found.");
				}

				var salesPrices = await salesPriceTask;
				var priceLookup = salesPrices?.value?
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice)
					?? new Dictionary<string, decimal>();

				var paymentTypes = new List<PaymentType>
				{
					new PaymentType { Code = "CASH", Name = "Cash" },
					new PaymentType { Code = "CARD", Name = "Credit Card" },
					new PaymentType { Code = "BANK", Name = "Bank Transfer" },
					new PaymentType { Code = "CHEQUE", Name = "Cheque" }
				};

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

				_logger.LogInformation("Retrieved order creation details with {LocationCount} locations, {CustomerCount} customers, and {ItemCount} items",
					details.Locations.Count, details.Customers.Count, details.Items.Count);
				return details;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve order creation details");
				_logger.LogDebug("Exiting GetOrderCreationDetailsAsync method due to exception");
				throw new InvalidOperationException("Error while retrieving order creation details", ex);
			}
		}

		public async Task<OrderCreationDetails> GetFilteredOrderCreationDetailsAsync(int userId)
		{
			try
			{
				_logger.LogInformation("Beginning to retrieve filtered order details for user {UserId}", userId);

				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found", userId);
					throw new InvalidOperationException("User Not found");
				}

				var locationsTask = _externalApiService.GetLocationsAsync();
				var customersTask = _externalApiService.GetCustomerDetailsAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPriceTask = _externalApiService.GetSalesPriceAsync();

				await Task.WhenAll(locationsTask, customersTask, itemsTask, salesPriceTask);

				var locations = await locationsTask;
				var filteredLocations = locations?.value?
					.Where(l => l.code == user.LocationCode)
					.ToList() ?? new List<LocationDetail>();

				if (!filteredLocations.Any())
				{
					_logger.LogWarning("No location data found for code: {LocationCode}", user.LocationCode);
					throw new InvalidOperationException("No valid location data found for user.");
				}

				var customers = await customersTask;
				var filteredCustomers = customers?.value?
					.Where(c => c.salespersonCode == user.SalesPersonCode)
					.ToList() ?? new List<Customer>();

				var items = await itemsTask;
				if (items?.value == null || !items.value.Any())
				{
					_logger.LogWarning("No items data available");
					throw new InvalidOperationException("No Items data found.");
				}

				var salesPrices = await salesPriceTask;
				var priceLookup = salesPrices?.value?
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice)
					?? new Dictionary<string, decimal>();

				var paymentTypes = new List<PaymentType>
				{
					new PaymentType { Code = "CASH", Name = "Cash" },
					new PaymentType { Code = "CARD", Name = "Credit Card" },
					new PaymentType { Code = "BANK", Name = "Bank Transfer" },
					new PaymentType { Code = "CHEQUE", Name = "Cheque" }
				};

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

				_logger.LogInformation("Retrieved filtered order details for user {UserId} with {LocationCount} locations, {CustomerCount} customers, and {ItemCount} items",
					userId, details.Locations.Count, details.Customers.Count, details.Items.Count);
				return details;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve filtered order details for user {UserId}", userId);
				throw new InvalidOperationException("Error while retrieving filtered order details", ex);
			}
		}

		public async Task<ServiceResult> SubmitOrderAsync(int userId, OrderRequest request)
		{
			_logger.LogDebug("Entering SubmitOrderAsync method for user {UserId}", userId);
			try
			{
				_logger.LogInformation("Beginning order submission for user {UserId}, customer {CustomerCode}, location {LocationCode}",
					userId, request.CustomerCode, request.LocationCode);

				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found during order submission", userId);
					return new ServiceResult { Success = false, Message = "User not found" };
				}

				var creditValidation = await ValidateCustomerCredit(request.CustomerCode, request.TotalAmount);
				if (!creditValidation.Success)
				{
					_logger.LogWarning("Credit validation failed for customer {CustomerCode}", request.CustomerCode);
					return creditValidation;
				}

				var inventoryValidation = await ValidateInventory(request.Items, request.LocationCode);
				if (!inventoryValidation.Success)
				{
					_logger.LogWarning("Inventory validation failed for location {LocationCode}", request.LocationCode);
					return inventoryValidation;
				}

				var order = new Order
				{
					CustomerCode = request.CustomerCode,
					LocationCode = request.LocationCode,
					OrderDate = DateTime.UtcNow,
					Status = OrderStatus.Pending,
					TotalAmount = request.TotalAmount,
					SalesPersonCode = user.SalesPersonCode,
					PaymentMethodCode = request.PaymentMethodCode,
					Note = request.SpecialNote
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

				_logger.LogInformation("Successfully submitted order {OrderNumber} for customer {CustomerCode} by user {UserId}",
					order.OrderNumber, request.CustomerCode, userId);
				return new ServiceResult { Success = true, Message = "Order submitted successfully" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to submit order for user {UserId}", userId);
				return new ServiceResult { Success = false, Message = "Order submission failed. Please try again." };
			}
		}

		public async Task<List<OrderReturn>> GetOrdersListAsync(int userId, OrderStatus orderStatus)
		{
			try
			{
				_logger.LogInformation("Beginning to retrieve orders for user {UserId} with status {OrderStatus}",
					userId, orderStatus);

				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found while retrieving orders", userId);
					throw new ApplicationException("Failed to retrieve orders. Please try again later.");
				}

				var pendingOrders = await _businessData.GetListOfOrdersAsync(user.SalesPersonCode, orderStatus);
				var orderNumbers = pendingOrders.Select(o => o.OrderNumber).ToList();

				var orderItems = await _businessData.GetOrderItemsByOrderNumbersAsync(orderNumbers);

				var itemsByOrder = orderItems
					.GroupBy(i => i.OrderNumber)
					.ToDictionary(g => g.Key, g => g.ToList());

				var customersTask = _externalApiService.GetCustomersAsync();
				var salesPeopleTask = _externalApiService.GetSalesPeopleAsync();
				await Task.WhenAll(customersTask, salesPeopleTask);

				var customers = (await customersTask).value;
				var salesPeople = (await salesPeopleTask).value;

				var customerDict = customers.ToDictionary(c => c.no, c => c.name);
				var salesPersonDict = salesPeople.ToDictionary(s => s.code, s => s.name);

				var result = pendingOrders.Select(order => new OrderReturn
				{
					OrderNumber = order.OrderNumber,
					CustomerName = customerDict.TryGetValue(order.CustomerCode, out var custName) ? custName : string.Empty,
					SalesPersonName = salesPersonDict.TryGetValue(order.SalesPersonCode, out var spName) ? spName : string.Empty,
					OrderDate = order.OrderDate,
					PaymentMethodType = order.PaymentMethodCode,
					Status = order.Status.ToString(),
					SpecialNote = order.Note ?? string.Empty,
					TotalAmount = order.TotalAmount,
					Items = itemsByOrder.TryGetValue(order.OrderNumber, out var items)
						? items.Select(i => new OrderItemReturn
						{
							ItemCode = i.ItemCode,
							Description = i.Description,
							Quantity = i.Quantity,
							UnitPrice = i.UnitPrice,
							DiscountPercent = i.DiscountPercent
						}).ToList() : new List<OrderItemReturn>(),
					RejectReason = order.RejectReason ?? null
				}).ToList();

				_logger.LogInformation("Retrieved {OrderCount} {OrderStatus} orders for user {UserId}",
					result.Count, orderStatus, userId);
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve {OrderStatus} orders for user {UserId}", orderStatus, userId);
				_logger.LogDebug("Exiting GetOrdersListAsync method due to exception");
				throw new ApplicationException("Failed to retrieve orders. Please try again later.", ex);
			}
		}

		public async Task<ServiceResult> UpdateOrderStatusAsync(UpdateOrderRequest updateOrderRequest)
		{
			try
			{
				_logger.LogInformation("Beginning status update for order {OrderNumber} to status {OrderStatus}",
					updateOrderRequest.Ordernumber, updateOrderRequest.Status);

				var order = await _businessData.GetOrderByIdAsync(updateOrderRequest.Ordernumber);
				if (order == null)
				{
					_logger.LogWarning("Order {OrderNumber} not found during status update", updateOrderRequest.Ordernumber);
					return new ServiceResult { Success = false, Message = $"Order {updateOrderRequest.Ordernumber} not found." };
				}

				order.Status = updateOrderRequest.Status;
				order.RejectReason = updateOrderRequest.RejectReason ?? null;
				await _businessData.UpdateOrderStatusAsync(order);

				if (updateOrderRequest.Status == OrderStatus.Processing)
				{
					var customersTask = _externalApiService.GetCustomersAsync();
					var locationsTask = _externalApiService.GetLocationsAsync();
					await Task.WhenAll(customersTask, locationsTask);

					var customers = (await customersTask).value;
					var locations = (await locationsTask).value;

					var customer = customers?.FirstOrDefault(c => c.no == order.CustomerCode);
					var paymentTermCode = customer?.paymentTermsCode;

					if (customer == null)
					{
						_logger.LogWarning("Customer {CustomerCode} not found in external API during order processing", order.CustomerCode);
						return new ServiceResult { Success = false, Message = $"Customer {order.CustomerCode} not found in external API." };
					}

					var location = locations?.FirstOrDefault(l => l.code == order.LocationCode);
					var locationName = location?.name;

					if (location == null)
					{
						_logger.LogWarning("Location {LocationCode} not found in external API during order processing", order.LocationCode);
						return new ServiceResult { Success = false, Message = $"Location {order.LocationCode} not found in external API." };
					}

					var orderItems = await _businessData.GetOrderItemsAsync(updateOrderRequest.Ordernumber);

					var salesOrderRequest = new SalesOrderRequest
					{
						orderNo = order.OrderNumber.ToString(),
						customerNo = order.CustomerCode,
						orderDate = order.OrderDate.ToString("yyyy-MM-dd"),
						salespersonCode = order.SalesPersonCode,
						paymentMethodCode = order.PaymentMethodCode,
						paymentTermCode = paymentTermCode,
						salesIntegrationLines = orderItems
							.Select((line, index) => new SalesIntegrationLine
							{
								lineNo = index + 1,
								itemNo = line.ItemCode,
								description = line.Description,
								location = locationName,
								quantity = line.Quantity,
								unitPrice = line.UnitPrice,
								lineDiscount = line.DiscountPercent
							})
							.ToList()
					};

					try
					{
						await _externalApiService.PostSalesOrderAsync(salesOrderRequest);
						_logger.LogInformation("Successfully posted order {OrderNumber} to external API", updateOrderRequest.Ordernumber);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to send order {OrderNumber} to external API", updateOrderRequest.Ordernumber);
						return new ServiceResult
						{
							Success = false,
							Message = $"Failed to send Order {updateOrderRequest.Ordernumber} to external API."
						};
					}
				}

				_logger.LogInformation("Successfully updated status of order {OrderNumber} to {OrderStatus}",
					updateOrderRequest.Ordernumber, updateOrderRequest.Status);
				return new ServiceResult { Success = true, Message = "Order status updated successfully." };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to update status for order {OrderNumber}", updateOrderRequest.Ordernumber);
				return new ServiceResult { Success = false, Message = "Unexpected error updating order status." };
			}
		}

		private async Task<ServiceResult> ValidateCustomerCredit(string customerCode, decimal orderTotal)
		{
		try
			{
				var customersResponse = await _externalApiService.GetCustomerDetailsAsync();

				if (customersResponse?.value == null || !customersResponse.value.Any())
				{
					_logger.LogWarning("Customer data not available for validation");
					return new ServiceResult { Success = false, Message = "Customer data not available" };
				}

				var customer = customersResponse.value.FirstOrDefault(c => c.no == customerCode);

				if (customer == null)
				{
					_logger.LogWarning("Customer {CustomerCode} not found for validation", customerCode);
					return new ServiceResult { Success = false, Message = $"Customer {customerCode} not found" };
				}

				if (!customer.creditAllowed && orderTotal > 0)
				{
					_logger.LogWarning("Customer {CustomerCode} not allowed credit purchases", customerCode);
					return new ServiceResult { Success = false, Message = "Customer not allowed credit purchases" };
				}

				if (customer.creditAllowed && (customer.balanceLCY + orderTotal) > customer.creditLimitLCY)
				{
					_logger.LogWarning("Order exceeds credit limit for customer {CustomerCode}", customerCode);
					return new ServiceResult
					{
						Success = false,
						Message = $"Order exceeds credit limit (Limit: {customer.creditLimitLCY}, Balance: {customer.balanceLCY})"
					};
				}

				_logger.LogInformation("Exiting ValidateCustomerCredit with validation success");
				return new ServiceResult { Success = true, Message = "Credit validation passed" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during credit validation for customer {CustomerCode}", customerCode);
				return new ServiceResult { Success = false, Message = "Error during credit validation" };
			}
		}

		private async Task<ServiceResult> ValidateInventory(List<OrderItemRequest> items, string locationCode)
		{
			_logger.LogInformation("Entering ValidateInventory for location {LocationCode}", locationCode);
			try
			{
				var inventoryResponse = await _externalApiService.GetInventoryBalanceAsync();

				if (inventoryResponse?.value == null || !inventoryResponse.value.Any())
				{
					_logger.LogWarning("Inventory data not available for validation");
					_logger.LogDebug("Exiting ValidateInventory with validation failure");
					return new ServiceResult { Success = false, Message = "Inventory data not available" };
				}

				var filteredInventory = inventoryResponse.value
					.Where(i => i.locationCode == locationCode)
					.ToList();

				if (!filteredInventory.Any())
				{
					_logger.LogWarning("No inventory data found for location {LocationCode}", locationCode);
					_logger.LogDebug("Exiting ValidateInventory with validation failure");
					return new ServiceResult
					{
						Success = false,
						Message = $"No inventory data found for location: {locationCode}"
					};
				}

				var inventoryLookup = filteredInventory.ToDictionary(i => i.itemNo);

				foreach (var item in items)
				{
					if (!inventoryLookup.TryGetValue(item.ItemCode, out var inventoryItem))
					{
						_logger.LogWarning("Item {ItemCode} not found in inventory at location {LocationCode}",
							item.ItemCode, locationCode);
						_logger.LogDebug("Exiting ValidateInventory with validation failure");
						return new ServiceResult
						{
							Success = false,
							Message = $"Item {item.ItemCode} not found in inventory at location {locationCode}"
						};
					}

					if (inventoryItem.inventory < item.Quantity)
					{
						_logger.LogWarning("Insufficient stock for item {ItemCode} (Available: {Available}, Requested: {Requested})",
							item.ItemCode, inventoryItem.inventory, item.Quantity);
						_logger.LogDebug("Exiting ValidateInventory with validation failure");
						return new ServiceResult
						{
							Success = false,
							Message = $"Insufficient stock for item {item.ItemCode} (Available: {inventoryItem.inventory}, Requested: {item.Quantity})"
						};
					}
				}

				_logger.LogInformation("Exiting ValidateInventory with validation success");
				return new ServiceResult { Success = true, Message = "Inventory validation passed" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during inventory validation for location {LocationCode}", locationCode);
				return new ServiceResult { Success = false, Message = "Error during inventory validation" };
			}
		}
	}
}