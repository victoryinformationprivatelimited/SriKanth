using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface
{
	public interface IEncryptionService
	{
		string EncryptData(string plainText);
		string DecryptData(string cipherText);
	}
}
