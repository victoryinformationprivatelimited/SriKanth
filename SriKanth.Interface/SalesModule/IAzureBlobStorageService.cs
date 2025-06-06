using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Interface.SalesModule
{
	public interface IAzureBlobStorageService
	{
		Task<(string DocumentUrl, string DocumentType, string OriginalFileName)> UploadDocumentAsync(int userId, IFormFile document);
		Task<List<(string DocumentUrl, string DocumentType, string OriginalFileName)>> GetListOfDocumentsAsync(int userId);
		Task<(Stream FileStream, string ContentType, string FileName)> DownloadDocumentAsync(string documentUrl);
		Task<bool> DeleteDocumentAsync(string documentUrl);
	}
}
