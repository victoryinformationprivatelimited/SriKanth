using SriKanth.Model.BusinessModule.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface
{
	public interface IBusinessApiService
	{
		Task<List<StockItem>> GetSalesStockDetails();
	}
}
