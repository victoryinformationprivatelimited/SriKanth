using Microsoft.Extensions.Configuration;
using SriKanth.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Service
{
	public class EncryptionService : IEncryptionService
	{
		private readonly IConfiguration _configuration;
		private string _key;
		private string _iv;

		/// <summary>
		/// Initializes a new instance of the <see cref="EncryptionService"/> class.
		/// </summary>
		/// <param name="configuration">The configuration object used to access encryption settings.</param>
		public EncryptionService(IConfiguration configuration)
		{
			_configuration = configuration;
			_key = _configuration["EncryptionSettings:Key"]; // Retrieve encryption key from configuration
			_iv = _configuration["EncryptionSettings:IV"]; // Retrieve initialization vector from configuration

			/*// If key or IV are null or empty, generate them
			if (string.IsNullOrEmpty(_key) || string.IsNullOrEmpty(_iv))
			{
				var (generatedKey, generatedIV) = GenerateEncryptionKeyAndIV();
				_key = generatedKey;
				_iv = generatedIV;

				// Optionally, store the generated values back to environment variables or config file
				Environment.SetEnvironmentVariable("EncryptionKey", _key);
				Environment.SetEnvironmentVariable("EncryptionIV", _iv);

				// Log or output the newly generated values (for debugging purposes)
				Console.WriteLine($"Generated and set Key: {_key}");
				Console.WriteLine($"Generated and set IV: {_iv}");*/

		}


		// Method to generate a new encryption key and IV
		/*
		public (string Key, string IV) GenerateEncryptionKeyAndIV()
		{
			using (Aes aes = Aes.Create())
			{
				aes.GenerateKey();
				aes.GenerateIV();

				_key = Convert.ToBase64String(aes.Key);  // 32-byte key for AES-256
				_iv = Convert.ToBase64String(aes.IV);    // 16-byte IV

				Console.WriteLine($"Generated Key: {_key}");
				Console.WriteLine($"Generated IV: {_iv}");

				return (_key, _iv);
			}
		}
		*/

		// Encrypt method (can use _key and _iv generated above)
		/// <summary>
		/// Encrypts the specified plain text using AES encryption.
		/// </summary>
		/// <param name="plainText">The plain text to encrypt.</param>
		/// <returns>The encrypted text, encoded as a Base64 string.</returns>
		public string EncryptData(string plainText)
		{
			using (Aes aesAlg = Aes.Create()) // Create a new AES algorithm instance
			{
				aesAlg.Key = Convert.FromBase64String(_key);  // Set the key
				aesAlg.IV = Convert.FromBase64String(_iv); // Set the initialization vector

				ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV); // Create an encryptor

				using (MemoryStream msEncrypt = new MemoryStream())
				{
					using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
					{
						using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
						{
							swEncrypt.Write(plainText); // Write plain text to the CryptoStream
						}
						byte[] encrypted = msEncrypt.ToArray(); // Get the encrypted data
						return Convert.ToBase64String(encrypted);  // Return encrypted data as Base64 string
					}
				}
			}
		}

		// Decrypt method (uses _key and _iv)
		/// <summary>
		/// Decrypts the specified cipher text using AES decryption.
		/// </summary>
		/// <param name="cipherText">The encrypted text, encoded as a Base64 string.</param>
		/// <returns>The decrypted plain text.</returns>
		public string DecryptData(string cipherText)
		{
			using (Aes aesAlg = Aes.Create()) // Create a new AES algorithm instance
			{
				aesAlg.Key = Convert.FromBase64String(_key); // Set the key
				aesAlg.IV = Convert.FromBase64String(_iv);  // Set the initialization vector

				ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV); // Create a decryptor

				using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
				{
					using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
					{
						using (StreamReader srDecrypt = new StreamReader(csDecrypt))
						{
							return srDecrypt.ReadToEnd(); // Write plain text to the CryptoStream
						}
					}
				}
			}
		}

	}
}
