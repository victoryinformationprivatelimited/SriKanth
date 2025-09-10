using MailKit.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CSharp.RuntimeBinder;
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
			
			try
			{
				_logger.LogInformation("Beginning to retrieve sales stock details");

				// Execute all API calls in parallel
				var inventoryTask = _externalApiService.GetInventoryBalanceAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPricesTask = _externalApiService.GetSalesPriceAsync();
				var locationsTask = _externalApiService.GetLocationsAsync();

				await Task.WhenAll(inventoryTask, itemsTask, salesPricesTask, locationsTask);

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
				// Codes to skip
				var codesToSkip = new HashSet<string> { "SEDAW-SNS", "SEDAW-SKM" };

				// Create lookup dictionaries
				var inventoryLookup = inventory.value
					.Where(i => i?.itemNo != null && i?.locationCode != null)
					.ToDictionary(i => (i.itemNo, i.locationCode), i => i);

				var priceLookup = salesPrices.value
					.Where(p => p?.itemNo != null)
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice);

				// Generate stock items using LINQ (much faster than nested loops)
				var stockList = (from item in items.value.Where(i => i?.no != null)
								 from location in locations.value.Where(l => l?.code != null && !codesToSkip.Contains(l.code))
								 let key = (item.no, location.code)
								 where inventoryLookup.ContainsKey(key)
								 let inv = inventoryLookup[key]
								 select new StockItem
								 {
									 ItemCode = item.no,
									 ItemName = item.description ?? string.Empty,
									 Location = location.name ?? location.code ?? "Unknown",
									 Stock = item.reorderPoint == 0 ? inv.inventory.ToString() : inv.inventory > item.reorderPoint ? $"{item.reorderPoint}+": inv.inventory.ToString(),
									 UnitPrice = priceLookup.GetValueOrDefault(item.no),
									 ItemCategory = item.itemCategoryCode ?? string.Empty,
									 Category = item.parentCategoryCode ?? string.Empty,
									 SubCategory = item.childCategoryCode ?? string.Empty,
									 Description = item.description ?? string.Empty,
									 Description2 = item.description2 ?? string.Empty,
									 UnitOfMeasure = item.unitOfMeasure ?? string.Empty,
									 Size = item.size ?? string.Empty,
									 ReorderQuantity = item.reorderQuantity,
									 Image = null // Load separately if needed
								 }).ToList();

				_logger.LogInformation("Successfully retrieved {StockItemCount} stock items", stockList.Count);
				return stockList;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve stock details");
				throw new ApplicationException("Failed to retrieve stock details. Please try again later.", ex);
			}
		}

		public async Task<string> GetImageByItemNo(string itemNo)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(itemNo))
				{
					return null;
				}

				var items = await _externalApiService.GetItemsWithSubstitutionsAsync();
				var item = items?.value?.FirstOrDefault(i => i.no == itemNo);

				if (item == null || item.systemId == Guid.Empty)
				{
					return null; // Item not found
				}

				var image = await _externalApiService.GetItemsPictureAsync(item.systemId);

				return image; // Will be null or empty if not available
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to get image for item {ItemNo}", itemNo);
				return null;
			}
		}

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
				var inventoryTask = _externalApiService.GetInventoryBalanceAsync();

				await Task.WhenAll(locationsTask, customersTask, itemsTask, salesPriceTask, inventoryTask);

				// Get results
				var locations = await locationsTask;
				var customers = await customersTask;
				var items = await itemsTask;
				var salesPrices = await salesPriceTask;
				var inventory = await inventoryTask;
				// Validate data existence early (type-safe)
				ValidateApiData(locations?.value, "location");
				ValidateApiData(customers?.value, "customer");
				ValidateApiData(items?.value, "item");

				// Create price lookup dictionary with better performance
				var priceLookup = CreatePriceLookupDictionary(salesPrices?.value);
				var customerWiseInvoices = await GetCustomerWiseInvoicesAsync();
				var dueAmountLookup = customerWiseInvoices.ToDictionary(
					cwi => cwi.CustomerNo,
					cwi => cwi.TotalDueAmount
				);
				// Process data in parallel using PLINQ for large datasets
				var locationDtos = ProcessLocationsInParallel(locations.value);
				var customerDtos = ProcessCustomersInParallel(customers.value,dueAmountLookup);
				var itemDtos = ProcessItemsInParallel(items.value, priceLookup,inventory.value);

				var details = new OrderCreationDetails
				{
					Locations = locationDtos,
					Customers = customerDtos,
					Items = itemDtos
				};

				_logger.LogInformation("Retrieved order creation details with {LocationCount} locations, {CustomerCount} customers, and {ItemCount} items",
					details.Locations.Count, details.Customers.Count, details.Items.Count);

				return details;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve order creation details");
				throw new InvalidOperationException("Error while retrieving order creation details", ex);
			}
		}

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

				// Get results
				var locations = await locationsTask;
				var customers = await customersTask;
				var items = await itemsTask;
				var salesPrices = await salesPriceTask;
				var inventory = await inventoryTask;

				// Validate data existence early (type-safe)
				ValidateApiData(locations?.value, "location");
				ValidateApiData(customers?.value, "customer");
				ValidateApiData(items?.value, "item");

				// Filter locations to only those assigned to the user
				var filteredLocations = locations.value
					.Where(l => userLocations.Contains(l.code))
					.ToList();

				if (!filteredLocations.Any())
				{
					_logger.LogWarning("No location data found for user {UserId}", userId);
					throw new InvalidOperationException("No valid location data found for user.");
				}

				// Filter customers by salesperson code
				var filteredCustomers = customers.value
					.Where(c => c.salespersonCode == user.SalesPersonCode)
					.ToList();

				// Filter inventory to only user's locations with stock
				var inventoryAtLocations = inventory.value
					.Where(i => userLocations.Contains(i.locationCode) && i.inventory > 0)
					.ToList();

				// Get item numbers available at these locations
				var availableItemNos = new HashSet<string>(inventoryAtLocations.Select(i => i.itemNo));

				// Create price lookup dictionary
				var priceLookup = CreatePriceLookupDictionary(salesPrices?.value);
				var customerWiseInvoices = await GetCustomerWiseInvoicesAsync();
				var dueAmountLookup = customerWiseInvoices.ToDictionary(
					cwi => cwi.CustomerNo,
					cwi => cwi.TotalDueAmount
				);
				// Process data in parallel using PLINQ for large datasets
				var locationDtos = ProcessLocationsInParallel(filteredLocations);
				var customerDtos = ProcessCustomersInParallel(filteredCustomers,dueAmountLookup);
				var itemDtos = ProcessItemsInParallel(items.value.Where(i => availableItemNos.Contains(i.no)), priceLookup,inventoryAtLocations);

				var details = new OrderCreationDetails
				{
					Locations = locationDtos,
					Customers = customerDtos,
					Items = itemDtos
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

		public async Task<List<Location>> GetFilteredLocationsAsync(int userId)
		{
			_logger.LogInformation("Retrieving filtered locations for user {UserId}", userId);

			var user = await _loginData.GetUserByIdAsync(userId)
				?? throw new InvalidOperationException("User Not found");

			// Get user role to determine filtering logic
			string userRole = await _loginData.GetUserRoleNameAsync(user.UserRoleId);

			// Get all locations from external API
			var locationsResponse = await _externalApiService.GetLocationsAsync();
			ValidateApiData(locationsResponse?.value, "location");

			List<LocationDetail> filteredLocationDetails;

			// Admin can see all locations
			if (userRole == "Admin")
			{
				_logger.LogInformation("Admin user {UserId} accessing all locations", userId);
				filteredLocationDetails = locationsResponse.value.ToList();
			}
			else
			{
				// Non-admin users see only their assigned locations
				var userLocations = await _loginData.GetUserLocationCodesAsync(userId);
				if (userLocations == null || !userLocations.Any())
					throw new InvalidOperationException("User has no locations assigned.");

				filteredLocationDetails = locationsResponse.value
					.Where(l => userLocations.Contains(l.code))
					.ToList();

				if (!filteredLocationDetails.Any())
					throw new InvalidOperationException("No valid location data found for user.");
			}

			// Process and convert LocationDetail to Location
			return ProcessLocationsInParallel(filteredLocationDetails);
		}

		public async Task<List<OrderCustomer>> GetFilteredCustomersAsync(int userId)
		{
			_logger.LogInformation("Retrieving filtered customers for user {UserId}", userId);

			var user = await _loginData.GetUserByIdAsync(userId)
				?? throw new InvalidOperationException("User Not found");

			string userRole = await _loginData.GetUserRoleNameAsync(user.UserRoleId);

			var customersResponse = await _externalApiService.GetCustomersAsync();
			ValidateApiData(customersResponse?.value, "customer");

			List<Customer> filteredCustomers;
			if (userRole == "Admin")
			{
				_logger.LogInformation("Admin user {UserId} accessing all customers", userId);
				filteredCustomers = customersResponse.value.ToList();
			}
			else
			{
				filteredCustomers = customersResponse.value
					.Where(c => c.salespersonCode == user.SalesPersonCode)
					.ToList();
			}

			// Fetch due amounts
			var customerWiseInvoices = await GetCustomerWiseInvoicesAsync();
			var dueAmountLookup = customerWiseInvoices.ToDictionary(
				cwi => cwi.CustomerNo,
				cwi => cwi.TotalDueAmount
			);

			return ProcessCustomersInParallel(filteredCustomers, dueAmountLookup);
		}

		public async Task<List<OrderItemDetails>> GetFilteredItemsAsync(int userId)
		{
			_logger.LogInformation("Retrieving filtered items for user {UserId}", userId);

			var user = await _loginData.GetUserByIdAsync(userId)
				?? throw new InvalidOperationException("User Not found");

			string userRole = await _loginData.GetUserRoleNameAsync(user.UserRoleId);

			var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
			var salesPriceTask = _externalApiService.GetSalesPriceAsync();
			var inventoryTask = _externalApiService.GetInventoryBalanceAsync();

			await Task.WhenAll(itemsTask, salesPriceTask, inventoryTask);

			var items = (await itemsTask)?.value;
			var salesPrices = (await salesPriceTask)?.value;
			var inventory = (await inventoryTask)?.value;

			ValidateApiData(items, "item");

			List<Item> filteredItems;
			if (userRole == "Admin")
			{
				_logger.LogInformation("Admin user {UserId} accessing all items", userId);
				filteredItems = items.ToList();
			}
			else
			{
				var userLocations = await _loginData.GetUserLocationCodesAsync(userId);
				if (userLocations == null || !userLocations.Any())
					throw new InvalidOperationException("User has no locations assigned.");

				var inventoryAtLocations = inventory
					.Where(i => userLocations.Contains(i.locationCode) && i.inventory > 0)
					.ToList();

				var availableItemNos = new HashSet<string>(inventoryAtLocations.Select(i => i.itemNo));
				filteredItems = items.Where(i => availableItemNos.Contains(i.no)).ToList();
				// Optionally, you can also filter inventory to only user locations if needed
				inventory = inventoryAtLocations;
			}

			var priceLookup = CreatePriceLookupDictionary(salesPrices);

			// Pass inventory to ProcessItemsInParallel
			return ProcessItemsInParallel(filteredItems, priceLookup, inventory);
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

				var customerTask = _externalApiService.GetCustomersAsync();
				var invoiceTask = _externalApiService.GetPostedInvoiceDetailsAsync();

				await Task.WhenAll(customerTask, invoiceTask);

				var customerResponse = await customerTask;
				var invoiceResponse = await invoiceTask;

				var customers = customerResponse?.value ?? new List<Customer>();
				var invoices = invoiceResponse?.value ?? new List<PostedInvoice>();

				// OPTIMIZATION 2: Filter customers early and create lookup dictionary
				var filteredCustomers = FilterCustomersByRole(customers, userRole, user.SalesPersonCode);

				if (!filteredCustomers.Any())
					return new CustomerInvoiceReturn { TotalDueAmount = 0, CustomerInvoices = new List<CustomerInvoice>() };

				// Create customer lookup dictionary for O(1) access
				var customerLookup = filteredCustomers.ToDictionary(c => c.no, c => c.name);
				var allowedCustomerCodes = customerLookup.Keys.ToHashSet();

				// OPTIMIZATION 3: Filter invoices early and process in memory
				var relevantInvoices = invoices
					.Where(inv => allowedCustomerCodes.Contains(inv.CustomerNo))
					.ToList();

				// OPTIMIZATION 4: Batch database queries for orders
				var orderNumbers = relevantInvoices
					.Where(inv => !string.IsNullOrWhiteSpace(inv.OrderNo) && int.TryParse(inv.OrderNo, out _))
					.Select(inv => int.Parse(inv.OrderNo))
					.Distinct()
					.ToList();

				// Get all orders in one query instead of individual queries
				var orderLookup = await GetOrderLookupAsync(orderNumbers);

				// OPTIMIZATION 5: Process invoices efficiently
				var customerInvoices = ProcessInvoicesEfficiently(relevantInvoices, customerLookup, orderLookup);

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
			_logger.LogInformation("Starting invoice retrieval process for customer code: {CustomerCode}", customerCode);

			try
			{
				//Retrieve all customer-wise invoices from external API
				var customerWiseInvoices = await GetCustomerWiseInvoicesAsync();

				// Validate that invoice data was successfully retrieved
				if (customerWiseInvoices == null || !customerWiseInvoices.Any())
				{
					_logger.LogWarning("No customer-wise invoices found in external API response");
					return null;
				}

				var customerCodeStr = customerCode.ToString();

				// Find invoices for the specific customer
				var customerInvoice = customerWiseInvoices
					.FirstOrDefault(c => c.CustomerNo == customerCodeStr);

				if (customerInvoice == null)
				{
					_logger.LogWarning("No invoices found for customer code: {CustomerCode}. Customer may not have any invoices or code may be invalid", customerCode);
					return null;
				}

				// Filter out invoices with DueAmount == 0
				var filteredInvoices = customerInvoice.Invoices
					.Where(inv => inv.DueAmount > 0)
					.ToList();

				// Process each invoice to map order dates from database
				foreach (var invoice in filteredInvoices)
				{
					try
					{
						if (!string.IsNullOrWhiteSpace(invoice.OrderNo))
						{
							if (int.TryParse(invoice.OrderNo, out int orderNumber))
							{
								try
								{
									var order = await _businessData.GetOrderByIdAsync(orderNumber);
									invoice.InvoiceDate = order != null ? order.OrderDate : DateTime.Today;
								}
								catch
								{
									invoice.InvoiceDate = DateTime.Today;
								}
							}
							else
							{
								invoice.InvoiceDate = DateTime.Today;
							}
						}
						else
						{
							invoice.InvoiceDate = null;
						}
					}
					catch
					{
						invoice.InvoiceDate = null;
					}
				}

				// Recalculate totals based on filtered invoices
				customerInvoice.Invoices = filteredInvoices;
				customerInvoice.TotalDueAmount = filteredInvoices.Sum(i => i.DueAmount);
				customerInvoice.TotalPdcAmount = filteredInvoices.Sum(i => i.PdcAmount);

				_logger.LogInformation("Successfully retrieved and processed invoice summary for customer code: {CustomerCode}", customerCode);
				return customerInvoice;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Critical error occurred while retrieving customer-wise invoices for customer code: {CustomerCode}. Error: {ErrorMessage}",
					customerCode, ex.Message);

				throw new ApplicationException($"Failed to retrieve customer-wise invoices for customer {customerCode}. Please try again later.", ex);
			}
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
						decimal originalAmount = inv.Amount;
						decimal releasedPDCs = inv.PdcAmount;
						decimal balanceAfterPDCs = inv.RemainingAmount;
						decimal balanceBeforePDCs = balanceAfterPDCs + releasedPDCs;

						return new InvoiceSummary
						{
							InvoiceNo = inv.DocumentNo,
							OrderNo = inv.OrderNo,
							PdcAmount = releasedPDCs,
							DueAmount = balanceAfterPDCs,
							TotalAmount = originalAmount,
							OriginalAmount = originalAmount,
							BalanceBeforePDCs = balanceBeforePDCs,
							ReleasedPDCs = releasedPDCs,
							BalanceAfterPDCs = balanceAfterPDCs
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
		private List<CustomerWiseInvoices> GetCustomerWiseInvoicesOptimized(List<PostedInvoice> invoices, HashSet<string> allowedCustomerCodes)
		{
			return invoices
				.Where(inv => allowedCustomerCodes.Contains(inv.CustomerNo))
				.GroupBy(inv => inv.CustomerNo)
				.AsParallel() // Use parallel processing for large datasets
				.Select(g =>
				{
					var invoiceSummaries = g.Select(inv => new InvoiceSummary
					{
						InvoiceNo = inv.DocumentNo,
						OrderNo = inv.OrderNo,
						PdcAmount = inv.PdcAmount,
						DueAmount = inv.RemainingAmount,
						TotalAmount = inv.Amount
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
		}
		private List<Customer> FilterCustomersByRole(List<Customer> customers, string userRole, string salesPersonCode)
		{
			return userRole switch
			{
				"Admin" or "SalesCoordinator" => customers,
				"SalesPerson" => customers.Where(c => c.salespersonCode == salesPersonCode).ToList(),
				_ => new List<Customer>()
			};
		}

		private async Task<Dictionary<int, DateTime?>> GetOrderLookupAsync(List<int> orderNumbers)
		{
			if (!orderNumbers.Any())
				return new Dictionary<int, DateTime?>();

			// Assuming you have a method to get multiple orders at once
			// If not, you'll need to create one in your business data layer
			var orders = await _businessData.GetListOfOrdersByOrderNumbersAsync(orderNumbers);
			return orders.ToDictionary(o => o.OrderNumber, o => (DateTime?)o.OrderDate);
		}

		private List<CustomerInvoice> ProcessInvoicesEfficiently(List<PostedInvoice> invoices,Dictionary<string, string> customerLookup,Dictionary<int, DateTime?> orderLookup)
		{
			var customerInvoices = new List<CustomerInvoice>();

			foreach (var invoice in invoices)
			{
				var customerName = customerLookup.GetValueOrDefault(invoice.CustomerNo, "");

				DateTime? invoiceDate = null;
				if (!string.IsNullOrWhiteSpace(invoice.OrderNo) && int.TryParse(invoice.OrderNo, out int orderNumber))
				{
					invoiceDate = orderLookup.GetValueOrDefault(orderNumber, DateTime.Today);
				}

				customerInvoices.Add(new CustomerInvoice
				{
					CustomerCode = invoice.CustomerNo,
					CustomerName = customerName,
					InvoiceDocumentNo = invoice.DocumentNo,
					InvoiceDate = invoiceDate,
					InvoicedAmount = invoice.Amount,
					DueAmount = invoice.RemainingAmount
				});
			}

			return customerInvoices;
		}

		private void ValidateApiData<T>(IEnumerable<T> data, string dataType)
		{
			if (data == null || !data.Any())
			{
				_logger.LogWarning("{DataType} data not available", dataType);
				throw new InvalidOperationException($"No {dataType} data found.");
			}
		}

		private Dictionary<string, decimal> CreatePriceLookupDictionary(IEnumerable<dynamic> salesPrices)
		{
			if (salesPrices == null) return new Dictionary<string, decimal>();

			// Use concurrent dictionary for thread-safe operations if needed
			// and pre-size the dictionary if you know approximate count
			var priceLookup = new Dictionary<string, decimal>();

			// Process in batches for very large datasets
			const int batchSize = 10000;
			var batches = salesPrices.Batch(batchSize);

			foreach (var batch in batches)
			{
				var batchLookup = batch
					.GroupBy(p => p.itemNo)
					.ToDictionary(g => g.Key, g => g.First().unitPrice);

				foreach (var kvp in batchLookup)
				{
					priceLookup[kvp.Key] = kvp.Value;
				}
			}

			return priceLookup;
		}

		private List<Model.Login_Module.DTOs.Location> ProcessLocationsInParallel(IEnumerable<dynamic> locations)
		{
			// Define the codes to skip
			var codesToSkip = new HashSet<string> { "SEDAW-SNS", "SEDAW-SKM" };

			// Use PLINQ for parallel processing with filtering
			return locations.AsParallel()
				.WithDegreeOfParallelism(Environment.ProcessorCount)
				.Where(l =>
				{
					try
					{
						string code = l.code; // Access the dynamic property
						return !codesToSkip.Contains(code); // Skip if in the exclusion list
					}
					catch (RuntimeBinderException)
					{
						// Handle cases where 'code' property doesn't exist
						return false; // Skip invalid entries
					}
				})
				.Select(l => new Model.Login_Module.DTOs.Location
				{
					LocationCode = l.code,
					LocationName = l.name
				})
				.ToList();
		}

		private List<OrderCustomer> ProcessCustomersInParallel(IEnumerable<dynamic> customers, Dictionary<string, decimal> dueAmountLookup)
		{
			return customers.AsParallel()
				.WithDegreeOfParallelism(Environment.ProcessorCount)
				.Select(c => new OrderCustomer
				{
					CustomerCode = c.no,
					CustomerName = c.name,
					DueAmount = dueAmountLookup.TryGetValue(c.no, out decimal due) ? due : 0,
					CreditAllowed = c.creditAllowed,
					CreditLimit = c.creditLimitLCY,
					BalanceCredit = c.balanceLCY,
					PaymentTermCode = c.paymentTermsCode,
					PaymentMethodCode = c.paymentMethodCode
				})
				.ToList();
		}

		private List<OrderItemDetails> ProcessItemsInParallel(IEnumerable<dynamic> items,Dictionary<string, decimal> priceLookup,IEnumerable<dynamic> inventory) // Pass inventory data here)
		{
			// Group inventory by itemNo for quick lookup
			var inventoryLookup = inventory
				.GroupBy(inv => inv.itemNo)
				.ToDictionary(g => g.Key, g => g.ToList());

			return items.AsParallel()
				.WithDegreeOfParallelism(Environment.ProcessorCount)
				.Select(i =>
				{
					// Build location-wise inventory for this item
					var locationInventories = inventoryLookup.TryGetValue(i.no, out List<dynamic> invList)
						? invList.Select(inv => new LocationByItemInventory
						{
							LocationCode = inv.locationCode,
							Inventory = inv.inventory
						}).ToList()
						: new List<LocationByItemInventory>();

					return new OrderItemDetails
					{
						ItemCode = i.no,
						ItemName = i.description,
						Unitprice = priceLookup.TryGetValue(i.no, out decimal price) ? price.ToString() : "0",
						SubstituteItems = CreateSubstituteItem(i.itemsubstitutions, priceLookup),
						LocationWiseInventory = locationInventories
					};
				})
				.ToList();
		}

		private SubstituteItem CreateSubstituteItem(IEnumerable<ItemSubstitution> itemSubstitutions, Dictionary<string, decimal> priceLookup)
		{
			var firstSubstitution = itemSubstitutions?.FirstOrDefault();
			if (firstSubstitution == null) return null;

			return new SubstituteItem
			{
				ItemCode = firstSubstitution.substituteNo,
				ItemName = firstSubstitution.description,
				UnitPrice = priceLookup.TryGetValue(firstSubstitution.substituteNo, out decimal subPrice) ? subPrice : 0
			};
		}

	}
}