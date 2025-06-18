using SriKanth.Model;
using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.Data
{
	public interface ILoginData
	{
		Task<User> GetUserByUsernameOrEmailAsync(string usernameOrEmail);
		Task UpdateUserAsync(User user);
		Task<List<User>> GetAllUsersAsync();
		Task<User> GetUserByIdAsync(int userId);
		Task AddUserTokenAsync(UserToken userToken);
		Task AddSendTokenAsync(SendToken sendToken);
		Task AddLoginTrackAsync(LoginTrack loginTrack);
		Task<MFASetting> GetMFATypeAsync(int userId);
		Task UpdateUserTokenUsedAsync(string hashedRefToken);
		Task<UserToken> GetUserTokenAsync(int userId, string hashedenteredMfa);
		Task UpdateUserTokenAsync(UserToken userToken);
		Task UpdateLastLoginAsync(int userId);
		Task<UserToken> GetUserTokenAsync(string hashedRefToken);
		Task<Message> GetMessageAsync(string messageName);
		Task AddNotificatonLogAsync(SentNotification log);
		Task<User> CheckUserByUsernameOrEmailAsync(string userName, string email);
		Task CreateUserAsync(User user);
		Task<User> CheckUserByUsernameOrEmailExceptIdAsync(int userId, string userName, string email);
		Task<List<Roles>> GetRoleDetailsAsync();
		Task AddMfaSettingAsync(MFASetting mFASetting);
		Task UpdateMfaTypeAsync(MFASetting mFASetting);
		Task<MFASetting> GetMfaSettingByIdAsync(int userId);
		Task AddUserHistoryAsync(UserHistory userHistory);
		Task AddUserLocationsAsync(IEnumerable<UserLocation> userLocations);
		Task<List<UserLocation>> GetUserLocationsByIdAsync(int userId);
		Task RemoveUserLocationsByIdAsync(IEnumerable<UserLocation> userLocations);
		Task<List<string>> GetUserLocationCodesAsync(int userId);
		Task<string> GetUserRoleNameAsync(int userRoleId);
	}
}
