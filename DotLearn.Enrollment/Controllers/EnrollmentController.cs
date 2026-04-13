using DotLearn.Enrollment.Models.DTOs;
using DotLearn.Enrollment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DotLearn.Enrollment.Controllers;

[ApiController]
public class EnrollmentController : ControllerBase
{
    private readonly IEnrollmentService _service;

    public EnrollmentController(IEnrollmentService service)
    {
        _service = service;
    }

    // POST /api/enrollments/free
    [HttpPost("api/enrollments/free")]
    [Authorize(Roles = "Student,Admin")]
    public async Task<IActionResult> EnrollFree(
        [FromBody] EnrollFreeRequestDto request)
    {
        try
        {
            var result = await _service.EnrollFreeAsync(
                request.CourseId, GetUserId());
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // GET /api/enrollments/my
    [HttpGet("api/enrollments/my")]
    [Authorize]
    public async Task<IActionResult> GetMyEnrollments()
    {
        var result = await _service.GetMyEnrollmentsAsync(GetUserId());
        return Ok(result);
    }

    // GET /api/enrollments/{id}
    [HttpGet("api/enrollments/{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var enrollment = await _service.GetByIdAsync(id);
        if (enrollment == null)
            return NotFound(new { error = "Enrollment not found." });
        return Ok(enrollment);
    }

    // GET /api/enrollments/instructor/stats
    [HttpGet("api/enrollments/instructor/stats")]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> GetInstructorStats()
    {
        var instructorId = GetUserId();
        var stats = await _service.GetInstructorStatsAsync(instructorId);
        return Ok(stats);
    }

    // GET /internal/enrollments/check?userId=&courseId=
    [HttpGet("internal/enrollments/check")]
    [AllowAnonymous]
    public async Task<IActionResult> IsEnrolled(
        [FromQuery] Guid userId, [FromQuery] Guid courseId)
    {
        var result = await _service.IsEnrolledAsync(userId, courseId);
        return Ok(new IsEnrolledResponseDto(result));
    }

    // PUT /api/enrollments/{id}/progress -- student endpoint (replaces internal-only route for frontend)
    [HttpPut("api/enrollments/{id}/progress")]
    [Authorize]
    public async Task<IActionResult> UpdateMyProgress(
        Guid id, [FromBody] UpdateProgressRequestDto request)
    {
        // Verify the enrollment belongs to the calling student
        var enrollment = await _service.GetByIdAsync(id);
        if (enrollment == null) return NotFound(new { error = "Enrollment not found." });
        if (enrollment.StudentId != GetUserId())
            return StatusCode(403, new { error = "Forbidden: enrollment does not belong to you." });

        try
        {
            await _service.UpdateProgressAsync(id, request.CompletedLessons, request.TotalLessons);
            return Ok(new { message = "Progress updated." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // PUT /internal/enrollments/{id}/progress
    [HttpPut("internal/enrollments/{id}/progress")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateProgress(
        Guid id, [FromBody] UpdateProgressRequestDto request)
    {
        try
        {
            await _service.UpdateProgressAsync(
                id, request.CompletedLessons, request.TotalLessons);
            return Ok(new { message = "Progress updated." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found."));
}
