namespace DotLearn.Enrollment.Models.DTOs;

public record EnrollFreeRequestDto(Guid CourseId);

public record EnrollmentResponseDto(
    Guid Id,
    Guid StudentId,
    Guid CourseId,
    string Status,
    decimal AmountPaid,
    string? TransactionId,
    double ProgressPercent,
    int CompletedLessons,
    int TotalLessons,
    DateTime EnrolledAt,
    DateTime? CompletedAt
);

public record IsEnrolledResponseDto(bool IsEnrolled);

public record UpdateProgressRequestDto(int CompletedLessons, int TotalLessons);

public record PaymentSucceededEventDto(
    string EventType,
    Guid StudentId,
    Guid CourseId,
    string TransactionId,
    decimal Amount,
    DateTime Timestamp
);
