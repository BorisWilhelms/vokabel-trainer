namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;
using VokabelTrainer.Shared.Models;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_EmptyDb_CreatesAdmin()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db);
        var result = await service.LoginOrSetupAsync("boris", "geheim123");
        result.Should().NotBeNull();
        result!.Role.Should().Be(UserRole.Admin);
        db.Users.Should().HaveCount(1);
        db.Users.First().IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("pw"), IsInitialized = true, Role = UserRole.Admin });
        await db.SaveChangesAsync();
        var service = new AuthService(db);
        var result = await service.LoginOrSetupAsync("unknown", "pw");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Login_UninitializedUser_SetsPassword()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "anna", Role = UserRole.User });
        await db.SaveChangesAsync();
        var service = new AuthService(db);
        var result = await service.LoginOrSetupAsync("anna", "meinpasswort");
        result.Should().NotBeNull();
        result!.RequiresPasswordSetup.Should().BeFalse();
        db.Users.First().IsInitialized.Should().BeTrue();
        db.Users.First().PasswordHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsUser()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "anna", PasswordHash = BCrypt.Net.BCrypt.HashPassword("richtig"), IsInitialized = true, Role = UserRole.User });
        await db.SaveChangesAsync();
        var service = new AuthService(db);
        var result = await service.LoginOrSetupAsync("anna", "richtig");
        result.Should().NotBeNull();
        result!.Username.Should().Be("anna");
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "anna", PasswordHash = BCrypt.Net.BCrypt.HashPassword("richtig"), IsInitialized = true, Role = UserRole.User });
        await db.SaveChangesAsync();
        var service = new AuthService(db);
        var result = await service.LoginOrSetupAsync("anna", "falsch");
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckNeedsSetup_EmptyDb_ReturnsTrue()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db);
        var result = await service.NeedsInitialSetupAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckNeedsSetup_WithUsers_ReturnsFalse()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "admin", IsInitialized = true, Role = UserRole.Admin });
        await db.SaveChangesAsync();
        var service = new AuthService(db);
        var result = await service.NeedsInitialSetupAsync();
        result.Should().BeFalse();
    }
}
