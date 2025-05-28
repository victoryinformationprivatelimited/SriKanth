using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class StockItem
	{
		public string ItemCode { get; set; }
		public string ItemName { get; set; }
		public string Location { get; set; }
		public string Stock { get; set; }
		public decimal UnitPrice { get; set; }
		public string ItemCategory { get; set; }
		public string Category { get; set; }
		public string SubCategory { get; set; }
		public string Description { get; set; }
		public string Description2 { get; set; }
		public string UnitOfMeasure { get; set; }
		public string Size { get; set; }
		public decimal ReorderQuantity { get; set; }
		public string Image { get; set; }
	}
}
