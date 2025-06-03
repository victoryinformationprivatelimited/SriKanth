using HRIS.API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriKanth.Interface;
using SriKanth.Model.BusinessModule.DTOs;
using SriKanth.Model.BusinessModule.Entities;
using SriKanth.Model.Login_Module.DTOs;

namespace SriKanth.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class BusinessController : Controller
	{
		private readonly IConfiguration _configuration;
		private readonly IBusinessApiService _businessApiService;
		public BusinessController(IConfiguration configuration, IBusinessApiService businessApiService) 
		{
			_configuration = configuration;
			_businessApiService = businessApiService;
		}

		[HttpGet("GetStockDetails")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetStockDetails()
		{
			try
			{
				var stockData = await _businessApiService.GetSalesStockDetails();
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetListOfOrderCreationDetails")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetListOfOrderCreationDetails()
		{
			try
			{
				var stockData = await _businessApiService.GetOrderCreationDetailsAsync();
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpGet("GetOrderCreationDetailsByUser")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetOrderCreationDetailsByUser(int userId)
		{
			try
			{
				var stockData = await _businessApiService.GetFilteredOrderCreationDetailsAsync(userId);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}

		[HttpPost("CreateOrder")]
		//[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> CreateOrder(int userId, [FromBody] OrderRequest orderRequest)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				var result = await _businessApiService.SubmitOrderAsync(userId,orderRequest);

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

		[HttpGet("GetPendingOrders")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetPendingOrdersByUser(int userId)
		{
			try
			{
				var stockData = await _businessApiService.GetOrdersListAsync(userId, OrderStatus.Pending);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}
		[HttpGet("GetDeliveredOrders")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetDeliveredOrdersByUser(int userId)
		{
			try
			{
				var stockData = await _businessApiService.GetOrdersListAsync(userId, OrderStatus.Delivered);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}
		[HttpGet("GetRejectedOrders")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> GetRejectedOrdersByUser(int userId)
		{
			try
			{
				var stockData = await _businessApiService.GetOrdersListAsync(userId, OrderStatus.Rejected);
				return Ok(stockData);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error: {ex.Message}");
			}
		}
		[HttpPost("ChangeStatus")]
		[Authorize]
		[ServiceFilter(typeof(UserHistoryActionFilter))]
		public async Task<IActionResult> UpdateOrderStatus(UpdateOrderRequest updateOrderRequest)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				var result = await _businessApiService.UpdateOrderStatusAsync(updateOrderRequest);

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

	}
}
