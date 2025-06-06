using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class UserDetails
	{
		public string Username { get; set; }
		public string Password { get; set; }
		public string ReEnteredPassword { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public int UserRoleId { get; set; }
		public string SalesPersonCode { get; set; }
		public List<string> LocationCodes { get; set; }
		public string? Email { get; set; }
		public string? PhoneNumber { get; set; }
		public bool IsActive { get; set; }
		public bool IsMfaEnabled { get; set; }
		public string? MfaType { get; set; }
	}
}
