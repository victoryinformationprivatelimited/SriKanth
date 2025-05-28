using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class RequestLogin
	{
		[Required(ErrorMessage = "Username or Email is required.")]
		[StringLength(100, ErrorMessage = "Username or Email must be between 1 and 100 characters.", MinimumLength = 1)]
		[RegularExpression(@"^(?:(?:[a-zA-Z0-9_.]+)|(?:[a-zA-Z0-9_.]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+))$",
		 ErrorMessage = "Username can only contain letters, numbers, underscores, and periods. Email must be valid.")]
		public string UsernameOrEmail { get; set; }

		[Required(ErrorMessage = "Password is required.")]
		[StringLength(50, ErrorMessage = "Password must be between 6 and 50 characters.", MinimumLength = 6)]
		public string? Password { get; set; }
		public bool Rememberme { get; set; } = false;
	}
}
