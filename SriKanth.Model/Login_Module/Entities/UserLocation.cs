using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.Entities
{
	public class UserLocation
	{
		public int UserLocationId { get; set; }	
		public int UserId { get; set; }
		public string LocationCode { get; set; }
	}
}
