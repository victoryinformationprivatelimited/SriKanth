using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class AuthenticatedResponse
	{
		public string AccessToken { get; set; }  // JWT Access Token
		public string RefreshToken { get; set; } // Refresh Token (optional if using refresh tokens)
		public DateTime Expiration { get; set; } // Expiry time of the access token
	}
}
