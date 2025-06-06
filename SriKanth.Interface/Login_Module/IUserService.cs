using SriKanth.Model.Login_Module.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.Login_Module
{
	public interface IUserService
	{
		Task<LoginResult> LoginAsync(RequestLogin loginRequest);
		Task<LoginResult> PasswordResetAsync(RequestLogin loginReq);
		Task<LoginResult> StoreNewpasswordAsync(PasswordReset passwordreset);
		Task<bool> LockAccountAsync(RequestLogin loginRequest);
		Task<bool> UnlockAccountAsync(RequestLogin loginRequest);
		Task<ServiceResult> CreateUserAsync(UserDetails userDetails);
		Task<ServiceResult> UpdateUserAsync(int userId, UserDetails userDetails);
		Task<UserCreationDetails> GetUserCreationDetailsAsync();
		Task<UserDetails> GetUserDetailsByIdAsync(int userId);
		Task<List<UserReturn>> GetListOfUsersAsync();
	}
}
