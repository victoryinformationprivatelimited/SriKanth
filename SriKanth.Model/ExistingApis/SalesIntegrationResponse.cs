using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class SalesIntegrationResponse
	{
		[JsonPropertyName("@odata.context")]
		public string ODataContext { get; set; }

		[JsonPropertyName("@odata.etag")]
		public string ODataEtag { get; set; }

		public string orderNo { get; set; }
		public string customerNo { get; set; }
		public string orderDate { get; set; }
		public string salespersonCode { get; set; }
		public string paymentMethodCode { get; set; }
		public string paymentTermCode { get; set; }
	}
}
