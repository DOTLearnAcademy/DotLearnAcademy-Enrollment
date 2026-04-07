using DotLearn.Enrollment.Models.DTOs;

namespace DotLearn.Enrollment.Services;

public interface IEnrollmentService
{
    Task<EnrollmentResponseDto> EnrollFreeAsync(Guid courseId, Guid studentId);
    Task<EnrollmentResponseDto> CreateFromPaymentAsync(PaymentSucceededEventDto evt);
    Task<List<EnrollmentResponseDto>> GetMyEnrollmentsAsync(Guid studentId);
    Task<EnrollmentResponseDto?> GetByIdAsync(Guid id);
    Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId);
    Task UpdateProgressAsync(Guid enrollmentId, int completedLessons, int totalLessons);
    Task RevokeAccessAsync(Guid studentId, Guid courseId);
}
