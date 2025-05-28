using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriKanth.Interface;

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

	}
}
