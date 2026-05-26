using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class CustomerLoyaltyPointsApiResponse
	{

		[JsonPropertyName("@odata.context")]
		public string ODataContext { get; set; }

		public List<CustomerLoyaltyPoints> value { get; set; }
	}
	public class CustomerLoyaltyPoints
	{
		[JsonPropertyName("@odata.etag")]
		public string ODataEtag { get; set; }

		public string no { get; set; }

		public string name { get; set; }

		[JsonPropertyName("loyaltyPointsOnInovices")]
		public decimal loyaltyPointsOnInvoices { get; set; }

		public decimal loyaltyPointsOnCrMemos { get; set; }

		public string dateFilter { get; set; }
	}
}
