using DotLearn.Enrollment.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotLearn.Enrollment.Data;

public class EnrollmentDbContext : DbContext
{
    public EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options)
        : base(options) { }

    public DbSet<DotLearn.Enrollment.Models.Entities.Enrollment> Enrollments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DotLearn.Enrollment.Models.Entities.Enrollment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();
            entity.Property(e => e.Status).HasDefaultValue(EnrollmentStatus.Active);
            entity.Property(e => e.AmountPaid).HasColumnType("decimal(18,2)");
        });
    }
}
