using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class MFAValidationRequest
	{
		[Required(ErrorMessage = "UserId is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "UserId must be a positive integer.")]
		public int UserId { get; set; }

		[Required(ErrorMessage = "MFA Code is required.")]
		[StringLength(6, MinimumLength = 6, ErrorMessage = "MFA Code must be exactly 6 digits.")]
		[RegularExpression("^[0-9]{6}$", ErrorMessage = "MFA Code must be a 6-digit number.")]
		public string MfaCode { get; set; }
	}
}
