using SriKanth.Model;
using SriKanth.Model.Login_Module.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface
{
	public interface IMessageService
	{
		Task<string> GenerateMfaMessage(User user, string mfaCode);
		Task<string> GenerateLockedMessage(User user);
		Task<string> GenerateSMSMessage(User user, string mfaCode);
	}
}
