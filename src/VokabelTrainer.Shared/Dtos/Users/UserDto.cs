using VokabelTrainer.Shared.Models;

namespace VokabelTrainer.Shared.Dtos.Users;

public record UserDto(int Id, string Username, UserRole Role, bool IsInitialized);
