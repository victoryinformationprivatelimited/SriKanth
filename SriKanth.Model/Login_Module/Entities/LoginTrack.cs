using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.Entities
{
    public class LoginTrack
    {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Automatically generate the ID
		public int LoginTrackID { get; set; }
		
		[Required]
		public int UserID { get; set; }
		[Required]
		[StringLength(50)]
		public string? LoginMethod { get; set; }
		[Required]
		public DateTime LoginTime { get; set; }
		[StringLength(50)]
		public string? IPAddress { get; set; }
		[StringLength(50)]
		public string? DeviceType { get; set; }
		[StringLength(50)]
		public string? OperatingSystem { get; set; }
		[StringLength(100)]
		public string? Browser { get; set; }
		[StringLength(50)]
		public string? Country { get; set; }
		[StringLength(50)]
		public string? City { get; set; }
		[Required]
		public bool IsSuccessful { get; set; }
		public bool? MFAUsed { get; set; }
		[StringLength(50)]
		public string? MFAMethod { get; set; }
		[StringLength(255)]
		public string? SessionID { get; set; }
		[StringLength(255)]
		public string? FailureReason { get; set; }
	}
}
