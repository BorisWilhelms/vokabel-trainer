namespace VokabelTrainer.Api.Models.Auth;

public record SetPasswordRequest(string Username, string Password, string PasswordConfirmation);
