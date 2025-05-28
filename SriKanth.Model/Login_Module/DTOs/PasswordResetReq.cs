using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class PasswordResetReq
	{
		[Required(ErrorMessage = "User ID is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than 0.")]
		public int UserId { get; set; }

		[Required(ErrorMessage = "New Password is required.")]
		[StringLength(50, ErrorMessage = "Password must be between 6 and 50 characters.", MinimumLength = 6)]
		public string NewPassword { get; set; }

		[Required(ErrorMessage = "Confirm Password is required.")]
		[Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
		public string ConfirmPassword { get; set; }
	}
}
