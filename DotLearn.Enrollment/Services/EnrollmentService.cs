using DotLearn.Enrollment.Models.DTOs;
using DotLearn.Enrollment.Models.Entities;
using DotLearn.Enrollment.Repositories;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;

namespace DotLearn.Enrollment.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly IEnrollmentRepository _repo;
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public EnrollmentService(
        IEnrollmentRepository repo,
        IAmazonSQS sqsClient,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _repo = repo;
        _sqsClient = sqsClient;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<EnrollmentResponseDto> EnrollFreeAsync(
        Guid courseId, Guid studentId)
    {
        // Call Course Service to verify course is actually free
        using var httpClient = _httpClientFactory.CreateClient();
        var courseUrl = $"{_config["Services:CourseServiceUrl"]}/internal/courses/{courseId}/price";
        var response = await httpClient.GetAsync(courseUrl);
        if (response.IsSuccessStatusCode)
        {
            var priceJson = await response.Content.ReadAsStringAsync();
            var price = JsonSerializer.Deserialize<CoursePriceDto>(priceJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (price?.Price > 0)
                throw new InvalidOperationException("This course requires payment.");
        }

        // Check duplicate
        var existing = await _repo.GetByStudentAndCourseAsync(studentId, courseId);
        if (existing != null)
            throw new InvalidOperationException("Already enrolled in this course.");

        var enrollment = new DotLearn.Enrollment.Models.Entities.Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            AmountPaid = 0,
            TransactionId = null,
            Status = EnrollmentStatus.Active
        };

        await _repo.AddAsync(enrollment);
        return MapToDto(enrollment);
    }

    public async Task<EnrollmentResponseDto> CreateFromPaymentAsync(
        PaymentSucceededEventDto evt)
    {
        // Idempotency check
        var existing = await _repo.GetByStudentAndCourseAsync(
            evt.StudentId, evt.CourseId);
        if (existing != null) return MapToDto(existing);

        var enrollment = new DotLearn.Enrollment.Models.Entities.Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = evt.StudentId,
            CourseId = evt.CourseId,
            AmountPaid = evt.Amount,
            TransactionId = evt.TransactionId,
            Status = EnrollmentStatus.Active
        };

        await _repo.AddAsync(enrollment);
        return MapToDto(enrollment);
    }

    public async Task<List<EnrollmentResponseDto>> GetMyEnrollmentsAsync(
        Guid studentId)
    {
        var enrollments = await _repo.GetByStudentIdAsync(studentId);
        return enrollments.Select(MapToDto).ToList();
    }

    public async Task<EnrollmentResponseDto?> GetByIdAsync(Guid id)
    {
        var enrollment = await _repo.GetByIdAsync(id);
        return enrollment == null ? null : MapToDto(enrollment);
    }

    public async Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId) =>
        await _repo.IsEnrolledAsync(studentId, courseId);

    public async Task UpdateProgressAsync(
        Guid enrollmentId, int completedLessons, int totalLessons)
    {
        var enrollment = await _repo.GetByIdAsync(enrollmentId)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        enrollment.CompletedLessons = completedLessons;
        enrollment.TotalLessons = totalLessons;
        enrollment.ProgressPercent = totalLessons == 0 ? 0 :
            Math.Round((double)completedLessons / totalLessons * 100, 2);

        if (enrollment.ProgressPercent >= 100)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.CompletedAt = DateTime.UtcNow;
            await PublishEnrollmentCompletedAsync(enrollment);
        }

        await _repo.UpdateAsync(enrollment);
    }

    public async Task RevokeAccessAsync(Guid studentId, Guid courseId)
    {
        var enrollment = await _repo.GetByStudentAndCourseAsync(studentId, courseId);
        if (enrollment == null) return;

        enrollment.Status = EnrollmentStatus.Revoked;
        await _repo.UpdateAsync(enrollment);
    }

    private async Task PublishEnrollmentCompletedAsync(DotLearn.Enrollment.Models.Entities.Enrollment enrollment)
    {
        var message = JsonSerializer.Serialize(new
        {
            EventType = "EnrollmentCompleted",
            enrollment.StudentId,
            enrollment.CourseId,
            enrollment.Id,
            Timestamp = DateTime.UtcNow
        });

        await _sqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _config["SQS:EnrollmentCompletedQueue"],
            MessageBody = message
        });
    }

    private static EnrollmentResponseDto MapToDto(DotLearn.Enrollment.Models.Entities.Enrollment e) => new(
        e.Id, e.StudentId, e.CourseId, e.Status.ToString(),
        e.AmountPaid, e.TransactionId, e.ProgressPercent,
        e.CompletedLessons, e.TotalLessons, e.EnrolledAt, e.CompletedAt
    );
}
