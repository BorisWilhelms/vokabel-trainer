namespace VokabelTrainer.Client.Services;

using System.Net.Http.Json;
using VokabelTrainer.Shared.Dtos.Auth;
using VokabelTrainer.Shared.Dtos.Languages;
using VokabelTrainer.Shared.Dtos.Lists;
using VokabelTrainer.Shared.Dtos.Training;
using VokabelTrainer.Shared.Dtos.Progress;
using VokabelTrainer.Shared.Dtos.Users;

public class ApiClient(HttpClient http)
{
    // Auth
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task LogoutAsync() => await http.PostAsync("api/auth/logout", null);

    public async Task<bool> NeedsSetupAsync()
        => await http.GetFromJsonAsync<bool>("api/auth/needs-setup");

    // Languages
    public async Task<List<LanguageDto>> GetLanguagesAsync()
        => await http.GetFromJsonAsync<List<LanguageDto>>("api/languages") ?? [];

    public async Task<LanguageDto?> CreateLanguageAsync(CreateLanguageRequest request)
    {
        var response = await http.PostAsJsonAsync("api/languages", request);
        return await response.Content.ReadFromJsonAsync<LanguageDto>();
    }

    public async Task<LanguageDto?> UpdateLanguageAsync(int id, UpdateLanguageRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/languages/{id}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LanguageDto>();
    }

    public async Task DeleteLanguageAsync(int id)
        => await http.DeleteAsync($"api/languages/{id}");

    // Users (admin)
    public async Task<List<UserDto>> GetUsersAsync()
        => await http.GetFromJsonAsync<List<UserDto>>("api/users") ?? [];

    public async Task CreateUserAsync(CreateUserRequest request)
        => await http.PostAsJsonAsync("api/users", request);

    public async Task ResetPasswordAsync(int userId)
        => await http.PostAsync($"api/users/{userId}/reset-password", null);

    public async Task DeleteUserAsync(int userId)
        => await http.DeleteAsync($"api/users/{userId}");

    // Vocabulary Lists
    public async Task<List<VocabularyListSummaryDto>> GetListsAsync()
        => await http.GetFromJsonAsync<List<VocabularyListSummaryDto>>("api/vocabularylists") ?? [];

    public async Task<VocabularyListDto?> GetListAsync(int id)
        => await http.GetFromJsonAsync<VocabularyListDto>($"api/vocabularylists/{id}");

    public async Task<int> CreateListAsync(CreateVocabularyListRequest request)
    {
        var response = await http.PostAsJsonAsync("api/vocabularylists", request);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<bool> UpdateListAsync(int id, UpdateVocabularyListRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/vocabularylists/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task DeleteListAsync(int id)
        => await http.DeleteAsync($"api/vocabularylists/{id}");

    // Training
    public async Task<int> StartSessionAsync(StartSessionRequest request)
    {
        var response = await http.PostAsJsonAsync("api/training/start", request);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<TrainingQuestionDto?> GetNextQuestionAsync(int sessionId)
        => await http.GetFromJsonAsync<TrainingQuestionDto?>($"api/training/next-question/{sessionId}");

    public async Task<SubmitAnswerResponse?> SubmitAnswerAsync(SubmitAnswerRequest request)
    {
        var response = await http.PostAsJsonAsync("api/training/submit-answer", request);
        return await response.Content.ReadFromJsonAsync<SubmitAnswerResponse>();
    }

    public async Task AbortSessionAsync(int sessionId)
        => await http.PostAsync($"api/training/abort/{sessionId}", null);

    public async Task<SessionResultDto?> GetSessionResultAsync(int sessionId)
        => await http.GetFromJsonAsync<SessionResultDto?>($"api/training/result/{sessionId}");

    // Progress
    public async Task<ListProgressDto?> GetListProgressAsync(int listId)
        => await http.GetFromJsonAsync<ListProgressDto?>($"api/progress/list/{listId}");
}
