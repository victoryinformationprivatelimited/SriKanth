using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service.SalesModule
{
	public static class EnumerableExtensions
	{
		public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
		{
			var batch = new List<T>(batchSize);
			foreach (var item in source)
			{
				batch.Add(item);
				if (batch.Count == batchSize)
				{
					yield return batch;
					batch = new List<T>(batchSize);
				}
			}
			if (batch.Count > 0)
				yield return batch;
		}
	}
}
