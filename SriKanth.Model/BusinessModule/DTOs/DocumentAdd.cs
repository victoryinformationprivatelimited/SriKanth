using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class DocumentAdd
	{
		public int UserId { get; set; }
		public IFormFile Document { get; set; }
	}
}
