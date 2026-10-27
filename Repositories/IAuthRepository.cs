using StudentManagement.Models;

namespace StudentManagement.Repositories;

public interface IAuthRepository
{
    User? GetByUsername(string username);
    User? GetById(int id);
    bool UsernameExists(string username);
    bool UserExists(string usernameOrEmail);
    void Add(User user);
    void SaveChanges();
}
