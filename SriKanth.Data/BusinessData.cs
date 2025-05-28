using SriKanth.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Data
{
	public class BusinessData : IBusinessData
	{

		private readonly SriKanthDbContext _context;
		public BusinessData(SriKanthDbContext dbContext)
		{
			_context = dbContext;
		}
	}
}
