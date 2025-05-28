using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class SalesPeopleApiResponse
	{
		[JsonPropertyName("@odata.context")] public string ODataContext { get; set; }
		public List<SalesPerson> value { get; set; }
	}

	public class SalesPerson
	{
		[JsonPropertyName("@odata.etag")] public string ODataEtag { get; set; }
		public string code { get; set; }
		public string name { get; set; }
		public string nic { get; set; }
		public string phoneNo { get; set; }
		public string eMail { get; set; }
	}
}
