using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using HRIS.Model.Employee_Module.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SriKanth.Data;
using SriKanth.Interface.Data;
using SriKanth.Interface.SalesModule;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SriKanth.Service.SalesModule
{
	public class AzureBlobStorageService : IAzureBlobStorageService
	{
		private readonly BlobServiceClient _blobServiceClient;
		private readonly BlobContainerClient _blobContainerClient;
		private readonly ILogger<AzureBlobStorageService> _logger;
		private readonly IBusinessData _businessData;
		private readonly ILoginData _loginData;
		private readonly bool _generateSasTokens;
		private readonly int _sasTokenExpiryHours;

		/// <summary>
		/// Initializes a new instance of the <see cref="AzureBlobStorageService"/> class.
		/// </summary>
		/// <param name="configuration">Configuration to read Azure Blob settings.</param>
		/// <param name="logger">Logger for logging operations.</param>
		/// <param name="businessData">Data access for document storage.</param>
		/// <param name="loginData">Data access for user info.</param>
		public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger, IBusinessData businessData, ILoginData loginData)
		{
			var connectionString = configuration["AzureBlobStorage:ConnectionString"] ??
				throw new ArgumentNullException("AzureBlobStorage:ConnectionString is missing in configuration");
			var containerName = configuration["AzureBlobStorage:ContainerName"] ??
				throw new ArgumentNullException("AzureBlobStorage:ContainerName is missing in configuration");

			_blobServiceClient = new BlobServiceClient(connectionString);
			_blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
			_logger = logger;
			_businessData = businessData;
			_loginData = loginData;

			// Optional: Configure whether to generate SAS tokens and how long they are valid
			_generateSasTokens = configuration.GetValue("AzureBlobStorage:GenerateSasTokens", false);
			_sasTokenExpiryHours = configuration.GetValue("AzureBlobStorage:SasTokenExpiryHours", 1);
		}

		/// <summary>
		/// Uploads a document to Azure Blob Storage.
		/// </summary>
		/// <param name="userId">User ID to associate the document with.</param>
		/// <param name="document">Document to upload.</param>
		/// <returns>Tuple containing URL, content type, and original file name.</returns>
		public async Task<(string DocumentUrl, string DocumentType, string OriginalFileName)> UploadDocumentAsync(int userId, IFormFile document)
		{
			_logger.LogInformation("Beginning to upload document for user {UserId}", userId);

			if (document == null || document.Length == 0)
				throw new ArgumentException("Document is null or empty.");

			// Get user information from the database
			var user = await _loginData.GetUserByIdAsync(userId) ??
				throw new ApplicationException($"User with ID {userId} not found");

			// Generate a unique name for the blob while preserving original file extension
			var originalFileName = document.FileName;
			var fileExtension = Path.GetExtension(originalFileName);
			var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

			try
			{
				var blobClient = _blobContainerClient.GetBlobClient(uniqueFileName);

				// Upload the file stream with appropriate content type
				await using (var stream = document.OpenReadStream())
				{
					var uploadOptions = new BlobUploadOptions
					{
						HttpHeaders = new BlobHttpHeaders { ContentType = document.ContentType }
					};
					await blobClient.UploadAsync(stream, uploadOptions);
				}

				// Get URL (with SAS token if required)
				var documentUrl = _generateSasTokens
					? GenerateSasToken(blobClient)
					: blobClient.Uri.ToString();

				// Save document metadata in the database
				var userDoc = new UserDocumentStorage
				{
					UserId = userId,
					DocumentReference = documentUrl,
					DocumentType = document.ContentType,
					OriginalFileName = originalFileName,
					FileSize = document.Length,
					AddedDate = DateTime.UtcNow
				};

				await _businessData.AddDocumentAsync(userDoc);

				_logger.LogInformation("Successfully uploaded document for user {UserId}: {FileName}", userId, originalFileName);

				return (documentUrl, document.ContentType, originalFileName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to upload document for user {UserId}", userId);
				throw new ApplicationException("Document upload failed. Please try again.", ex);
			}
		}

		/// <summary>
		/// Retrieves a list of uploaded documents for a user.
		/// </summary>
		/// <param name="userId">User ID.</param>
		/// <returns>List of document metadata tuples.</returns>
		public async Task<List<(string DocumentUrl, string DocumentType, string OriginalFileName)>> GetListOfDocumentsAsync(int userId)
		{
			_logger.LogInformation("Retrieving documents for user {UserId}", userId);

			try
			{
				var userDocuments = await _businessData.GetUserDocumentsAsync(userId);
				var documentResults = new List<(string, string, string)>();

				foreach (var doc in userDocuments)
				{
					try
					{
						// If needed, regenerate SAS token
						var documentUrl = _generateSasTokens && !doc.DocumentReference.Contains("?")
							? GenerateSasTokenFromUrl(doc.DocumentReference)
							: doc.DocumentReference;

						documentResults.Add((documentUrl, doc.DocumentType, doc.OriginalFileName));
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to process document {DocumentReference} for user {UserId}",
							doc.DocumentReference, userId);
					}
				}

				return documentResults;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve documents for user {UserId}", userId);
				throw new ApplicationException("Failed to retrieve documents. Please try again.", ex);
			}
		}

		/// <summary>
		/// Downloads a document from Azure Blob Storage.
		/// </summary>
		/// <param name="documentUrl">URL of the document.</param>
		/// <returns>Tuple containing stream, content type, and file name.</returns>
		public async Task<(Stream FileStream, string ContentType, string FileName)> DownloadDocumentAsync(string documentUrl)
		{
			try
			{
				var blobClient = GetBlobClientFromUrl(documentUrl);
				var response = await blobClient.DownloadAsync();

				var contentType = response.Value.Details.ContentType ??
					GetContentType(blobClient.Name);

				return (response.Value.Content, contentType, blobClient.Name);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to download document from {DocumentUrl}", documentUrl);
				throw new ApplicationException("Document download failed. Please try again.", ex);
			}
		}

		/// <summary>
		/// Deletes a document from Azure Blob Storage.
		/// </summary>
		/// <param name="documentUrl">URL of the document.</param>
		/// <returns>True if deleted, false otherwise.</returns>
		public async Task<bool> DeleteDocumentAsync(string documentUrl)
		{
			_logger.LogInformation("Attempting to delete document: {DocumentUrl}", documentUrl);

			try
			{
				var blobClient = GetBlobClientFromUrl(documentUrl);
				var document = await _businessData.GetUserDocumenByUrlAsync(documentUrl);

				if (!await blobClient.ExistsAsync())
				{
					_logger.LogWarning("Document not found: {DocumentUrl}", documentUrl);
					return false;
				}

				var response = await blobClient.DeleteAsync();

				if (response.Status == 200 || response.Status == 202)
				{
					_logger.LogInformation("Successfully deleted document: {DocumentUrl}", documentUrl);
					await _businessData.RemoveDocumentAsync(document); // Assuming this method exists to remove metadata
					return true;
				}

				_logger.LogWarning("Failed to delete document: {DocumentUrl}. Status: {Status}", documentUrl, response.Status);
				return false;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting document: {DocumentUrl}", documentUrl);
				throw new ApplicationException("Document deletion failed. Please try again.", ex);
			}
		}

		/// <summary>
		/// Extracts the blob client from a given URL.
		/// </summary>
		/// <param name="documentUrl">Document URL.</param>
		/// <returns>Blob client object.</returns>
		private BlobClient GetBlobClientFromUrl(string documentUrl)
		{
			var uri = new Uri(documentUrl);
			var blobName = uri.Segments[^1].Split('?')[0]; // Handles SAS tokens
			return _blobContainerClient.GetBlobClient(blobName);
		}

		/// <summary>
		/// Generates a SAS token for the given blob.
		/// </summary>
		/// <param name="blobClient">Blob client.</param>
		/// <returns>SAS URL string.</returns>
		private string GenerateSasToken(BlobClient blobClient)
		{
			var sasBuilder = new BlobSasBuilder
			{
				BlobContainerName = _blobContainerClient.Name,
				BlobName = blobClient.Name,
				Resource = "b",
				ExpiresOn = DateTimeOffset.UtcNow.AddHours(_sasTokenExpiryHours)
			};

			sasBuilder.SetPermissions(BlobSasPermissions.Read);

			var sasToken = blobClient.GenerateSasUri(sasBuilder);
			return sasToken.ToString();
		}

		/// <summary>
		/// Generates a SAS token using a blob URL.
		/// </summary>
		/// <param name="blobUrl">URL of the blob.</param>
		/// <returns>SAS token URL string.</returns>
		private string GenerateSasTokenFromUrl(string blobUrl)
		{
			var blobClient = GetBlobClientFromUrl(blobUrl);
			return GenerateSasToken(blobClient);
		}

		/// <summary>
		/// Returns MIME content type based on file extension.
		/// </summary>
		/// <param name="fileName">File name.</param>
		/// <returns>Content type string.</returns>
		private static string GetContentType(string fileName)
		{
			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			return extension switch
			{
				".pdf" => "application/pdf",
				".jpg" or ".jpeg" => "image/jpeg",
				".png" => "image/png",
				".doc" => "application/msword",
				".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
				".xls" => "application/vnd.ms-excel",
				".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
				".ppt" => "application/vnd.ms-powerpoint",
				".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
				".txt" => "text/plain",
				".csv" => "text/csv",
				_ => "application/octet-stream"
			};
		}
	}
}