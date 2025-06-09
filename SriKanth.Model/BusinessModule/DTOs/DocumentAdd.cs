using Microsoft.AspNetCore.Http;
using SriKanth.Model.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class DocumentAdd
	{
		[Required(ErrorMessage = "User ID is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Invalid User ID.")]
		public int UserId { get; set; }
		[CustomValidation(typeof(PdfImageFileValidator), nameof(PdfImageFileValidator.Validate), ErrorMessage = "Only .jpg, .jpeg, .png and .pdf files are allowed.")]
		public IFormFile Document { get; set; }
	}
}
