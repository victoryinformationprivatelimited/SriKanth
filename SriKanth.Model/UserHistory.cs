using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model
{
	public class UserHistory
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int UserHistoryId { get; set; }
		public int UserId { get; set; }
		public string ActionType { get; set; } // "Add", "Update", "View"
		public string EntityType { get; set; } // "Employee", "Organization"
		public string Endpoint { get; set; }   // "/api/employee"
		public DateTime Timestamp { get; set; }
		public string IPAddress { get; set; }
	}
}
