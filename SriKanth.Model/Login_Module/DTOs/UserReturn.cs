using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class UserReturn
	{
		public string Username { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public int UserRoleId { get; set; }
		public string SalesPersonCode { get; set; }
		public bool IsActive { get; set; }
	}
}
