using SriKanth.Model.Login_Module.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.Notification
{
    public interface INotificationService
    {
		Task<object> SendNotificationAsync(NotificationRequest notificationRequest);
		Task<NotificationResult> StoreNotificationAsync(NotificationRequest.NotificationType type, NotificationRequest request);

	}
}
