using DotLearn.Enrollment.Data;
using DotLearn.Enrollment.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotLearn.Enrollment.Repositories;

public class EnrollmentRepository : IEnrollmentRepository
{
    private readonly EnrollmentDbContext _context;

    public EnrollmentRepository(EnrollmentDbContext context)
    {
        _context = context;
    }

    public async Task<DotLearn.Enrollment.Models.Entities.Enrollment?> GetByIdAsync(Guid id) =>
        await _context.Enrollments.FindAsync(id);

    public async Task<DotLearn.Enrollment.Models.Entities.Enrollment?> GetByStudentAndCourseAsync(
        Guid studentId, Guid courseId) =>
        await _context.Enrollments.FirstOrDefaultAsync(e =>
            e.StudentId == studentId && e.CourseId == courseId);

    public async Task<List<DotLearn.Enrollment.Models.Entities.Enrollment>> GetByStudentIdAsync(Guid studentId) =>
        await _context.Enrollments
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

    public async Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId) =>
        await _context.Enrollments.AnyAsync(e =>
            e.StudentId == studentId &&
            e.CourseId == courseId &&
            e.Status == EnrollmentStatus.Active);

    public async Task AddAsync(DotLearn.Enrollment.Models.Entities.Enrollment enrollment)
    {
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(DotLearn.Enrollment.Models.Entities.Enrollment enrollment)
    {
        _context.Enrollments.Update(enrollment);
        await _context.SaveChangesAsync();
    }
}
