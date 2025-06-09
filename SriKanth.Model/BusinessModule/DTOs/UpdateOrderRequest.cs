using SriKanth.Model.BusinessModule.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class UpdateOrderRequest
	{
		[Required(ErrorMessage = "Order number is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Invalid order number.")]
		public int Ordernumber { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		public OrderStatus Status { get; set; }

		[StringLength(500, ErrorMessage = "Reject reason cannot exceed {1} characters.")]
		public string? RejectReason { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			// Require reject reason when status is Rejected
			if (Status == OrderStatus.Rejected && string.IsNullOrWhiteSpace(RejectReason))
			{
				yield return new ValidationResult(
					"Reject reason is required when status is Rejected.",
					new[] { nameof(RejectReason) });
			}

			// Validate reject reason is not provided for non-rejected statuses
			if (Status != OrderStatus.Rejected && !string.IsNullOrWhiteSpace(RejectReason))
			{
				yield return new ValidationResult(
					"Reject reason should only be provided when status is Rejected.",
					new[] { nameof(RejectReason) });
			}
		}
	}
}
