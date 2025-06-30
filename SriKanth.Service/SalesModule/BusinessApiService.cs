using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SriKanth.Data;
using SriKanth.Interface.Data;
using SriKanth.Interface.SalesModule;
using SriKanth.Model;
using SriKanth.Model.BusinessModule.DTOs;
using SriKanth.Model.BusinessModule.Entities;
using SriKanth.Model.ExistingApis;
using SriKanth.Model.Login_Module.DTOs;

namespace SriKanth.Service.SalesModule
{
	/// <summary>
	/// Service class for handling business operations related to sales, orders, and inventory
	/// </summary>
	public class BusinessApiService : IBusinessApiService
	{
		private readonly IExternalApiService _externalApiService;
		private readonly ILogger<BusinessApiService> _logger;
		private readonly ILoginData _loginData;
		private readonly IBusinessData _businessData;

		/// <summary>
		/// Initializes a new instance of the BusinessApiService class
		/// </summary>
		/// <param name="externalApiService">Service for external API calls</param>
		/// <param name="logger">Logger instance</param>
		/// <param name="loginData">Data access for login-related operations</param>
		/// <param name="businessData">Data access for business operations</param>
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

		/// <summary>
		/// Retrieves detailed stock information including inventory levels, prices, and item details
		/// </summary>
		/// <returns>List of stock items with comprehensive details</returns>
		public async Task<List<StockItem>> GetSalesStockDetails()
		{
			_logger.LogDebug("Entering GetSalesStockDetails method");
			try
			{
				_logger.LogInformation("Beginning to retrieve sales stock details");

				// Execute all API calls in parallel for better performance
				var inventoryTask = _externalApiService.GetInventoryBalanceAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPricesTask = _externalApiService.GetSalesPriceAsync();
				var locationsTask = _externalApiService.GetLocationsAsync();

				await Task.WhenAll(inventoryTask, itemsTask, salesPricesTask, locationsTask);

				// Retrieve results from completed tasks
				var inventory = await inventoryTask;
				var items = await itemsTask;
				var salesPrices = await salesPricesTask;
				var locations = await locationsTask;

				// Validate API responses
				if (items?.value == null || inventory?.value == null ||
					salesPrices?.value == null || locations?.value == null)
				{
					_logger.LogWarning("One or more required API responses returned null data");
					throw new ApplicationException("Required data not available from APIs");
				}

				// Fetch item pictures in parallel
				var pictureTasks = items.value
					.Where(i => i?.no != null && i.systemId != Guid.Empty)
					.ToDictionary(
						item => item.no,
						item => _externalApiService.GetItemsPictureAsync(item.systemId)
					);

				await Task.WhenAll(pictureTasks.Values);

				// Create lookup dictionaries for efficient data access
				var inventoryLookup = inventory.value
					.Where(i => i?.itemNo != null && i?.locationCode != null)
					.ToDictionary(i => (i.itemNo, i.locationCode), i => i);

				var priceLookup = salesPrices.value
					.Where(p => p?.itemNo != null)
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice);

				var locationLookup = locations.value
					.Where(l => l?.code != null)
					.ToDictionary(l => l.code, l => l.name);

				var stockList = new List<StockItem>();

				// Process each item and location combination
				foreach (var item in items.value.Where(i => i?.no != null))
				{
					foreach (var location in locations.value.Where(l => l?.code != null))
					{
						var key = (item.no, location.code);
						if (!inventoryLookup.TryGetValue(key, out var inv))
							continue; // Skip items with no inventory at this location

						priceLookup.TryGetValue(item.no, out var itemPrice);

						// Retrieve item picture if available
						var picture = pictureTasks.TryGetValue(item.no, out var task) && task.IsCompletedSuccessfully
							? task.Result
							: null;

						stockList.Add(new StockItem
						{
							ItemCode = item.no,
							ItemName = item.description ?? string.Empty,
							Location = location.name ?? location.code ?? "Unknown",
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

		/// <summary>
		/// Retrieves all necessary details for creating a new order
		/// </summary>
		/// <returns>OrderCreationDetails containing locations, customers, payment types and items</returns>
		public async Task<OrderCreationDetails> GetOrderCreationDetailsAsync()
		{
			try
			{
				_logger.LogInformation("Beginning to retrieve order creation details");

				// Execute all API calls in parallel
				var locationsTask = _externalApiService.GetLocationsAsync();
				var customersTask = _externalApiService.GetCustomersAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPriceTask = _externalApiService.GetSalesPriceAsync();

				await Task.WhenAll(locationsTask, customersTask, itemsTask, salesPriceTask);

				// Process locations data
				var locations = await locationsTask;
				if (locations?.value == null || !locations.value.Any())
				{
					_logger.LogWarning("Location data not available");
					throw new InvalidOperationException("No location data found.");
				}

				// Process customers data
				var customers = await customersTask;
				if (customers?.value == null || !customers.value.Any())
				{
					_logger.LogWarning("Customer data not available");
					throw new InvalidOperationException("No Customer data found.");
				}

				// Process items data
				var items = await itemsTask;
				if (items?.value == null || !items.value.Any())
				{
					_logger.LogWarning("Item data not available");
					throw new InvalidOperationException("No Items data found.");
				}

				// Create price lookup dictionary
				var salesPrices = await salesPriceTask;
				var priceLookup = salesPrices?.value?
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice)
					?? new Dictionary<string, decimal>();

				// Compile all order creation details
				var details = new OrderCreationDetails
				{
					Locations = locations.value.Select(l => new Model.Login_Module.DTOs.Location
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
						BalanceCredit = c.balanceLCY,
						PaymentTermCode = c.paymentTermsCode,
						PaymentMethodCode = c.paymentMethodCode
					}).ToList(),

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

		/// <summary>
		/// Retrieves filtered order creation details based on user permissions and assigned locations
		/// </summary>
		/// <param name="userId">ID of the user making the request</param>
		/// <returns>Filtered OrderCreationDetails specific to the user</returns>
		public async Task<OrderCreationDetails> GetFilteredOrderCreationDetailsAsync(int userId)
		{
			try
			{
				_logger.LogInformation("Beginning to retrieve filtered order details for user {UserId}", userId);

				// Get user and validate existence
				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found", userId);
					throw new InvalidOperationException("User Not found");
				}

				// Get user's assigned locations
				var userLocations = await _loginData.GetUserLocationCodesAsync(userId);
				if (userLocations == null || !userLocations.Any())
				{
					_logger.LogWarning("No locations found for user {UserId}", userId);
					throw new InvalidOperationException("User has no locations assigned.");
				}

				// Execute all API calls in parallel
				var locationsTask = _externalApiService.GetLocationsAsync();
				var customersTask = _externalApiService.GetCustomersAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPriceTask = _externalApiService.GetSalesPriceAsync();
				var inventoryTask = _externalApiService.GetInventoryBalanceAsync();

				await Task.WhenAll(locationsTask, customersTask, itemsTask, salesPriceTask, inventoryTask);

				// Filter locations to only those assigned to the user
				var locations = await locationsTask;
				var filteredLocations = locations?.value?
					 .Where(l => userLocations.Contains(l.code))
					.ToList() ?? new List<LocationDetail>();

				if (!filteredLocations.Any())
				{
					_logger.LogWarning("No location data found for user {UserId}", userId);
					throw new InvalidOperationException("No valid location data found for user.");
				}

				// Filter customers by salesperson code
				var customers = await customersTask;
				var filteredCustomers = customers?.value?
					.Where(c => c.salespersonCode == user.SalesPersonCode)
					.ToList() ?? new List<Customer>();

				// Process items data
				var items = await itemsTask;
				if (items?.value == null || !items.value.Any())
				{
					_logger.LogWarning("No items data available");
					throw new InvalidOperationException("No Items data found.");
				}

				// Create price lookup dictionary
				var salesPrices = await salesPriceTask;
				var priceLookup = salesPrices?.value?
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice)
					?? new Dictionary<string, decimal>();

				// Filter inventory to only user's locations with stock
				var inventory = await inventoryTask;
				var inventoryAtLocations = inventory?.value?
					.Where(i => userLocations.Contains(i.locationCode) && i.inventory > 0)
					.ToList() ?? new List<InventoryBalance>();

				// Get item numbers available at these locations
				var availableItemNos = new HashSet<string>(inventoryAtLocations.Select(i => i.itemNo));


				// Compile all filtered order creation details
				var details = new OrderCreationDetails
				{
					Locations = filteredLocations.Select(l => new Model.Login_Module.DTOs.Location
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
						BalanceCredit = c.creditLimitLCY - c.balanceLCY,
						PaymentTermCode = c.paymentTermsCode,
						PaymentMethodCode = c.paymentMethodCode
					}).ToList(),


					Items = items.value
						.Where(i => availableItemNos.Contains(i.no))
						.Select(i => new OrderItemDetails
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

		/// <summary>
		/// Submits a new order after validating customer credit and inventory availability
		/// </summary>
		/// <param name="userId">ID of the user submitting the order</param>
		/// <param name="request">Order details including items and customer information</param>
		/// <returns>ServiceResult indicating success or failure</returns>
		public async Task<ServiceResult> SubmitOrderAsync(int userId, OrderRequest request)
		{
			_logger.LogDebug("Entering SubmitOrderAsync method for user {UserId}", userId);
			try
			{
				_logger.LogInformation("Beginning order submission for user {UserId}, customer {CustomerCode}, location {LocationCode}",
					userId, request.CustomerCode, request.LocationCode);

				// Validate user exists
				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found during order submission", userId);
					return new ServiceResult { Success = false, Message = "User not found" };
				}

				// Validate customer credit
				var creditValidation = await ValidateCustomerCredit(request.CustomerCode, request.TotalAmount);
				if (!creditValidation.Success)
				{
					_logger.LogWarning("Credit validation failed for customer {CustomerCode}", request.CustomerCode);
					return creditValidation;
				}

				// Validate inventory availability
				var inventoryValidation = await ValidateInventory(request.Items, request.LocationCode);
				if (!inventoryValidation.Success)
				{
					_logger.LogWarning("Inventory validation failed for location {LocationCode}", request.LocationCode);
					return inventoryValidation;
				}

				// Create order record
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

				// Create order items
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

		/// <summary>
		/// Retrieves a list of orders filtered by status and user's salesperson code
		/// </summary>
		/// <param name="userId">ID of the user making the request</param>
		/// <param name="orderStatus">Status of orders to retrieve</param>
		/// <returns>List of orders with detailed information</returns>
		public async Task<List<OrderReturn>> GetOrdersListAsync(int userId, OrderStatus orderStatus)
		{
			try
			{
				_logger.LogInformation("Beginning to retrieve orders for user {UserId} with status {OrderStatus}",
					userId, orderStatus);

				// Validate user exists
				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found while retrieving orders", userId);
					throw new ApplicationException("Failed to retrieve orders. Please try again later.");
				}

				string userRole = await _loginData.GetUserRoleNameAsync(user.UserRoleId);
				List<Order> orders;

				// Get orders filtered by salesperson and status
				if (userRole == "Admin" || userRole == "SalesCoordinator")
				{
					orders = await _businessData.GetAllOrdersAsync(orderStatus);
				}
				else
				{
					orders = await _businessData.GetListOfOrdersAsync(user.SalesPersonCode, orderStatus);
				}

				if (!orders.Any())
				{
					_logger.LogInformation("No orders found for user {UserId} with status {OrderStatus}", userId, orderStatus);
					return new List<OrderReturn>();
				}

				var orderNumbers = orders.Select(o => o.OrderNumber).ToList();

				// Get all items for these orders
				var orderItems = await _businessData.GetOrderItemsByOrderNumbersAsync(orderNumbers);

				// Group items by order number for efficient lookup
				var itemsByOrder = orderItems
					.GroupBy(i => i.OrderNumber)
					.ToDictionary(g => g.Key, g => g.ToList());

				// Get customer and salesperson details in parallel
				var customersTask = _externalApiService.GetCustomersAsync();
				var salesPeopleTask = _externalApiService.GetSalesPeopleAsync();

				// Create list of tasks to wait for
				var tasks = new List<Task> { customersTask, salesPeopleTask };
				Task<InvoiceApiResponse>? invoiceTask = null;

				// Only fetch invoices if delivered orders are requested
				if (orderStatus == OrderStatus.Delivered)
				{
					invoiceTask = _externalApiService.GetInvoiceDetailsAsync();
					tasks.Add(invoiceTask);
				}

				await Task.WhenAll(tasks);

				// Safely extract results with null checks
				var customersResponse = await customersTask;
				var salesPeopleResponse = await salesPeopleTask;

				if (customersResponse?.value == null || salesPeopleResponse?.value == null)
				{
					_logger.LogError("Failed to retrieve customer or salesperson data from external API");
					throw new ApplicationException("Failed to retrieve required data. Please try again later.");
				}

				var customers = customersResponse.value;
				var salesPeople = salesPeopleResponse.value;

				// Create lookup dictionaries for names
				var customerDict = customers.ToDictionary(c => c.no, c => c.name);
				var salesPersonDict = salesPeople.ToDictionary(s => s.code, s => s.name);

				// Prepare invoice lookup if needed
				var invoiceLines = new List<SriKanth.Model.ExistingApis.Invoice>();
				if (orderStatus == OrderStatus.Delivered && invoiceTask != null)
				{
					var invoiceResponse = await invoiceTask;
					if (invoiceResponse?.value != null)
					{
						invoiceLines = invoiceResponse.value;
					}
					else
					{
						_logger.LogWarning("Invoice data not available for delivered orders");
					}
				}

				// Compile order return objects with all details
				var result = orders.Select(order =>
				{
					// Ordered items from order
					var orderedItems = itemsByOrder.TryGetValue(order.OrderNumber, out var items)
						? items.Select(i => new OrderItemReturn
						{
							ItemCode = i.ItemCode,
							Description = i.Description,
							Quantity = i.Quantity,
							UnitPrice = i.UnitPrice,
							DiscountPercent = i.DiscountPercent
						}).ToList()
						: new List<OrderItemReturn>();

					// Invoiced items: for delivered orders, from invoice lines; otherwise, null
					List<OrderItemReturn>? invoicedItems = null;
					if (orderStatus == OrderStatus.Delivered && invoiceLines.Any())
					{
						try
						{
							// Filter invoice lines for this order with better error handling
							var lines = invoiceLines
								.Where(inv =>
									inv != null &&
									!string.IsNullOrWhiteSpace(inv.OrderNo) &&
									int.TryParse(inv.OrderNo.Trim(), out int orderNo) &&
									orderNo == order.OrderNumber)
								.Select(inv => new OrderItemReturn
								{
									ItemCode = inv.No ?? string.Empty,
									Description = inv.Description ?? string.Empty,
									Quantity = inv.Quantity,
									UnitPrice = inv.UnitPrice,
									DiscountPercent = inv.LineDiscount
								})
								.ToList();

							invoicedItems = lines.Any() ? lines : null;
						}
						catch (Exception ex)
						{
							_logger.LogWarning(ex, "Failed to process invoice lines for order {OrderNumber}", order.OrderNumber);
							invoicedItems = null;
						}
					}

					return new OrderReturn
					{
						OrderNumber = order.OrderNumber,
						CustomerName = customerDict.TryGetValue(order.CustomerCode, out var custName) ? custName : string.Empty,
						SalesPersonName = salesPersonDict.TryGetValue(order.SalesPersonCode, out var spName) ? spName : string.Empty,
						OrderDate = order.OrderDate,
						PaymentMethodType = order.PaymentMethodCode,
						Status = order.Status.ToString(),
						SpecialNote = order.Note ?? string.Empty,
						TotalAmount = order.TotalAmount,
						OrderedItems = orderedItems,
						InvoicedItems = invoicedItems,
						RejectReason = order.RejectReason,
						DeliveryDate = orderStatus == OrderStatus.Delivered ? order.DeliveryDate : null,
						TrackingNumber = orderStatus == OrderStatus.Delivered ? order.TrackingNumber : null,
						DelivertPersonName = orderStatus == OrderStatus.Delivered ? order.DelivertPersonName : null
					};
				}).ToList();

				_logger.LogInformation("Retrieved {OrderCount} {OrderStatus} orders for user {UserId}",
					result.Count, orderStatus, userId);
				return result;
			}
			catch (ApplicationException)
			{
				// Re-throw application exceptions as they are already user-friendly
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve {OrderStatus} orders for user {UserId}", orderStatus, userId);
				throw new ApplicationException("Failed to retrieve orders. Please try again later.", ex);
			}
		}

		/// <summary>
		/// Updates the status of an order and performs related business logic
		/// </summary>
		/// <param name="updateOrderRequest">Request containing order number and new status</param>
		/// <returns>ServiceResult indicating success or failure</returns>
		public async Task<ServiceResult> UpdateOrderStatusAsync(UpdateOrderRequest updateOrderRequest)
		{
			try
			{
				_logger.LogInformation("Beginning status update for order {OrderNumber} to status {OrderStatus}",
					updateOrderRequest.Ordernumber, updateOrderRequest.Status);

				// Get the order to update
				var order = await _businessData.GetOrderByIdAsync(updateOrderRequest.Ordernumber);
				if (order == null)
				{
					_logger.LogWarning("Order {OrderNumber} not found during status update", updateOrderRequest.Ordernumber);
					return new ServiceResult { Success = false, Message = $"Order {updateOrderRequest.Ordernumber} not found." };
				}
				// Validate status transition
				var isValidTransition = await ValidateStatusTransition(order.Status, updateOrderRequest.Status);
				if (!isValidTransition)
				{
					_logger.LogWarning("Invalid status transition for order {OrderNumber}: {CurrentStatus} -> {NewStatus}",
						order.OrderNumber, order.Status, updateOrderRequest.Status);
					return new ServiceResult
					{
						Success = false,
						Message = $"Invalid status transition from {order.Status} to {updateOrderRequest.Status}."
					};
				}
				if (order.Status == OrderStatus.Delivered)
				{
					bool isInvoiced = await CheckInvoicedStatusAsync(order);
					if(isInvoiced)
					{
						order.TrackingNumber = updateOrderRequest.TrackingNumber;
						order.DelivertPersonName = updateOrderRequest.DelivertPersonName;
						order.DeliveryDate = DateOnly.FromDateTime(updateOrderRequest.DeliveryDate.Value);
						order.Note = updateOrderRequest.Note ?? null;

						order.Status = OrderStatus.Delivered;
						await _businessData.UpdateOrderStatusAsync(order);

						_logger.LogInformation("Order {OrderNumber} marked as Delivered after invoice check.", order.OrderNumber);
						return new ServiceResult { Success = true, Message = "Order status updated to Delivered." };

					}
					else
					{
						_logger.LogWarning("Order {OrderNumber} cannot be updated to Delivered status as it is not invoiced", updateOrderRequest.Ordernumber);
						return new ServiceResult { Success = false, Message = $"Order {updateOrderRequest.Ordernumber} cannot be updated to Delivered status as it is not invoiced." };
					}
				}
				// Update order status
				order.Status = updateOrderRequest.Status;
				order.RejectReason = updateOrderRequest.RejectReason ?? null;
				await _businessData.UpdateOrderStatusAsync(order);

				// Additional processing for orders moving to Processing status
				if (updateOrderRequest.Status == OrderStatus.Processing)
				{
					// Get customer and location details in parallel
					var customersTask = _externalApiService.GetCustomersAsync();
					var locationsTask = _externalApiService.GetLocationsAsync();
					await Task.WhenAll(customersTask, locationsTask);

					var customers = (await customersTask).value;
					var locations = (await locationsTask).value;

					// Validate customer exists
					var customer = customers?.FirstOrDefault(c => c.no == order.CustomerCode);
					var paymentTermCode = customer?.paymentTermsCode;
					var paymentMethodCode = customer?.paymentMethodCode;

					if (customer == null)
					{
						_logger.LogWarning("Customer {CustomerCode} not found in external API during order processing", order.CustomerCode);
						return new ServiceResult { Success = false, Message = $"Customer {order.CustomerCode} not found in external API." };
					}

					// Validate location exists
					var location = locations?.FirstOrDefault(l => l.code == order.LocationCode);
					var locationCode = location?.code;

					if (location == null)
					{
						_logger.LogWarning("Location {LocationCode} not found in external API during order processing", order.LocationCode);
						return new ServiceResult { Success = false, Message = $"Location {order.LocationCode} not found in external API." };
					}

					// Get order items
					var orderItems = await _businessData.GetOrderItemsAsync(updateOrderRequest.Ordernumber);

					// Prepare sales order request for external API
					var salesOrderRequest = new SalesOrderRequest
					{
						orderNo = order.OrderNumber.ToString(),
						customerNo = order.CustomerCode,
						orderDate = order.OrderDate.ToString("yyyy-MM-dd"),
						salespersonCode = order.SalesPersonCode,
						paymentMethodCode = paymentMethodCode,   
						paymentTermCode = paymentTermCode,
						salesIntegrationLines = orderItems
							.Select((line, index) => new SalesIntegrationLine
							{
								lineNo = line.OrderItemId,
								itemNo = line.ItemCode,
								description = line.Description,
								location = locationCode,
								quantity = line.Quantity,
								unitPrice = line.UnitPrice,
								lineDiscount = line.DiscountPercent
							})
							.ToList()
					};

					try
					{
						// Submit to external API
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

		/// <summary>
		/// Retrieves invoice details for customers assigned to the specified user
		/// </summary>
		/// <param name="userId">ID of the user making the request</param>
		/// <returns>CustomerInvoiceReturn containing invoice details and total due amount</returns>
		public async Task<CustomerInvoiceReturn> GetCustomerInvoicesAsync(int userId)
		{
			try
			{
				_logger.LogInformation("Beginning to retrieve Customer Invoice Details for user {UserId} ", userId);

				// Validate user exists
				var user = await _loginData.GetUserByIdAsync(userId);
				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found while retrieving invoices", userId);
					throw new ApplicationException("Failed to retrieve invoices. Please try again later.");
				}
				string userRole = await _loginData.GetUserRoleNameAsync(user.UserRoleId);
				// 1. Get all customers
				var customerResponse = await _externalApiService.GetCustomersAsync();
				var customers = customerResponse?.value ?? new List<Customer>();

				var filteredCustomers = new List<Customer>();
				if (userRole == "Admin" || userRole == "SalesCoordinator")
				{
					filteredCustomers = customers.ToList(); // Admin can see all customers
				}
				// 2. Filter customers by salesperson code
				else if (userRole == "SalesPerson")
				{
					filteredCustomers = customers
					.Where(c => c.salespersonCode == user.SalesPersonCode)
					.ToList();
				}

				if (!filteredCustomers.Any())
					return new CustomerInvoiceReturn { TotalDueAmount = 0, CustomerInvoices = new List<CustomerInvoice>() };

				// 3. Get all customer-wise invoices
				var customerWiseInvoicesList = await GetCustomerWiseInvoicesAsync();

				// 4. Filter to only customers the user has permission for
				var customerCodes = filteredCustomers.Select(c => c.no).ToHashSet();

				var customerInvoices = new List<CustomerInvoice>();

				foreach (var customerWise in customerWiseInvoicesList.Where(cwi => customerCodes.Contains(cwi.CustomerNo)))
				{
					var customerName = filteredCustomers.FirstOrDefault(c => c.no == customerWise.CustomerNo)?.name ?? "";

					foreach (var invoice in customerWise.Invoices)
					{
						DateTime? invoiceDate;
						if (!string.IsNullOrWhiteSpace(invoice.OrderNo))
						{
							if (int.TryParse(invoice.OrderNo, out int orderNumber))
							{
								var order = await _businessData.GetOrderByIdAsync(orderNumber);
								invoiceDate = order?.OrderDate ?? DateTime.Today; // fallback if order is null
							}
							else
							{
								// Invalid format, fallback date
								invoiceDate = DateTime.Today;
							}
						}
						else
						{
							invoiceDate = null;
						}
						customerInvoices.Add(new CustomerInvoice
						{
							CustomerCode = customerWise.CustomerNo,
							CustomerName = customerName,
							InvoiceDocumentNo = invoice.InvoiceNo,
							InvoiceDate = invoiceDate,
							InvoicedAmount = invoice.TotalAmount,
							DueAmount = invoice.DueAmount
						});
					}
				}

				var totalDue = customerInvoices.Sum(ci => ci.DueAmount ?? 0);

				_logger.LogInformation("Successfully retrieved customer invoice details for user {UserId}", userId);
				return new CustomerInvoiceReturn
				{
					TotalDueAmount = totalDue,
					CustomerInvoices = customerInvoices
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve invoices for user {UserId}", userId);
				throw new ApplicationException("Failed to retrieve invoices. Please try again later.", ex);
			}
		}
		/// <summary>
		/// Retrieves detailed invoice information for a specific customer, including order dates mapped from database records.
		/// </summary>
		/// <param name="customerCode">The unique identifier code for the customer whose invoices are to be retrieved</param>
		/// <returns>CustomerWiseInvoices object containing all invoice details for the specified customer, or null if no invoices are found</returns>
		public async Task<CustomerWiseInvoices> GetCustomerInvoiceDetailsAsync(string customerCode)
		{
			// Log the start of invoice retrieval process
			_logger.LogInformation("Starting invoice retrieval process for customer code: {CustomerCode}", customerCode);

			try
			{
				// Step 1: Retrieve all customer-wise invoices from external API
				var customerWiseInvoices = await GetCustomerWiseInvoicesAsync();

				// Validate that invoice data was successfully retrieved
				if (customerWiseInvoices == null || !customerWiseInvoices.Any())
				{
					_logger.LogWarning("No customer-wise invoices found in external API response");
					return null;
				}
				// Step 2: Convert customer code to string for comparison (defensive programming)
				var customerCodeStr = customerCode.ToString();

				// Step 3: Find invoices for the specific customer
				var customerInvoice = customerWiseInvoices
					.FirstOrDefault(c => c.CustomerNo == customerCodeStr);

				// Validate that customer was found in the invoice data
				if (customerInvoice == null)
				{
					_logger.LogWarning("No invoices found for customer code: {CustomerCode}. Customer may not have any invoices or code may be invalid", customerCode);
					return null;
				}

				_logger.LogInformation("Found {InvoiceCount} invoices for customer code: {CustomerCode}",
					customerInvoice.Invoices?.Count ?? 0, customerCode);

				// Step 4: Process each invoice to map order dates from database
				var processedInvoices = 0;
				var failedInvoices = 0;

				foreach (var invoice in customerInvoice.Invoices)
				{
					try
					{
						// Check if order number is available and valid
						if (!string.IsNullOrWhiteSpace(invoice.OrderNo))
						{
							// Attempt to parse order number to integer
							if (int.TryParse(invoice.OrderNo, out int orderNumber))
							{
								try
								{
									// Retrieve order details from database
									var order = await _businessData.GetOrderByIdAsync(orderNumber);

									if (order != null)
									{
										// Successfully retrieved order, use actual order date
										invoice.InvoiceDate = order.OrderDate;
									}
									else
									{
										// Order not found in database, use fallback date
										invoice.InvoiceDate = DateTime.Today;
										_logger.LogWarning("Order {OrderNumber} not found in database, using fallback date: {FallbackDate}",
											orderNumber, DateTime.Today);
										failedInvoices++;
									}
								}
								catch (Exception orderEx)
								{
									// Database error while retrieving order, use fallback date
									invoice.InvoiceDate = DateTime.Today;
									_logger.LogError(orderEx, "Database error while retrieving order {OrderNumber}, using fallback date: {FallbackDate}",
										orderNumber, DateTime.Today);
									failedInvoices++;
								}
							}
							else
							{
								// Invalid order number format, use fallback date
								invoice.InvoiceDate = DateTime.Today;
								_logger.LogWarning("Invalid order number format '{OrderNo}' for invoice, using fallback date: {FallbackDate}",
									invoice.OrderNo, DateTime.Today);
								failedInvoices++;
							}
						}
						else
						{
							// No order number available, set invoice date to null
							invoice.InvoiceDate = null;
						}

						processedInvoices++;
					}
					catch (Exception invoiceEx)
					{
						// Error processing individual invoice, log and continue
						_logger.LogError(invoiceEx, "Error processing invoice with order number: {OrderNo}", invoice.OrderNo ?? "N/A");
						failedInvoices++;
					}
				}

				_logger.LogInformation("Successfully retrieved and processed invoice summary for customer code: {CustomerCode}", customerCode);
				return customerInvoice;
			}
			catch (Exception ex)
			{
				// Log detailed error information
				_logger.LogError(ex, "Critical error occurred while retrieving customer-wise invoices for customer code: {CustomerCode}. Error: {ErrorMessage}",
					customerCode, ex.Message);

				// Throw user-friendly exception
				throw new ApplicationException($"Failed to retrieve customer-wise invoices for customer {customerCode}. Please try again later.", ex);
			}
		}

		/// <summary>
		/// Retrieves a summary of order counts grouped by status for a specific user.
		/// </summary>
		/// <param name="userId">The unique identifier of the user whose order status summary is to be retrieved</param>
		/// <returns>OrderStatusSummary object containing counts of pending, delivered, and rejected orders</returns>
		public async Task<OrderStatusSummary> GetOrderStatusSummaryByUserAsync(int userId)
		{
			// Log the start of order status summary retrieval
			_logger.LogInformation("Starting order status summary retrieval for user ID: {UserId}", userId);

			try
			{
				// Step 1: Validate user existence and retrieve user details
				var user = await _loginData.GetUserByIdAsync(userId);

				if (user == null)
				{
					_logger.LogWarning("User with ID {UserId} not found while retrieving order status summary", userId);
					throw new ApplicationException($"User with ID {userId} not found.");
				}
				string userRole = await _loginData.GetUserRoleNameAsync(user.UserRoleId);

				try
				{
					List<Order> pendingTask;
					List<Order> deliveredTask;
					List<Order> rejectedTask;

					// Step 3: Create tasks for parallel execution based on user role
					if (userRole == "Admin" || userRole == "SalesCoordinator")
					{
						pendingTask = await _businessData.GetAllOrdersAsync(OrderStatus.Pending);
						deliveredTask = await _businessData.GetAllOrdersAsync(OrderStatus.Delivered);
						rejectedTask = await _businessData.GetAllOrdersAsync(OrderStatus.Rejected);
					}
					else
					{
						pendingTask = await _businessData.GetListOfOrdersAsync(user.SalesPersonCode, OrderStatus.Pending);
						deliveredTask = await _businessData.GetListOfOrdersAsync(user.SalesPersonCode, OrderStatus.Delivered);
						rejectedTask = await _businessData.GetListOfOrdersAsync(user.SalesPersonCode, OrderStatus.Rejected);
					}

					// Retrieve results from completed tasks
					var pendingOrders =  pendingTask;
					var deliveredOrders =  deliveredTask;
					var rejectedOrders =  rejectedTask;

					// Calculate counts with null safety
					var pendingCount = pendingOrders?.Count ?? 0;
					var deliveredCount = deliveredOrders?.Count ?? 0;
					var rejectedCount = rejectedOrders?.Count ?? 0;

					// Create and return the summary object
					var summary = new OrderStatusSummary
					{
						PendingCount = pendingCount,
						DeliveredCount = deliveredCount,
						RejectedCount = rejectedCount
					};

					_logger.LogInformation("Successfully retrieved order status summary for user {UserId} ({UserRole}). Total orders: {TotalOrders}",
						userId, userRole, pendingCount + deliveredCount + rejectedCount);

					return summary;
				}
				catch (Exception dataEx)
				{
					// Error retrieving order data from business layer
					_logger.LogError(dataEx, "Error retrieving order data for user {UserId} with sales person code {SalesPersonCode}. Error: {ErrorMessage}",
						userId, user.SalesPersonCode, dataEx.Message);
					throw new ApplicationException($"Failed to retrieve order status data for user {userId}. Please try again later.", dataEx);
				}
			}
			catch (ApplicationException)
			{
				// Re-throw application exceptions as they are already user-friendly
				throw;
			}
			catch (Exception ex)
			{
				// Log unexpected errors
				_logger.LogError(ex, "Unexpected error occurred while retrieving order status summary for user {UserId}. Error: {ErrorMessage}",
					userId, ex.Message);

				// Throw user-friendly exception
				throw new ApplicationException($"Failed to retrieve order status summary for user {userId}. Please try again later.", ex);
			}
		}
		/// <summary>
		/// Validates customer credit status against order total
		/// </summary>
		/// <param name="customerCode">Customer code to validate</param>
		/// <param name="orderTotal">Total amount of the order</param>
		/// <returns>ServiceResult indicating validation success or failure</returns>
		private async Task<ServiceResult> ValidateCustomerCredit(string customerCode, decimal orderTotal)
		{
			try
			{
				// Get customer list from external API
				var customersResponse = await _externalApiService.GetCustomersAsync();

				// Validate response
				if (customersResponse?.value == null || !customersResponse.value.Any())
				{
					_logger.LogWarning("Customer data not available for validation");
					return new ServiceResult { Success = false, Message = "Customer data not available" };
				}

				// Find the specific customer using the provided code
				var customer = customersResponse.value.FirstOrDefault(c => c.no == customerCode);

				if (customer == null)
				{
					_logger.LogWarning("Customer {CustomerCode} not found for validation", customerCode);
					return new ServiceResult { Success = false, Message = $"Customer {customerCode} not found" };
				}

				// Check if credit is allowed for this customer
				if (!customer.creditAllowed)
				{
					_logger.LogWarning("Customer {CustomerCode} is not allowed credit purchases", customerCode);
					return new ServiceResult { Success = false, Message = "Customer is not allowed credit purchases" };
				}

				// Calculate available credit balance
				// Available credit = Credit Limit - Current Balance
				var availableCredit = customer.creditLimitLCY - customer.balanceLCY;

				// Check if the order total exceeds available credit
				if (orderTotal > availableCredit)
				{
					_logger.LogWarning("Order exceeds available credit for customer {CustomerCode}. " +
									 "Available: {AvailableCredit}, Order: {OrderTotal}",
									 customerCode, availableCredit, orderTotal);
					return new ServiceResult
					{
						Success = false,
						Message = $"Order exceeds available credit. Available credit: {availableCredit:C}, Order total: {orderTotal:C}"
					};
				}

				// Additional check: Ensure credit limit is positive (optional business rule)
				if (customer.creditLimitLCY <= 0)
				{
					_logger.LogWarning("Customer {CustomerCode} has no credit limit set", customerCode);
					return new ServiceResult { Success = false, Message = "Customer has no credit limit set" };
				}

				// All checks passed
				_logger.LogInformation("Credit validation passed for customer {CustomerCode}. " +
									 "Order: {OrderTotal}, Available Credit: {AvailableCredit}",
									 customerCode, orderTotal, availableCredit);
				return new ServiceResult { Success = true, Message = "Credit validation passed" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during credit validation for customer {CustomerCode}", customerCode);
				return new ServiceResult { Success = false, Message = "Error during credit validation" };
			}
		}

		/// <summary>
		/// Validates inventory availability for order items at specified location
		/// </summary>
		/// <param name="items">List of order items to validate</param>
		/// <param name="locationCode">Location code to check inventory at</param>
		/// <returns>ServiceResult indicating validation success or failure</returns>
		private async Task<ServiceResult> ValidateInventory(List<OrderItemRequest> items, string locationCode)
		{
			_logger.LogInformation("Entering ValidateInventory for location {LocationCode}", locationCode);
			try
			{
				// Get inventory data from external API
				var inventoryResponse = await _externalApiService.GetInventoryBalanceAsync();

				if (inventoryResponse?.value == null || !inventoryResponse.value.Any())
				{
					_logger.LogWarning("Inventory data not available for validation");
					return new ServiceResult { Success = false, Message = "Inventory data not available" };
				}

				// Filter inventory for specific location
				var filteredInventory = inventoryResponse.value
					.Where(i => i.locationCode == locationCode)
					.ToList();

				if (!filteredInventory.Any())
				{
					_logger.LogWarning("No inventory data found for location {LocationCode}", locationCode);
					return new ServiceResult
					{
						Success = false,
						Message = $"No inventory data found for location: {locationCode}"
					};
				}

				// Create lookup dictionary for inventory items
				var inventoryLookup = filteredInventory.ToDictionary(i => i.itemNo);

				// Validate each item in the order
				foreach (var item in items)
				{
					// Check item exists at location
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

					// Check sufficient quantity available
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

		/// <summary>
		/// Validates inventory availability for order items at specified location
		/// </summary>
		/// <param name="items">List of order items to validate</param>
		/// <param name="locationCode">Location code to check inventory at</param>
		/// <returns>ServiceResult indicating validation success or failure</returns>
		private async Task<bool> CheckInvoicedStatusAsync(Order order)
		{
			try
			{
				// Get all invoices for delivery status checking
				var invoiceResponse = await _externalApiService.GetInvoiceDetailsAsync();
				var invoices = invoiceResponse?.value ?? new List<Invoice>();

				if (!invoices.Any())
				{
					_logger.LogWarning("No invoices found for delivery status update");
					return false;
				}

				var isInvoiced = invoices.Any(inv =>
					!string.IsNullOrWhiteSpace(inv.OrderNo) &&
					int.TryParse(inv.OrderNo.Trim(), out var orderNo) &&
					orderNo == order.OrderNumber);

				if (!isInvoiced)
				{
					_logger.LogWarning("Order {OrderNumber} cannot be marked as Delivered because no invoice found.", order.OrderNumber);
					return false;
				}

				// Success path
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to verify invoiced status for order {OrderNumber}", order.OrderNumber);
				return false;
			}
		}

		private Task<bool> ValidateStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
		{
			// Same status - no change needed
			if (currentStatus == newStatus)
			{
				_logger.LogWarning("No status change: attempted to set status {Status} to the same value.", currentStatus);
				return Task.FromResult(false);
			}

			// Check if transition is valid
			if (!IsValidTransition(currentStatus, newStatus))
			{
				var allowedStatuses = GetAllowedNextStatuses(currentStatus);
				var allowedStatusNames = string.Join(", ", allowedStatuses.Select(s => s.ToString()));

				_logger.LogWarning("Invalid status transition attempted: {CurrentStatus} -> {NewStatus}. Allowed: {AllowedStatuses}",
					currentStatus, newStatus, allowedStatusNames);

				return Task.FromResult(false);
			}

			return Task.FromResult(true);
		}
		private bool IsValidTransition(OrderStatus currentStatus, OrderStatus newStatus)
		{
			var allowedStatuses = GetAllowedNextStatuses(currentStatus);
			return allowedStatuses.Contains(newStatus);
		}
		private List<OrderStatus> GetAllowedNextStatuses(OrderStatus currentStatus)
		{
			return currentStatus switch
			{
				OrderStatus.Pending => new List<OrderStatus>
			{
				OrderStatus.Processing,
				OrderStatus.Rejected
			},
				OrderStatus.Processing => new List<OrderStatus>
			{
				OrderStatus.Delivered,
				OrderStatus.Rejected
			},
				OrderStatus.Delivered => new List<OrderStatus>(), // Terminal status - no changes allowed
				OrderStatus.Rejected => new List<OrderStatus>(),   // Terminal status - no changes allowed
				_ => new List<OrderStatus>()
			};
		}
		


		private async Task<List<CustomerWiseInvoices>> GetCustomerWiseInvoicesAsync()
		{
			var postedInvoiceResponse = await _externalApiService.GetPostedInvoiceDetailsAsync();
			var invoices = postedInvoiceResponse.value;

			// Group by customer
			var customerGroups = invoices
				.GroupBy(inv => inv.CustomerNo)
				.Select(g =>
				{
					var invoiceSummaries = g.Select(inv =>
					{
						// Calculate total amount from all lines in this invoice
						decimal totalAmount = inv.Lines?.Sum(line => line.Amount) ?? 0;
						// Adjust these fields if your model uses different names for PDC/Due
						decimal pdcAmount = inv.PdcAmount; // Replace with actual property if needed
						decimal dueAmount = inv.RemainingAmount; // Replace with actual property if needed

						return new InvoiceSummary
						{
							InvoiceNo = inv.DocumentNo, // Replace with actual invoice number property
							OrderNo = inv.OrderNo, // Replace with actual order number property
							PdcAmount = pdcAmount,
							DueAmount = dueAmount,
							TotalAmount = totalAmount
						};
					}).ToList();

					return new CustomerWiseInvoices
					{
						CustomerNo = g.Key,
						TotalDueAmount = invoiceSummaries.Sum(i => i.DueAmount),
						TotalPdcAmount = invoiceSummaries.Sum(i => i.PdcAmount),
						Invoices = invoiceSummaries
					};
				})
				.ToList();

			return customerGroups;
		}

	}
}