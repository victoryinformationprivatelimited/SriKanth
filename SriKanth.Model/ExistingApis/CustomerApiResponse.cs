using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class CustomerApiResponse
	{
		[JsonPropertyName("@odata.context")]
		public string ODataContext { get; set; }

		public List<Customer> value { get; set; }
	}
	public class Customer
	{
		[JsonPropertyName("@odata.etag")]
		public string ODataEtag { get; set; }

		public string no { get; set; }
		public string name { get; set; }
		public decimal creditLimitLCY { get; set; }
		public bool creditAllowed { get; set; }
		public string address { get; set; }
		public string address2 { get; set; }
		public string whatsAppNo { get; set; }
		public string phoneNo { get; set; }
		public string eMail { get; set; }
		public string customerPostingGroup { get; set; }
		public string paymentTermsCode { get; set; }
		public string paymentMethodCode { get; set; }
		public string salespersonCode { get; set; }
		public decimal balanceLCY { get; set; }
	}
}
