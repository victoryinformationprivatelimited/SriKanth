using Microsoft.AspNetCore.Identity;
using SriKanth.Interface;
using SriKanth.Interface.Data;
using SriKanth.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service
{
	public class UserHistoryService : IUserHistoryService
	{
		private readonly ILoginData _userData;

		public UserHistoryService(ILoginData userData)
		{
			_userData = userData;
		}

		public async Task LogUserActionAsync(int userId, string actionType, string entityType, string endpoint, string ipAddress)
		{
			var history = new UserHistory
			{
				UserId = userId,
				ActionType = actionType,
				EntityType = entityType,
				Endpoint = endpoint,
				Timestamp = DateTime.UtcNow,
				IPAddress = ipAddress
			};
			await _userData.AddUserHistoryAsync(history);
		}
	}
}
