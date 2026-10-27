using Microsoft.AspNetCore.Http;
using static StudentManagement.DTOs.FileUploadDbDto;

namespace StudentManagement.Services;

public interface IFileService
{
    Task<FileResponseDto> UploadFileAsync(IFormFile file);
    Task<FileDownloadDto?> GetFileForDownloadAsync(int id);
}

