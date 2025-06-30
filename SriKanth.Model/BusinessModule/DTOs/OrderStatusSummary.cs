using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class OrderStatusSummary
	{
		public int PendingCount { get; set; }
		public int DeliveredCount { get; set; }
		public int RejectedCount { get; set; }
	}
}
