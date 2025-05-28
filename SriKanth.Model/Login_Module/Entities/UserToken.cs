using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.Entities
{
   public class UserToken
    {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Automatically generate the ID
		public int TokenID { get; set; }
		[Required]
		public int UserID { get; set; }
		[Required]
		[MaxLength(1000)]
		public string Token { get; set; }
		[Required]
		[StringLength(50)]
		public string TokenType { get; set; }
		public DateTime? CreatedAt { get; set; }
		public DateTime? ExpiresAt { get; set; }
		public bool? IsUsed { get; set; }
		public bool? IsRevoked { get; set; }
		[StringLength(255)]
		public string Purpose { get; set; }
		public DateTime? LastUsedAt { get; set; }
	}
}
