using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class CustomerInvoiceReturn
	{
		public Decimal? TotalDueAmount { get; set; }
		public List<CustomerInvoice> CustomerInvoices { get; set; }
	}

	public class CustomerInvoice
	{
		public string CustomerCode { get; set; }
		public string CustomerName { get; set; }
		public string InvoiceDocumentNo { get; set; }
		public DateTime? InvoiceDate { get; set; }
		public Decimal InvoicedAmount { get; set; }
		public Decimal? DueAmount { get; set; }
	}
}
