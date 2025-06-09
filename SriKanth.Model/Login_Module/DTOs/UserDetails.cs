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
		[Required(ErrorMessage = "Username is required.")]
		[StringLength(50, ErrorMessage = "Username must be between {2} and {1} characters.", MinimumLength = 4)]
		[RegularExpression(@"^[a-zA-Z0-9_\-\.]+$",
			ErrorMessage = "Username can only contain letters, numbers, underscores, hyphens, and dots.")]
		public string Username { get; set; }

		[Required(ErrorMessage = "Password is required.")]
		[StringLength(100, ErrorMessage = "Password must be between {2} and {1} characters.", MinimumLength = 8)]
		[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z]).{8,}$",
	ErrorMessage = "Password must contain at least one uppercase and one lowercase letter.")]
		[DataType(DataType.Password)]
		public string Password { get; set; }


		[Required(ErrorMessage = "Please re-enter your password.")]
		[Compare("Password", ErrorMessage = "Passwords do not match.")]
		[DataType(DataType.Password)]
		public string ReEnteredPassword { get; set; }

		[Required(ErrorMessage = "First name is required.")]
		[StringLength(50, ErrorMessage = "First name must be less than {1} characters.")]
		[RegularExpression(@"^[a-zA-Z\s\-']+$",
			ErrorMessage = "First name can only contain letters, spaces, hyphens, and apostrophes.")]
		public string FirstName { get; set; }

		[Required(ErrorMessage = "Last name is required.")]
		[StringLength(50, ErrorMessage = "Last name must be less than {1} characters.")]
		[RegularExpression(@"^[a-zA-Z\s\-']+$",
			ErrorMessage = "Last name can only contain letters, spaces, hyphens, and apostrophes.")]
		public string LastName { get; set; }

		[Required(ErrorMessage = "User role is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Please select a valid user role.")]
		public int UserRoleId { get; set; }

		[StringLength(20, ErrorMessage = "Sales person code must be less than {1} characters.")]
		public string SalesPersonCode { get; set; }

		[Required(ErrorMessage = "At least one location must be selected.")]
		[MinLength(1, ErrorMessage = "At least one location must be selected.")]
		public List<string> LocationCodes { get; set; }

		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(100, ErrorMessage = "Email must be less than {1} characters.")]
		public string? Email { get; set; }

		[Phone(ErrorMessage = "Please enter a valid phone number.")]
		[StringLength(20, ErrorMessage = "Phone number must be less than {1} characters.")]
		public string? PhoneNumber { get; set; }

		[Required]
		public bool IsActive { get; set; }

		[Required]
		public bool IsMfaEnabled { get; set; }

		[StringLength(20, ErrorMessage = "MFA type must be less than {1} characters.")]
		public string? MfaType { get; set; }
	}
}
