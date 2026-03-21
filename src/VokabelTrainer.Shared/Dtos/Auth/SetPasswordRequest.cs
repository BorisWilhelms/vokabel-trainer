namespace VokabelTrainer.Shared.Dtos.Auth;

public record SetPasswordRequest(string Username, string Password, string PasswordConfirmation);
