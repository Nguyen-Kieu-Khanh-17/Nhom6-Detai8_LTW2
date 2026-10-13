using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        this.reviewService = reviewService;
    }

    /// <summary>
    /// Bệnh nhân gửi đánh giá và nhận xét cho bác sĩ sau khi khám xong.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "User")]
    public async Task<ActionResult<ReviewResponseDto>> Create([FromBody] CreateReviewDto dto)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await reviewService.CreateReviewAsync(userId, dto);
            return CreatedAtAction(nameof(GetDoctorReviews), new { doctorId = result.DoctorId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Lấy danh sách đánh giá của một bác sĩ kèm điểm trung bình sao ⭐ và tổng số đánh giá.
    /// Cho phép mọi người dùng hoặc khách xem.
    /// </summary>
    [HttpGet("doctor/{doctorId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<DoctorReviewSummaryDto>> GetDoctorReviews(int doctorId)
    {
        try
        {
            var isAdmin = User.IsInRole("Admin");
            var summary = await reviewService.GetDoctorReviewsAsync(doctorId, includeHidden: isAdmin);
            return Ok(summary);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Admin xem toàn bộ đánh giá trong hệ thống (có thể lọc theo doctorId).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ReviewResponseDto>>> GetAll([FromQuery] int? doctorId = null)
    {
        var reviews = await reviewService.GetAllReviewsAsync(doctorId, includeHidden: true);
        return Ok(reviews);
    }

    /// <summary>
    /// Admin ẩn hoặc bỏ ẩn một đánh giá vi phạm tiêu chuẩn cộng đồng.
    /// </summary>
    [HttpPatch("{id:int}/toggle-hide")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReviewResponseDto>> ToggleHide(int id, [FromBody] ToggleReviewHideDto dto)
    {
        try
        {
            var updated = await reviewService.ToggleHideReviewAsync(id, dto.IsHidden);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Admin xóa vĩnh viễn một đánh giá.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await reviewService.DeleteReviewAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}

