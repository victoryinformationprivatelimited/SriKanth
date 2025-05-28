using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class LoginResult
	{
		[Required]
		public bool Success { get; set; }
		public bool RequiresMfa { get; set; }
		public string? AccessToken { get; set; }
		public string? RefreshToken { get; set; }
		public string? Message { get; set; }
		public int? UserId { get; set; }
		public bool UserLocked { get; set; } = false;
	}
}
