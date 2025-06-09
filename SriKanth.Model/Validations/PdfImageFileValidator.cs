using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Validations
{
	public class PdfImageFileValidator
	{
		public static ValidationResult Validate(IFormFile file, ValidationContext context)
		{
			if (file == null)
				return ValidationResult.Success;

			var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
			var extension = Path.GetExtension(file.FileName).ToLower();

			// Check extension
			if (!allowedExtensions.Contains(extension))
			{
				return new ValidationResult("Only .jpg, .jpeg, .png, and .pdf files are allowed.");
			}

			// Check content type
			var allowedContentTypes = new[] { "image/jpeg", "image/png", "application/pdf" };
			if (!allowedContentTypes.Contains(file.ContentType.ToLower()))
			{
				return new ValidationResult($"Invalid file content type. Received: {file.ContentType}");
			}
			// Check file size (5MB limit)
			const int maxFileSize = 5 * 1024 * 1024; // 5MB in bytes
			if (file.Length > maxFileSize)
			{
				return new ValidationResult($"Maximum allowed file size is 5MB. Your file is {file.Length / 1024 / 1024}MB.");
			}

			return ValidationResult.Success;
		}
	}
}
