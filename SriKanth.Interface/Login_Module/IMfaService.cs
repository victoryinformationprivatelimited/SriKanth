using SriKanth.Model.Login_Module.DTOs;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.Login_Module
{
	public interface IMfaService
	{
		Task<LoginResult> ValidateMfaAsync(int userId, string enteredMfaCode);
		Task<bool> SendMfaCodeAsync(User user, string mfaType);
	}
}
