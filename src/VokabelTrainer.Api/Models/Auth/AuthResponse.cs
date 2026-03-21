namespace VokabelTrainer.Api.Models.Auth;

public record AuthResponse(int UserId, string Username, UserRole Role, bool RequiresPasswordSetup);
