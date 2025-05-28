using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.Entities
{
    public class MFASetting
    {
        [Key]
        public int MFASettingID { get; set; }
        [Required]
        public int UserID { get; set; }
        [Required]
        public bool IsMFAEnabled { get; set; }
        [MaxLength(50)]
        public string? PreferredMFAType { get; set; }
    }
}
