using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class SalesPriceApiResponse
	{
		[JsonPropertyName("@odata.context")]
		public string ODataContext { get; set; }

		public List<SalesPrice> value { get; set; }
	}

	public class SalesPrice
	{
		public string itemNo { get; set; }
		public string unitOfMeasureCode { get; set; }
		public decimal unitPrice { get; set; }
		public string auxiliaryIndex1 { get; set; }
		public int auxiliaryIndex2 { get; set; }
	}
}
