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
using System.Collections.Concurrent;
using System.Diagnostics;

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
				var sw = Stopwatch.StartNew();

				// Execute all API calls in parallel
				var inventoryTask = _externalApiService.GetInventoryBalanceAsync();
				var itemsTask = _externalApiService.GetItemsWithSubstitutionsAsync();
				var salesPricesTask = _externalApiService.GetSalesPriceAsync();
				var locationsTask = _externalApiService.GetLocationsAsync();

				await Task.WhenAll(inventoryTask, itemsTask, salesPricesTask, locationsTask);

				_logger.LogInformation("API calls completed in {Ms}ms", sw.ElapsedMilliseconds);

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

				sw.Restart();
				var stockList = BuildStockList(inventory, items, salesPrices, locations);

				_logger.LogInformation("Successfully built {StockItemCount} stock items in {Ms}ms",
					stockList.Count, sw.ElapsedMilliseconds);

				return stockList;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve stock details");
				throw new ApplicationException("Failed to retrieve stock details. Please try again later.", ex);
			}
		}

		private List<StockItem> BuildStockList(dynamic inventory, dynamic items, dynamic salesPrices, dynamic locations)
		{
			var sw = Stopwatch.StartNew();

			// Codes to skip - case insensitive for better matching
			var codesToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SED-SNS", "SED-SKM" };

			// Pre-filter and materialize valid locations
			var validLocations = new List<(string Code, string Name)>();
			foreach (var loc in locations.value)
			{
				if (loc?.code != null && !codesToSkip.Contains(loc.code))
				{
					validLocations.Add((loc.code, loc.name ?? loc.code ?? "Unknown"));
				}
			}

			_logger.LogInformation("Filtered to {Count} valid locations in {Ms}ms",
				validLocations.Count, sw.ElapsedMilliseconds);
			sw.Restart();

			// Build inventory lookup with proper capacity
			var inventoryArray = inventory.value.ToArray();
			var inventoryLookup = new Dictionary<(string, string), dynamic>(inventoryArray.Length);

			foreach (var inv in inventoryArray)
			{
				if (inv?.itemNo != null && inv?.locationCode != null)
				{
					inventoryLookup[(inv.itemNo, inv.locationCode)] = inv;
				}
			}

			_logger.LogInformation("Built inventory lookup ({Count} items) in {Ms}ms",
				inventoryLookup.Count, sw.ElapsedMilliseconds);
			sw.Restart();

			// Build price lookup - optimized with early exit
			var pricesArray = salesPrices.value.ToArray();
			var priceLookup = new Dictionary<string, decimal>(pricesArray.Length);

			foreach (var price in pricesArray)
			{
				if (price?.itemNo != null && !priceLookup.ContainsKey(price.itemNo))
				{
					priceLookup[price.itemNo] = price.unitPrice;
				}
			}

			_logger.LogInformation("Built price lookup ({Count} items) in {Ms}ms",
				priceLookup.Count, sw.ElapsedMilliseconds);
			sw.Restart();

			// Pre-filter valid items
			var validItems = new List<dynamic>();
			foreach (var item in items.value)
			{
				if (item?.no != null)
				{
					validItems.Add(item);
				}
			}

			// Estimate capacity for stock list
			int estimatedSize = Math.Min(validItems.Count * validLocations.Count, 100000);
			var stockList = new List<StockItem>(estimatedSize);

			// Decide processing strategy based on dataset size
			int totalCombinations = validItems.Count * validLocations.Count;

			if (totalCombinations > 20000)
			{
				// Parallel processing for large datasets
				_logger.LogInformation("Using parallel processing for {Count} combinations", totalCombinations);
				stockList = BuildStockListParallel(validItems, validLocations, inventoryLookup, priceLookup);
			}
			else
			{
				// Sequential processing for smaller datasets (less overhead)
				_logger.LogInformation("Using sequential processing for {Count} combinations", totalCombinations);
				BuildStockListSequential(validItems, validLocations, inventoryLookup, priceLookup, stockList);
			}

			_logger.LogInformation("Built stock list ({Count} items) in {Ms}ms",
				stockList.Count, sw.ElapsedMilliseconds);

			return stockList;
		}

		// Sequential processing (optimized for smaller datasets)
		private void BuildStockListSequential(
			List<dynamic> items,
			List<(string Code, string Name)> locations,
			Dictionary<(string, string), dynamic> inventoryLookup,
			Dictionary<string, decimal> priceLookup,
			List<StockItem> stockList)
		{
			foreach (var item in items)
			{
				string itemNo = item.no;
				string itemName = item.description ?? string.Empty;
				string itemCategory = item.itemCategoryCode ?? string.Empty;
				string category = item.parentCategoryCode ?? string.Empty;
				string subCategory = item.childCategoryCode ?? string.Empty;
				string description = item.description ?? string.Empty;
				string description2 = item.description2 ?? string.Empty;
				string unitOfMeasure = item.unitOfMeasure ?? string.Empty;
				string size = item.size ?? string.Empty;
				decimal reorderPoint = item.reorderPoint;
				decimal reorderQuantity = item.reorderQuantity;
				decimal unitPrice = priceLookup.GetValueOrDefault(itemNo, 0);

				foreach (var location in locations)
				{
					var key = (itemNo, location.Code);

					if (inventoryLookup.TryGetValue(key, out var inv))
					{
						decimal inventoryQty = inv.inventory;

						string stock;
						if (reorderPoint == 0)
						{
							stock = inventoryQty.ToString();
						}
						else
						{
							stock = inventoryQty > reorderPoint
								? $"{reorderPoint}+"
								: inventoryQty.ToString();
						}

						stockList.Add(new StockItem
						{
							ItemCode = itemNo,
							ItemName = itemName,
							Location = location.Name,
							Stock = stock,
							UnitPrice = unitPrice,
							ItemCategory = itemCategory,
							Category = category,
							SubCategory = subCategory,
							Description = description,
							Description2 = description2,
							UnitOfMeasure = unitOfMeasure,
							Size = size,
							ReorderQuantity = reorderQuantity,
							Image = null
						});
					}
				}
			}
		}

		// Parallel processing (optimized for larger datasets)
		private List<StockItem> BuildStockListParallel(
			List<dynamic> items,
			List<(string Code, string Name)> locations,
			Dictionary<(string, string), dynamic> inventoryLookup,
			Dictionary<string, decimal> priceLookup)
		{
			var partitions = Partitioner.Create(0, items.Count);
			var localLists = new ConcurrentBag<List<StockItem>>();

			Parallel.ForEach(partitions, new ParallelOptions
			{
				MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8)
			}, range =>
			{
				var localList = new List<StockItem>(1000);

				for (int i = range.Item1; i < range.Item2; i++)
				{
					var item = items[i];

					string itemNo = item.no;
					string itemName = item.description ?? string.Empty;
					string itemCategory = item.itemCategoryCode ?? string.Empty;
					string category = item.parentCategoryCode ?? string.Empty;
					string subCategory = item.childCategoryCode ?? string.Empty;
					string description = item.description ?? string.Empty;
					string description2 = item.description2 ?? string.Empty;
					string unitOfMeasure = item.unitOfMeasure ?? string.Empty;
					string size = item.size ?? string.Empty;
					decimal reorderPoint = item.reorderPoint;
					decimal reorderQuantity = item.reorderQuantity;
					decimal unitPrice = priceLookup.GetValueOrDefault(itemNo, 0);

					foreach (var location in locations)
					{
						var key = (itemNo, location.Code);

						if (inventoryLookup.TryGetValue(key, out var inv))
						{
							decimal inventoryQty = inv.inventory;

							string stock;
							if (reorderPoint == 0)
							{
								stock = inventoryQty.ToString();
							}
							else
							{
								stock = inventoryQty > reorderPoint
									? $"{reorderPoint}+"
									: inventoryQty.ToString();
							}

							localList.Add(new StockItem
							{
								ItemCode = itemNo,
								ItemName = itemName,
								Location = location.Name,
								Stock = stock,
								UnitPrice = unitPrice,
								ItemCategory = itemCategory,
								Category = category,
								SubCategory = subCategory,
								Description = description,
								Description2 = description2,
								UnitOfMeasure = unitOfMeasure,
								Size = size,
								ReorderQuantity = reorderQuantity,
								Image = null
							});
						}
					}
				}

				localLists.Add(localList);
			});

			// Merge results efficiently
			var totalCount = localLists.Sum(list => list.Count);
			var result = new List<StockItem>(totalCount);

			foreach (var localList in localLists)
			{
				result.AddRange(localList);
			}

			return result;
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

			// Parallel fetch of user and customers
			var userTask = _loginData.GetUserByIdAsync(userId);
			var customersTask = _externalApiService.GetCustomersAsync();

			await Task.WhenAll(userTask, customersTask);

			var user = await userTask
				?? throw new InvalidOperationException("User Not found");
			var customersResponse = await customersTask;

			ValidateApiData(customersResponse?.value, "customer");

			// Get role and filter customers
			string userRole = await _loginData.GetUserRoleNameAsync(user.UserRoleId);

			IEnumerable<Customer> filteredCustomers;
			if (userRole == "Admin")
			{
				_logger.LogInformation("Admin user {UserId} accessing all customers", userId);
				filteredCustomers = customersResponse.value;
			}
			else
			{
				filteredCustomers = customersResponse.value
					.Where(c => c.salespersonCode == user.SalesPersonCode);
			}

			// Fetch due amounts and build lookup in one pass
			var dueAmountLookup = await GetCustomerDueAmountsAsync();

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
				var invoices = invoiceResponse?.Value ?? new List<PostedInvoice>();

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
				// Retrieve all customer-wise invoices from external API
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
					.Where(inv => inv.BalanceBeforePDCs > 0)
					.ToList();

				// No need to set or process order date

				// Recalculate totals based on filtered invoices
				customerInvoice.Invoices = filteredInvoices;
				customerInvoice.TotalDueAmount = filteredInvoices.Sum(i => i.BalanceBeforePDCs);
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

		private async Task<Dictionary<string, decimal>> GetCustomerDueAmountsAsync()
		{
			var postedInvoiceResponse = await _externalApiService.GetPostedInvoiceDetailsAsync();
			var invoices = postedInvoiceResponse.Value;

			// Build dictionary directly during grouping - single pass
			return invoices
				.GroupBy(inv => inv.CustomerNo)
				.ToDictionary(
					g => g.Key,
					g => g.Sum(inv => inv.RemainingAmount) // TotalDueAmount = sum of RemainingAmount
				);
		}

		private async Task<List<CustomerWiseInvoices>> GetCustomerWiseInvoicesAsync()
		{
			var postedInvoiceResponse = await _externalApiService.GetPostedInvoiceDetailsAsync();
			var invoices = postedInvoiceResponse.Value;

			// Single enumeration with immediate materialization
			var customerGroups = new List<CustomerWiseInvoices>(invoices.Count() / 10); // rough estimate

			foreach (var group in invoices.GroupBy(inv => inv.CustomerNo))
			{
				var invoiceSummaries = new List<InvoiceSummary>(group.Count());
				decimal totalDue = 0;
				decimal totalPdc = 0;

				foreach (var inv in group)
				{
					decimal balanceBeforePDCs = inv.RemainingAmount;
					decimal releasedPDCs = inv.PdcAmount;
					decimal balanceAfterPDCs = balanceBeforePDCs - releasedPDCs;

					totalDue += balanceBeforePDCs;
					totalPdc += releasedPDCs;

					invoiceSummaries.Add(new InvoiceSummary
					{
						InvoiceNo = inv.DocumentNo,
						OrderNo = inv.OrderNo,
						PostedDate = inv.PostingDate,
						PdcAmount = releasedPDCs,
						DueAmount = balanceAfterPDCs,
						TotalAmount = inv.Amount,
						BalanceBeforePDCs = balanceBeforePDCs,
						ReleasedPDCs = releasedPDCs,
						BalanceAfterPDCs = balanceAfterPDCs
					});
				}

				customerGroups.Add(new CustomerWiseInvoices
				{
					CustomerNo = group.Key,
					TotalDueAmount = totalDue,
					TotalPdcAmount = totalPdc,
					Invoices = invoiceSummaries
				});
			}

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
						PostedDate = inv.PostingDate,
						PdcAmount = inv.PdcAmount,
						DueAmount = inv.RemainingAmount - inv.PdcAmount,
						TotalAmount = inv.Amount,
						BalanceBeforePDCs = inv.RemainingAmount,
						ReleasedPDCs = inv.PdcAmount,
						BalanceAfterPDCs = inv.RemainingAmount - inv.PdcAmount
					}).ToList();

					return new CustomerWiseInvoices
					{
						CustomerNo = g.Key,
						TotalDueAmount = invoiceSummaries.Sum(i => i.BalanceBeforePDCs),
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
					OrderDate = invoiceDate,
					InvoicedAmount = invoice.Amount,
					DueAmount = invoice.RemainingAmount,
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
			var codesToSkip = new HashSet<string> { "SED-SNS", "SED-SKM" };

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

		private List<OrderCustomer> ProcessCustomersInParallel(IEnumerable<Customer> customers, Dictionary<string, decimal> dueAmountLookup)
		{
			// Only parallelize if dataset is large enough to benefit
			var customerList = customers as IList<Customer> ?? customers.ToList();
    
			if (customerList.Count < 100)
			{
				// Sequential processing for small datasets (faster due to less overhead)
				return customerList.Select(c => new OrderCustomer
				{
					CustomerCode = c.no,
					CustomerName = c.name,
					DueAmount = dueAmountLookup.TryGetValue(c.no, out decimal due) ? due : 0,
					CreditAllowed = c.creditAllowed,
					CreditLimit = c.creditLimitLCY,
					BalanceCredit = c.balanceLCY,
					PaymentTermCode = c.paymentTermsCode,
					PaymentMethodCode = c.paymentMethodCode
				}).ToList();
			}
    
			// Parallel processing for larger datasets
			return customerList.AsParallel()
				.WithDegreeOfParallelism(Math.Min(Environment.ProcessorCount, 8))
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


		private List<OrderItemDetails> ProcessItemsInParallel(IEnumerable<dynamic> items, Dictionary<string, decimal> priceLookup, IEnumerable<dynamic> inventory) // Pass inventory data here)
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
						   Inventory = i.reorderPoint == 0
							   ? inv.inventory.ToString()
							   : inv.inventory > i.reorderPoint
								   ? $"{i.reorderPoint}+"
								   : inv.inventory.ToString()
					   }).ToList()
					   : new List<LocationByItemInventory>();
					return new OrderItemDetails
					{
						ItemCode = i.no,
						ItemName = i.description,
						Unitprice = priceLookup.TryGetValue(i.no, out decimal price) ? price.ToString() : "0",
						SubstituteItems = CreateSubstituteItems(i.itemsubstitutions, priceLookup),
						LocationWiseInventory = locationInventories
					};
				})
				.ToList();
		}

		private List<SubstituteItem> CreateSubstituteItems(IEnumerable<ItemSubstitution> itemSubstitutions, Dictionary<string, decimal> priceLookup)
		{
			if (itemSubstitutions == null)
				return new List<SubstituteItem>();

			return itemSubstitutions.Select(substitution => new SubstituteItem
			{
				ItemCode = substitution.substituteNo,
				ItemName = substitution.description,
				UnitPrice = priceLookup.TryGetValue(substitution.substituteNo, out decimal subPrice) ? subPrice : 0
			}).ToList();
		}

	}
}