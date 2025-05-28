using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SriKanth.Model.ExistingApis
{
	public class ItemApiResponse
	{
		[JsonPropertyName("@odata.context")] public string ODataContext { get; set; }
		public List<Item> value { get; set; }
	}

	public class Item
	{
		[JsonPropertyName("@odata.etag")] public string ODataEtag { get; set; }
		public string no { get; set; }
		public Guid systemId { get; set; }
		public string description { get; set; }
		public string description2 { get; set; }
		public string unitOfMeasure { get; set; }
		public string size { get; set; }
		public decimal reorderQuantity { get; set; }
		public decimal reorderPoint { get; set; }
		public string globalDimension1Code { get; set; }
		public string itemCategoryCode { get; set; }
		public string parentCategoryCode { get; set; }
		public string childCategoryCode { get; set; }
		public List<ItemSubstitution> itemsubstitutions { get; set; }
	}
	public class ItemSubstitution
	{
		[JsonPropertyName("@odata.etag")] public string ODataEtag { get; set; }
		public string type { get; set; }
		public string no { get; set; }
		public string variantCode { get; set; }
		public string substituteType { get; set; }
		public string substituteNo { get; set; }
		public string substituteVariantCode { get; set; }
		public string description { get; set; }
	}
}
