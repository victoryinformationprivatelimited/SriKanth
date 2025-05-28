using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class LocationApiResponse
	{
		[JsonPropertyName("@odata.context")] public string ODataContext { get; set; }
		public List<Location> value { get; set; }
	}

	public class Location
	{
		[JsonPropertyName("@odata.etag")] public string ODataEtag { get; set; }
		public string code { get; set; }
		public string name { get; set; }
	}
}
