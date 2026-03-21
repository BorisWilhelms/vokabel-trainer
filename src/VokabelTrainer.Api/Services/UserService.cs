// Services/UserService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models.Users;
using VokabelTrainer.Api.Models;

public class UserService(AppDbContext db)
{
    public async Task<List<UserDto>> GetAllAsync()
        => await db.Users
            .Select(u => new UserDto(u.Id, u.Username, u.Role, u.IsInitialized))
            .ToListAsync();

    public async Task<UserDto> CreateAsync(CreateUserRequest request)
    {
        var user = new User { Username = request.Username, Role = UserRole.User };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new UserDto(user.Id, user.Username, user.Role, user.IsInitialized);
    }

    public async Task<bool> ResetPasswordAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return false;
        user.PasswordHash = null;
        user.IsInitialized = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return false;
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return true;
    }
}
