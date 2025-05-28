using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.Login_Module
{
	public interface IJwtTokenService
	{
		Task<string> GenerateJwtToken(User user);
		Task<string> GenerateRefreshToken(User user);
		Task<AuthenticatedResponse> RefreshToken(string refreshToken);
	}
}
