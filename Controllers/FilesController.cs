using Microsoft.AspNetCore.Mvc;
using StudentManagement.Services;
using static StudentManagement.DTOs.FileUploadDbDto;

namespace StudentManagement.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] UploadFileDto dto)
    {
        try
        {
            var result = await _fileService.UploadFileAsync(dto.File);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var fileDto = await _fileService.GetFileForDownloadAsync(id);

        if (fileDto == null)
            return NotFound("Không tìm thấy file yêu cầu.");

        return File(fileDto.Content, fileDto.ContentType, fileDto.FileName);
    }
}

