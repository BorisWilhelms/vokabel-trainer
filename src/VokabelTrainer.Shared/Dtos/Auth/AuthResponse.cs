using VokabelTrainer.Shared.Models;

namespace VokabelTrainer.Shared.Dtos.Auth;

public record AuthResponse(int UserId, string Username, UserRole Role, bool RequiresPasswordSetup);
