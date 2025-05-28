using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class InventoryApiResponse
	{
		[JsonPropertyName("@odata.context")]
		public string ODataContext { get; set; }

		public List<InventoryBalance> value { get; set; }
	}


	public class InventoryBalance
	{
		public string itemNo { get; set; }
		public string locationCode { get; set; }
		public int inventory { get; set; }
	}
}
