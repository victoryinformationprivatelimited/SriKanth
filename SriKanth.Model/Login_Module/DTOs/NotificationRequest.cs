using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class NotificationRequest
	{
		public List<string> Emails { get; set; } = new List<string>();
		public List<string> ToWnums { get; set; } = new List<string>();
		public List<string> ToPnums { get; set; } = new List<string>();
		public List<string> UserIds { get; set; } = new List<string>();

		[StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
		public string? Subject { get; set; }

		[StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters.")]
		public string? Message { get; set; }
		public List<NotificationType> NotificationTypes { get; set; } = new List<NotificationType>();

		public enum NotificationType
		{
			SMS,
			Email,
			WhatsApp,
			InApp
		}
	}
}
