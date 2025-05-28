using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class ServiceResult
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public int? UserId { get; set; }
	}
}
