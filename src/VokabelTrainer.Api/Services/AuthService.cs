namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models.Auth;
using VokabelTrainer.Api.Models;

public class AuthService(AppDbContext db)
{
    public async Task<User?> GetByIdAsync(int id)
    {
        return await db.Users.FindAsync(id);
    }

    public async Task<bool> NeedsInitialSetupAsync()
    {
        return !await db.Users.AnyAsync();
    }

    public async Task<AuthResponse?> LoginOrSetupAsync(string username, string password)
    {
        if (!await db.Users.AnyAsync())
        {
            var admin = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsInitialized = true,
                Role = UserRole.Admin
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            return new AuthResponse(admin.Id, admin.Username, admin.Role, false);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null)
            return null;

        if (!user.IsInitialized)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.IsInitialized = true;
            await db.SaveChangesAsync();
            return new AuthResponse(user.Id, user.Username, user.Role, false);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return new AuthResponse(user.Id, user.Username, user.Role, false);
    }
}
