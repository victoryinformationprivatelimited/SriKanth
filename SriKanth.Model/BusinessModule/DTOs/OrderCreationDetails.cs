using SriKanth.Model.Login_Module.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class OrderCreationDetails
	{
		public List<Location> Locations { get; set; }
		public List<OrderCustomer> Customers{ get; set; }
		public List<OrderItemDetails> Items { get; set; }

	}
	public class OrderCustomer
	{
		public string CustomerCode { get; set; }
		public string CustomerName { get; set; }
		public Decimal? DueAmount { get; set; }
		public bool CreditAllowed { get; set; }
		public Decimal CreditLimit { get; set; }
		public Decimal BalanceCredit { get; set; }
		public string PaymentTermCode { get; set; }
		public string PaymentMethodCode { get; set; }

	}
	public class OrderItemDetails
	{
		public string ItemCode { get; set; }
		public string ItemName { get; set; }
		public string Unitprice {  get; set; }
		public List<LocationByItemInventory> LocationWiseInventory { get; set; }
		public List<SubstituteItem> SubstituteItems { get; set; }

	}
	public class LocationByItemInventory
	{ 
		public string LocationCode { get; set; }
		public string Inventory { get; set; }
	}

	public class SubstituteItem
	{
		public string ItemCode { get; set; }
		public string ItemName { get; set; }
		public Decimal UnitPrice { get; set; }
	}
}
