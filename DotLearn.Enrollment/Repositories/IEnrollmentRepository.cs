using DotLearn.Enrollment.Models.Entities;

namespace DotLearn.Enrollment.Repositories;

public interface IEnrollmentRepository
{
    Task<DotLearn.Enrollment.Models.Entities.Enrollment?> GetByIdAsync(Guid id);
    Task<DotLearn.Enrollment.Models.Entities.Enrollment?> GetByStudentAndCourseAsync(Guid studentId, Guid courseId);
    Task<List<DotLearn.Enrollment.Models.Entities.Enrollment>> GetByStudentIdAsync(Guid studentId);
    Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId);
    Task AddAsync(DotLearn.Enrollment.Models.Entities.Enrollment enrollment);
    Task UpdateAsync(DotLearn.Enrollment.Models.Entities.Enrollment enrollment);
}
