using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRIS.Model.Employee_Module.Entities
{
	public class UserDocumentStorage
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int UserDocumentStorageId { get; set; }
		public int UserId { get; set; }
		public string DocumentReference { get; set; }
		public string OriginalFileName { get; set; }
		public long FileSize { get; set; }
		public string DocumentType { get; set; }
		public DateTime AddedDate { get; set; }

	}
}
