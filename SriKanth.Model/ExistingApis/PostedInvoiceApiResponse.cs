using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class PostedInvoiceApiResponse
	{
		[JsonPropertyName("@odata.context")]
		public string ODataContext { get; set; }

		public List<PostedInvoice> value { get; set; }
	}

	public class PostedInvoice
	{
		[JsonPropertyName("@odata.etag")]
		public string ODataEtag { get; set; }

		[JsonPropertyName("no")]
		public string DocumentNo { get; set; }

		[JsonPropertyName("orderNo")]
		public string OrderNo { get; set; }

		[JsonPropertyName("sellToCustomerNo")]
		public string CustomerNo { get; set; }

		[JsonPropertyName("amount")]
		public decimal Amount { get; set; }

		[JsonPropertyName("remainingAmount")]
		public decimal RemainingAmount { get; set; }

		[JsonPropertyName("pdcAmount")]
		public decimal PdcAmount { get; set; }

		[JsonPropertyName("postedInvoiceLines")]
		public List<InvoiceLine> Lines { get; set; }
	}

	public class InvoiceLine
	{
		[JsonPropertyName("@odata.etag")]
		public string ODataEtag { get; set; }

		[JsonPropertyName("documentNo")]
		public string DocumentNo { get; set; }

		[JsonPropertyName("lineNo")]
		public int LineNo { get; set; }

		[JsonPropertyName("orderNo")]
		public string OrderNo { get; set; }

		[JsonPropertyName("customerNo")]
		public string CustomerNo { get; set; }

		[JsonPropertyName("salesAppLineNo")]
		public int SalesAppLineNo { get; set; }

		[JsonPropertyName("type")]
		public string Type { get; set; }

		[JsonPropertyName("no")]
		public string ItemNo { get; set; }

		[JsonPropertyName("description")]
		public string Description { get; set; }

		[JsonPropertyName("description2")]
		public string Description2 { get; set; }

		[JsonPropertyName("quantity")]
		public decimal Quantity { get; set; }

		[JsonPropertyName("unitOfMeasureCode")]
		public string UnitOfMeasureCode { get; set; }

		[JsonPropertyName("unitPrice")]
		public decimal UnitPrice { get; set; }

		[JsonPropertyName("amount")]
		public decimal Amount { get; set; }

		[JsonPropertyName("lineDiscount")]
		public decimal LineDiscount { get; set; }
	}
}
