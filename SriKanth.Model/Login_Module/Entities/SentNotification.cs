using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.Entities
{
	public class SentNotification
	{
		[Key]
		public int NotificationId { get; set; }
		public string Recipient { get; set; }
		public string NotificationType { get; set; }
		public string Subject { get; set; }
		public string Message { get; set; }
		public DateTime SentAt { get; set; }
		public bool IsSuccess { get; set; }
	}
}
