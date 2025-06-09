using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class OrderRequest
	{
		[Required(ErrorMessage = "Customer code is required.")]
		[StringLength(20, ErrorMessage = "Customer code cannot exceed {1} characters.")]
		public string CustomerCode { get; set; }

		[Required(ErrorMessage = "Location code is required.")]
		[StringLength(10, ErrorMessage = "Location code cannot exceed {1} characters.")]
		public string LocationCode { get; set; }

		[Required(ErrorMessage = "Payment method is required.")]
		[StringLength(10, ErrorMessage = "Payment method code cannot exceed {1} characters.")]
		public string PaymentMethodCode { get; set; }

		[StringLength(500, ErrorMessage = "Special note cannot exceed {1} characters.")]
		public string? SpecialNote { get; set; }

		[Required(ErrorMessage = "Total amount is required.")]
		[Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than 0.")]
		public decimal TotalAmount { get; set; }

		[Required(ErrorMessage = "Order items are required.")]
		[MinLength(1, ErrorMessage = "At least one order item is required.")]
		public List<OrderItemRequest> Items { get; set; }
	}

	public class OrderItemRequest
	{
		[Required(ErrorMessage = "Item code is required.")]
		[StringLength(20, ErrorMessage = "Item code cannot exceed {1} characters.")]
		public string ItemCode { get; set; }

		[Required(ErrorMessage = "Description is required.")]
		[StringLength(100, ErrorMessage = "Description cannot exceed {1} characters.")]
		public string Description { get; set; }

		[Required(ErrorMessage = "Quantity is required.")]
		[Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
		public decimal Quantity { get; set; }

		[Required(ErrorMessage = "Unit price is required.")]
		[Range(0, double.MaxValue, ErrorMessage = "Unit price cannot be negative.")]
		public decimal UnitPrice { get; set; }

		[Range(0, 100, ErrorMessage = "Discount must be between 0 and 100 percent.")]
		public decimal DiscountPercent { get; set; }
	}
}
