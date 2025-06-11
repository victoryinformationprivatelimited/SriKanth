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
		[StringLength(100, ErrorMessage = "Username or Email must be between {2} and {1} characters.", MinimumLength = 1)]
		[RegularExpression(
		pattern: @"^(?:(?:[a-zA-Z0-9_\.-]+)|(?:[a-zA-Z0-9_\.-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-\.]+))$",
		ErrorMessage = "Invalid format. Use alphanumeric with _.- or a valid email.")]
		[Display(Name = "Username/Email")]
		public string UsernameOrEmail { get; set; }

		[Required(ErrorMessage = "Password is required.")]
		[StringLength(50, ErrorMessage = "Password must be between {2} and {1} characters.", MinimumLength = 8)]
		[DataType(DataType.Password)]
		[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z]).{8,}$",
			ErrorMessage = "Password must contain at least one uppercase and one lowercase letter.")]
		[Display(Name = "Password")]
		public string Password { get; set; }

		[Display(Name = "Remember Me")]
		public bool RememberMe { get; set; } = false;
	}
}
