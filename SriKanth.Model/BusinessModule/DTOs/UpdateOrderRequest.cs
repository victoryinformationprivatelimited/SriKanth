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
		public string? TrackingNumber { get; set; }
		public string? DelivertPersonName { get; set; }
		public DateTime? DeliveryDate { get; set; }
		public string? Note { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			// Reject reason validation
			if (Status == OrderStatus.Rejected && string.IsNullOrWhiteSpace(RejectReason))
			{
				yield return new ValidationResult(
					"Reject reason is required when status is Rejected.",
					new[] { nameof(RejectReason) });
			}

			if (Status != OrderStatus.Rejected && !string.IsNullOrWhiteSpace(RejectReason))
			{
				yield return new ValidationResult(
					"Reject reason should only be provided when status is Rejected.",
					new[] { nameof(RejectReason) });
			}

			// Delivery info validation
			if (Status == OrderStatus.Delivered)
			{
				if (string.IsNullOrWhiteSpace(TrackingNumber))
				{
					yield return new ValidationResult(
						"Tracking number is required when status is Delivered.",
						new[] { nameof(TrackingNumber) });
				}

				if (string.IsNullOrWhiteSpace(DelivertPersonName))
				{
					yield return new ValidationResult(
						"Delivery person name is required when status is Delivered.",
						new[] { nameof(DelivertPersonName) });
				}

				if (DeliveryDate == null)
				{
					yield return new ValidationResult(
						"Delivery date is required when status is Delivered.",
						new[] { nameof(DeliveryDate) });
				}
			}
			else
			{
				// Optional: validate that delivery fields are null for other statuses
				if (!string.IsNullOrWhiteSpace(TrackingNumber) ||
					!string.IsNullOrWhiteSpace(DelivertPersonName) ||
					DeliveryDate != null)
				{
					yield return new ValidationResult(
						"Tracking number, delivery person name, and delivery date should only be provided when status is Delivered.",
						new[] { nameof(TrackingNumber), nameof(DelivertPersonName), nameof(DeliveryDate) });
				}
			}
		}

	}
}
