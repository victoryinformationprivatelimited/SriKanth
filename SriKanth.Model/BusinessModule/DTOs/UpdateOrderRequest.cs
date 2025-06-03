using SriKanth.Model.BusinessModule.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class UpdateOrderRequest
	{
		public int Ordernumber { get; set; }
		public OrderStatus Status { get; set; }
		public string? RejectReason { get; set; }
	}
}
