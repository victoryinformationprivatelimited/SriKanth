using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.BusinessModule.Entities
{
	public class PaymentMethod
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int PaymentMethodId { get; set; }
		public string PaymentMethodCode { get; set; }
		public string Description { get; set; }
	}
}
