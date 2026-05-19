using SriKanth.Model.BusinessModule.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.DTOs
{
	public class LocationRequest
	{
		[Required(ErrorMessage = "Order number is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Invalid order number.")]
		public int Ordernumber { get; set; }

		[Required(ErrorMessage = "Location code is required.")]
		[StringLength(20, ErrorMessage = "Location code cannot exceed {1} characters.")]
		public string Locationcode { get; set; }
	}
}
