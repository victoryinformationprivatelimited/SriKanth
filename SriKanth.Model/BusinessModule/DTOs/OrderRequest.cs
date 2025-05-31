using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class OrderRequest
	{
		public string CustomerCode { get; set; }
		public string LocationCode { get; set; }
		public string PaymentMethodCode { get; set; }
		public decimal TotalAmount { get; set; }
		public List<OrderItemRequest> Items { get; set; }
	}

	public class OrderItemRequest
	{
		public string ItemCode { get; set; }
		public string Description { get; set; }
		public decimal Quantity { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal DiscountPercent { get; set; }
	}
}
