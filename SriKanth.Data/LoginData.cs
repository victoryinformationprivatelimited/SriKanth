using Microsoft.EntityFrameworkCore;
using SriKanth.Interface.Data;
using SriKanth.Model;
using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Data
{
	public class LoginData : ILoginData
	{
		private readonly SriKanthDbContext _context;
		public LoginData(SriKanthDbContext dbContext)
		{
			_context = dbContext;
		}

		public async Task<User> GetUserByUsernameOrEmailAsync(string usernameOrEmail)
		{
			return await _context.Users
				.FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);
		}

		public async Task UpdateUserAsync(User user)
		{
			_context.Users.Update(user);
			await _context.SaveChangesAsync();
		}

		public async Task<List<User>> GetAllUsersAsync()
		{
			return await _context.Users.ToListAsync();
		}

		public async Task<User> GetUserByIdAsync(int userId)
		{
			return await _context.Users.FindAsync(userId);
		}

		public async Task AddUserTokenAsync(UserToken userToken)
		{
			await _context.UserToken.AddAsync(userToken);
			await _context.SaveChangesAsync();
		}

		public async Task AddSendTokenAsync(SendToken sendToken)
		{
			await _context.SendToken.AddAsync(sendToken);
			await _context.SaveChangesAsync();
		}
		public async Task AddLoginTrackAsync(LoginTrack loginTrack)
		{
			await _context.LoginTrack.AddAsync(loginTrack);
			await _context.SaveChangesAsync();
		}
		
		public async Task<MFASetting> GetMFATypeAsync(int userId)
		{
			return await _context.MFASetting.FirstOrDefaultAsync(m => m.UserID == userId);
		}
		public async Task<UserToken> GetUserTokenAsync(int userId, string hashedenteredMfa)
		{
			return await _context.UserToken
					.FirstOrDefaultAsync(ut => ut.UserID == userId && ut.Token == hashedenteredMfa && ut.TokenType == "MFA" &&
											   (ut.IsUsed == false || ut.IsUsed == null) && ut.ExpiresAt <= DateTime.Now);
		}

		public async Task UpdateUserTokenAsync(UserToken userToken)
		{
			_context.UserToken.Update(userToken);
			await _context.SaveChangesAsync();
		}
		public async Task UpdateLastLoginAsync(int userId)
		{
			var user = await _context.Users.FindAsync(userId);
			if (user != null)
			{
				user.LastLoginAt = DateTime.Now;
				await _context.SaveChangesAsync();
			}
		}
		public async Task<UserToken> GetUserTokenAsync(string hashedRefToken)
		{
			return await _context.UserToken
					.FirstOrDefaultAsync(ut => ut.Token == hashedRefToken && ut.TokenType == "RefreshToken" && ut.IsUsed == false && ut.IsRevoked == false);
		}
		public async Task UpdateUserTokenUsedAsync(string hashedRefToken)
		{
			var storedRefreshToken = await _context.UserToken
					.FirstOrDefaultAsync(ut => ut.Token == hashedRefToken && ut.TokenType == "RefreshToken" && ut.IsUsed == false && ut.IsRevoked == false);
			if (storedRefreshToken != null)
			{
				storedRefreshToken.IsUsed = true;
				storedRefreshToken.LastUsedAt = DateTime.UtcNow;
				await _context.SaveChangesAsync();
			}
		}
		public async Task<Message> GetMessageAsync(string messageName)
		{
			return await _context.Message.FirstOrDefaultAsync(m => m.MessageName == messageName);

		}
		public async Task AddNotificatonLogAsync(SentNotification log)
		{
			await _context.SentNotification.AddAsync(log);
			await _context.SaveChangesAsync();
		}
		public async Task<User> CheckUserByUsernameOrEmailAsync(string userName, string email)
		{		
			var normalizedUsername = userName.ToLower();
			var normalizedEmail = email.ToLower();

			return await _context.Users
				.FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername || u.Email.ToLower() == normalizedEmail);		
		}
		public async Task CreateUserAsync(User user)
		{
			await _context.Users.AddAsync(user);
			await _context.SaveChangesAsync();
		}
		public async Task<User> CheckUserByUsernameOrEmailExceptIdAsync(int userId, string userName, string email)
		{
			return await _context.Users
				.Where(p => p.UserID != userId &&
						   (p.Username == userName || p.Email == email))
				.FirstOrDefaultAsync();
		}
		public async Task<List<Roles>> GetRoleDetailsAsync()
		{
			return await _context.UserRole
				.Select(ur => new Roles
				{
					RoleId = ur.UserRoleID,
					RoleName = ur.UserRoleName
				})
				.ToListAsync();
		}
		public async Task AddMfaSettingAsync(MFASetting mFASetting)
		{
			await _context.MFASetting.AddAsync(mFASetting);
			await _context.SaveChangesAsync();
		}
		public async Task UpdateMfaTypeAsync(MFASetting mFASetting)
		{
			_context.MFASetting.Update(mFASetting);
			await _context.SaveChangesAsync();
		}
		public async Task<MFASetting> GetMfaSettingByIdAsync(int userId)
		{
			return await _context.MFASetting
				.FirstOrDefaultAsync(m => m.UserID == userId);
		}

		public async Task AddUserHistoryAsync(UserHistory userHistory)
		{
			await _context.UserHistory.AddAsync(userHistory);
			await _context.SaveChangesAsync();
		}
		public async Task AddUserLocationsAsync(IEnumerable<UserLocation> userLocations)
		{
			await _context.UserLocation.AddRangeAsync(userLocations);
			await _context.SaveChangesAsync();
		}
		public async Task<List<UserLocation>> GetUserLocationsByIdAsync(int userId)
		{
			return await _context.UserLocation
								 .Where(l => l.UserId == userId)
								 .ToListAsync();
		}
		public async Task RemoveUserLocationsByIdAsync(IEnumerable<UserLocation> userLocations)
		{
			if (userLocations.Any())
			{
				_context.UserLocation.RemoveRange(userLocations);
				await _context.SaveChangesAsync();
			}
		}
		public async Task<List<string>> GetUserLocationCodesAsync(int userId)
		{
			return await _context.UserLocation
								 .Where(l => l.UserId == userId)
								 .Select(l => l.LocationCode)
								 .ToListAsync();
		}

	}
}
