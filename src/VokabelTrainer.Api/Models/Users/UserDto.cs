namespace VokabelTrainer.Api.Models.Users;

public record UserDto(int Id, string Username, UserRole Role, bool IsInitialized);
