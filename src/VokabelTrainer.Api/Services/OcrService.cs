namespace VokabelTrainer.Api.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class OcrService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<OcrService> logger)
{
    private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

    private const string SystemPrompt = """
        You are a vocabulary extraction assistant. Extract vocabulary pairs from the image.
        Output ONLY the vocabulary in this exact format, one per line:
        term = translation1, translation2

        Do not add any other text, explanation, or formatting. Just the vocabulary lines.
        If you see multiple translations for one term, separate them with commas.
        Skip headers, page numbers, and non-vocabulary content.
        """;

    public bool IsConfigured
    {
        get
        {
            var apiKey = config["OpenRouter:ApiKey"];
            return !string.IsNullOrEmpty(apiKey);
        }
    }

    public async Task<string?> ExtractVocabularyAsync(byte[] imageBytes, string mimeType)
    {
        var apiKey = config["OpenRouter:ApiKey"];
        var model = config["OpenRouter:Model"] ?? "anthropic/claude-sonnet-4";

        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("OpenRouter API key not configured");
            return null;
        }

        var base64 = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{mimeType};base64,{base64}";

        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = SystemPrompt },
                            new { type = "image_url", image_url = new { url = dataUrl } }
                        }
                    }
                },
                max_tokens = 4096
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(OpenRouterUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("OpenRouter API error ({Status}): {Response}", response.StatusCode, responseJson);
                return null;
            }

            using var doc = JsonDocument.Parse(responseJson);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return text?.Trim();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to call OpenRouter Vision API");
            return null;
        }
    }
}
