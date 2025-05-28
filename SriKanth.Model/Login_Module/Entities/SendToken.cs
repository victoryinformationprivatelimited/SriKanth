using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.Entities
{
	public class SendToken
	{
		[Key]
		public int SendTokenID { get; set; }
		[Required]
		public int UserID { get; set; }
		[Required]
		public int MFADeviceID { get; set; }
		[Required]
		public int UserTokenID { get; set; }
		[Required]
		public DateTime SendAt { get; set; }
		public bool? SendSuccessful { get; set; }
	}
}
