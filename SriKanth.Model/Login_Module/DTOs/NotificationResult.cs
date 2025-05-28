using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class NotificationResult
	{
		public bool IsSuccess { get; set; }
		public List<string> ErrorMessages { get; set; } = new List<string>();
	}
}
