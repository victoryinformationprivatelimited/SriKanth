using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class CustomerWiseInvoices
	{
		public string CustomerNo { get; set; }
		public string CustomerName { get; set; }
		public decimal TotalDueAmount { get; set; }
		public decimal TotalPdcAmount { get; set; }
		public List<InvoiceSummary> Invoices { get; set; }
	}

	public class InvoiceSummary
	{
		public string InvoiceNo { get; set; }
		public string? OrderNo { get; set; }
		public DateTime? PostedDate { get; set; }
		public decimal PdcAmount { get; set; }
		public decimal DueAmount { get; set; }
		public decimal TotalAmount { get; set; }// total amount get from items wise amount
		public decimal BalanceBeforePDCs { get; set; }
		public decimal ReleasedPDCs { get; set; }
		public decimal BalanceAfterPDCs { get; set; }
	}
}

