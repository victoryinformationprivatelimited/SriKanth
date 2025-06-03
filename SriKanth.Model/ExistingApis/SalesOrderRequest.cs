using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class SalesOrderRequest
	{
		public string orderNo { get; set; }
		public string customerNo { get; set; }
		public string orderDate { get; set; }
		public string salespersonCode { get; set; }
		public string paymentMethodCode { get; set; }
		public string paymentTermCode { get; set; }
		public List<SalesIntegrationLine> salesIntegrationLines { get; set; }
	}

	public class SalesIntegrationLine
	{
		public int lineNo { get; set; }
		public string itemNo { get; set; }
		public string description { get; set; }
		public string location { get; set; }
		public decimal quantity { get; set; }
		public decimal unitPrice { get; set; }
		public decimal lineDiscount { get; set; }
	}

	public class SalesIntegrationResponse
	{
		// Define properties based on what the API returns
		// This is a placeholder - you should adjust based on the actual response
		public string id { get; set; }
		public string orderNo { get; set; }
		public string status { get; set; }
		// Add other relevant properties
	}
}
