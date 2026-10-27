using StudentManagement.Models;

namespace StudentManagement.Repositories;

public interface IFileRepository
{
    Task<StoredFile> AddAsync(StoredFile file);
    Task<StoredFile?> GetByIdAsync(int id);
}

