namespace DotLearn.Enrollment.Models.Entities;

public class Enrollment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public decimal AmountPaid { get; set; }
    public string? TransactionId { get; set; }
    public double ProgressPercent { get; set; } = 0;
    public int CompletedLessons { get; set; } = 0;
    public int TotalLessons { get; set; } = 0;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum EnrollmentStatus { Active = 0, Completed = 1, Revoked = 2 }
