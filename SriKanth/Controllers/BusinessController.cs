using HRIS.API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriKanth.Interface.SalesModule;
using SriKanth.Model.BusinessModule.DTOs;
using SriKanth.Model.BusinessModule.Entities;
using SriKanth.Model.Login_Module.DTOs;

namespace SriKanth.API.Controllers
{
	/// <summary>
	/// Controller for handling business-related operations including orders, documents, and inventory
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class BusinessController : Controller
	{
		private readonly IConfiguration _configuration;
		private readonly IBusinessApiService _businessApiService;
		private readonly IAzureBlobStorageService _azureBlobStorage;
		private readonly IOrderDetailsApiService _orderDetailsApiService;


		/// <summary>
		/// Initializes a new instance of the BusinessController class
		/// </summary>
		/// <param name="configuration">Application configuration</param>
		/// <param name="businessApiService">Business API service</param>
		/// <param name="azureBlobStorage">Azure Blob Storage service</param>
		public BusinessController(IConfiguration configuration, IBusinessApiService businessApiService, IAzureBlobStorageService azureBlobStorage, IOrderDetailsApiService orderDetailsApiService)
		{
			_configuration = configuration;
			_businessApiService = businessApiService;
			_azureBlobStorage = azureBlobStorage;
			_orderDetailsApiService = orderDetailsApiService;
		}

		/// <summary>
		/// Retrieves stock details from the inventory
		/// </summary>
		/// <returns>List of stock items</returns>
		[HttpGet("GetStockDetails")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetStockDetails()
		{
			try
			{
				// Get stock data from the business service
				var stockData = await _businessApiService.GetSalesStockDetails();
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}
		[HttpGet("item-image")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetImageByItemNo(string itemNo)
		{
			try
			{
				// Get stock data from the business service
				var itemImage = await _businessApiService.GetImageByItemNo(itemNo);
				return Ok(itemImage);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}
		/// <summary>
		/// Retrieves order creation details for all users
		/// </summary>
		/// <returns>List of order creation details</returns>
		[HttpGet("GetListOfOrderCreationDetails")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetListOfOrderCreationDetails()
		{
			try
			{
				// Get order creation details from the business service
				var stockData = await _businessApiService.GetOrderCreationDetailsAsync();
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		/// <summary>
		/// Retrieves order creation details for a specific user
		/// </summary>
		/// <param name="userId">ID of the user</param>
		/// <returns>List of order creation details for the specified user</returns>
		[HttpGet("GetOrderCreationDetailsByUser")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetOrderCreationDetailsByUser(int userId)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get filtered order creation details for the specified user
				var stockData = await _businessApiService.GetFilteredOrderCreationDetailsAsync(userId);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetLocationDetailsByUser")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetLocationDetailsByUser(int userId)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get filtered order creation details for the specified user
				var locations = await _businessApiService.GetFilteredLocationsAsync(userId);
				return Ok(locations);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetCustomersDetailsByUser")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetCustomersDetailsByUser(int userId)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get filtered order creation details for the specified user
				var customers = await _businessApiService.GetFilteredCustomersAsync(userId);
				return Ok(customers);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetItemsDetailsByUser")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetItemsDetailsByUser(int userId)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get filtered order creation details for the specified user
				var locations = await _businessApiService.GetFilteredItemsAsync(userId);
				return Ok(locations);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		/// <summary>
		/// Creates a new order for a user
		/// </summary>
		/// <param name="userId">ID of the user creating the order</param>
		/// <param name="orderRequest">Order request details</param>
		/// <returns>Result of the order creation</returns>
		[HttpPost("CreateOrder")]
		[Authorize(Roles = "SalesPerson")]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> CreateOrder(int userId, [FromBody] OrderRequest orderRequest)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Validate the model state
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				// Submit the order to the business service
				var result = await _orderDetailsApiService.SubmitOrderAsync(userId, orderRequest);

				if (!result.Success)
				{
					return BadRequest(new { message = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error:{ex.InnerException}");
			}
		}

		/// <summary>
		/// Retrieves pending orders for a specific user
		/// </summary>
		/// <param name="userId">ID of the user</param>
		/// <returns>List of pending orders</returns>
		[HttpGet("GetPendingOrders")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetPendingOrdersByUser(int userId)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get pending orders for the specified user
				var stockData = await _orderDetailsApiService.GetOrdersListAsync(userId, OrderStatus.Pending);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetProcessingOrders")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetProcessingOrdersByUser(int userId)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get pending orders for the specified user
				var stockData = await _orderDetailsApiService.GetOrdersListAsync(userId, OrderStatus.Processing);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		/// <summary>
		/// Retrieves delivered orders for a specific user
		/// </summary>
		/// <param name="userId">ID of the user</param>
		/// <returns>List of delivered orders</returns>
		[HttpGet("GetDeliveredOrders")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetDeliveredOrdersByUser(int userId)
		{
			// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get delivered orders for the specified user
				var stockData = await _orderDetailsApiService.GetOrdersListAsync(userId, OrderStatus.Delivered);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		/// <summary>
		/// Retrieves rejected orders for a specific user
		/// </summary>
		/// <param name="userId">ID of the user</param>
		/// <returns>List of rejected orders</returns>
		[HttpGet("GetRejectedOrders")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetRejectedOrdersByUser(int userId)
		{// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get rejected orders for the specified user
				var stockData = await _orderDetailsApiService.GetOrdersListAsync(userId, OrderStatus.Rejected);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		/// <summary>
		/// Updates the status of an order
		/// </summary>
		/// <param name="updateOrderRequest">Order status update request</param>
		/// <returns>Result of the status update</returns>
		[HttpPost("ChangeStatus")]
		[Authorize(Roles = "SalesCoordinator")]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> UpdateOrderStatus(UpdateOrderRequest updateOrderRequest)
		{
			try
			{
				// Validate the model state
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				// Update the order status through the business service
				var result = await _orderDetailsApiService.UpdateOrderStatusAsync(updateOrderRequest);

				if (!result.Success)
				{
					return BadRequest(new { message = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error:{ex.InnerException}");
			}
		}

		/// <summary>
		/// Retrieves invoice details for a specific user
		/// </summary>
		/// <param name="userId">ID of the user</param>
		/// <returns>List of invoices</returns>
		[HttpGet("GetInvoiceDetails")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetInvoicedByUser(int userId)
		{// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get invoice details for the specified user
				var stockData = await _businessApiService.GetCustomerInvoicesAsync(userId);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetInvoicesByCustomer")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetInvoicesByCustomer(string customerId)
		{
			if (!IsValidCustomerCode(customerId))
			{
				return BadRequest(new { message = "Invalid or missing customerId. CustomerId cannot be null or empty." });
			}
			try
			{
				// Get invoice details for the specified user
				var invoices = await _businessApiService.GetCustomerInvoiceDetailsAsync(customerId);
				return Ok(invoices);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetOrdersCount")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetOrdersCount(int userId)
		{// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get invoice details for the specified user
				var orderCounts = await _orderDetailsApiService.GetOrderStatusSummaryByUserAsync(userId);
				return Ok(orderCounts);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}
		/// <summary>
		/// Uploads a document for a user to Azure Blob Storage
		/// </summary>
		/// <param name="documentAdd">Document upload request</param>
		/// <returns>Result of the upload operation</returns>
		[HttpPost("UploadUserDocument")]
		[Authorize]
		[DisableRequestSizeLimit] // For large file uploads
		[RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = long.MaxValue)]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> UploadUserDocument([FromForm] DocumentAdd documentAdd)
		{
			try
			{
				// Check if document is provided
				if (documentAdd.Document == null)
					return BadRequest(new { message = "No document uploaded." });

				// Upload the document to Azure Blob Storage
				var (documentUrl, documentType, documentReference) = await _azureBlobStorage.UploadDocumentAsync(documentAdd.UserId, documentAdd.Document);

				return Ok(new
				{
					message = "Document uploaded successfully.",
					documentUrl,
					documentType
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		/// <summary>
		/// Retrieves a list of documents for a specific user
		/// </summary>
		/// <param name="userId">ID of the user</param>
		/// <returns>List of document metadata</returns>
		[HttpGet("GetListOfDocuments")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetUserDocuments(int userId)
		{// Security validation for userId
			if (!IsValidUserId(userId))
			{
				return BadRequest(new { message = "Invalid or missing userId. UserId must be greater than 0." });
			}
			try
			{
				// Get list of documents for the specified user
				var documents = await _azureBlobStorage.GetListOfDocumentsAsync(userId);

				// Format the response
				var result = documents.Select(d => new
				{
					Url = d.DocumentUrl,
					Type = d.DocumentType,
					OriginalName = d.OriginalFileName
				});

				return Ok(result);
			}
			catch (Exception ex)
			{
				return StatusCode(500, "Error retrieving documents");
			}
		}

		/// <summary>
		/// Downloads a document from Azure Blob Storage
		/// </summary>
		/// <param name="documentUrl">URL of the document to download</param>
		/// <returns>The document file</returns>
		[HttpGet("download")]
		[Authorize]
		public async Task<IActionResult> DownloadDocument([FromQuery] string documentUrl)
		{
			try
			{
				// Download the document from Azure Blob Storage
				var result = await _azureBlobStorage.DownloadDocumentAsync(documentUrl);

				// Return the file with proper content type and file name
				return File(result.FileStream, result.ContentType, result.FileName);
			}
			catch (Exception ex)
			{
				return StatusCode(500, "Error downloading document");
			}
		}

		/// <summary>
		/// Deletes a document from Azure Blob Storage
		/// </summary>
		/// <param name="documentUrl">URL of the document to delete</param>
		/// <returns>Result of the delete operation</returns>
		[HttpDelete("DeleteDocument")]
		[Authorize]
		public async Task<IActionResult> DeleteDocument([FromQuery] string documentUrl)
		{
			try
			{
				// Delete the document from Azure Blob Storage
				await _azureBlobStorage.DeleteDocumentAsync(documentUrl);
				return NoContent();
			}
			catch (Exception ex)
			{
				return StatusCode(500, "Error deleting document");
			}
		}

		[HttpGet("check-environment")]
		public IActionResult CheckEnvironment([FromServices] IConfiguration config)
		{
			var envName = config["BusinessCentral:EnvironmentName"];
			var companyId = config["BusinessCentral:CompanyId"];
			return Ok(new { Environment = envName, CompanyId = companyId });
		}


		private bool IsValidUserId(int userId)
		{
			return userId > 0;
		}
		private bool IsValidCustomerCode(string customerCode)
		{
			return !string.IsNullOrWhiteSpace(customerCode);
		}
	}
}