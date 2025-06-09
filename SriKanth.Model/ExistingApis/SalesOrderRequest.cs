using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class SalesOrderRequest
	{
		[Required]
		[StringLength(20, ErrorMessage = "OrderNo cannot exceed 20 characters")]
		public string orderNo { get; set; }

		[Required]
		[StringLength(20, ErrorMessage = "CustomerNo cannot exceed 20 characters")]
		public string customerNo { get; set; }

		[Required]
		[RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "OrderDate must be in YYYY-MM-DD format")]
		public string orderDate { get; set; }

		[Required]
		[StringLength(10, ErrorMessage = "SalespersonCode cannot exceed 10 characters")]
		public string salespersonCode { get; set; }

		[Required]
		[StringLength(10, ErrorMessage = "PaymentMethodCode cannot exceed 10 characters")]
		public string paymentMethodCode { get; set; }

		[Required]
		[StringLength(10, ErrorMessage = "PaymentTermCode cannot exceed 10 characters")]
		public string paymentTermCode { get; set; }

		[Required]
		[MinLength(1, ErrorMessage = "At least one order line is required")]
		public List<SalesIntegrationLine> salesIntegrationLines { get; set; } = new List<SalesIntegrationLine>();
	}

	public class SalesIntegrationLine
	{
		[Required]
		[Range(10000, int.MaxValue, ErrorMessage = "LineNo must be at least 10000")]
		public int lineNo { get; set; }  // Business Central typically expects line numbers in 10000 increments

		[Required]
		[StringLength(20, ErrorMessage = "ItemNo cannot exceed 20 characters")]
		public string itemNo { get; set; }

		[StringLength(50, ErrorMessage = "Description cannot exceed 50 characters")]
		public string description { get; set; }

		[Required]
		[StringLength(10, ErrorMessage = "Location cannot exceed 10 characters")]
		public string location { get; set; }

		[Required]
		[Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
		public decimal quantity { get; set; }

		[Required]
		[Range(0, double.MaxValue, ErrorMessage = "UnitPrice cannot be negative")]
		public decimal unitPrice { get; set; }

		[Range(0, 100, ErrorMessage = "Discount must be between 0 and 100")]
		public decimal lineDiscount { get; set; }  // Now clearly representing percentage (0-100)
	}

}
