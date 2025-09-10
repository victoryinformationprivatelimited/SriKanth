﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class OrderReturn
	{
		public int OrderNumber { get; set; }
		public string CustomerName { get; set; }
		public string SalesPersonName { get; set; }
		public string Location { get; set; }
		public DateTime OrderDate { get; set; }
		public string PaymentMethodType { get; set; }
		public decimal TotalAmount { get; set; }
		public string Status { get; set; } // Pending, Completed, Rejected
		public string SpecialNote { get; set; }
		public List<OrderItemReturn> OrderedItems { get; set; }
		public string? InvoiceNumber { get; set; }
		public List<OrderItemReturn>? InvoicedItems { get; set; }
		public string? RejectReason { get; set; }
		public string? TrackingNumber { get; set; }
		public string? DelivertPersonName { get; set; }
		public DateOnly? DeliveryDate { get; set; }
	}

	public class OrderItemReturn
	{
		public string ItemCode { get; set; }
		public string Description { get; set; }
		public decimal Quantity { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal DiscountPercent { get; set; }
	}
}
