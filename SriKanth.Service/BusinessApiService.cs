using Microsoft.Extensions.Logging;
using SriKanth.Interface;
using SriKanth.Model.BusinessModule.DTOs;
using SriKanth.Model.ExistingApis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service
{
	public class BusinessApiService : IBusinessApiService
	{
		public readonly IExternalApiService _externalApiService;
		private readonly ILogger<BusinessApiService> _logger;

		public BusinessApiService(IExternalApiService externalApiService,ILogger<BusinessApiService> logger )
		{
			_externalApiService = externalApiService;
			_logger = logger;
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

	}
}
