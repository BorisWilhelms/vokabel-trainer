namespace VokabelTrainer.Api.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class AiService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<AiService> logger)
{
    private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

    public bool IsConfigured => !string.IsNullOrEmpty(config["OpenRouter:ApiKey"]);

    public async Task<string?> ExtractVocabularyAsync(byte[] imageBytes, string mimeType)
    {
        const string prompt = """
            You are a vocabulary extraction assistant. Extract vocabulary pairs from the image.
            Output ONLY the vocabulary in this exact format, one per line:
            term = translation1, translation2

            Do not add any other text, explanation, or formatting. Just the vocabulary lines.
            If you see multiple translations for one term, separate them with commas.
            Skip headers, page numbers, and non-vocabulary content.
            """;

        var base64 = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{mimeType};base64,{base64}";

        var messages = new[]
        {
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = dataUrl } }
                }
            }
        };

        return await CallAsync(config["OpenRouter:VisionModel"] ?? "google/gemini-2.5-flash", messages);
    }

    public async Task<string?> GenerateFlagSvgAsync(string languageName, string languageCode)
    {
        var prompt = $"""
            Generate a simple, clean SVG flag icon for the language "{languageName}" (code: {languageCode}).
            The SVG should be a small flag suitable for inline display (viewBox="0 0 24 16").
            Use the actual flag colors of the country most associated with this language.
            Output ONLY the raw SVG markup, starting with <svg and ending with </svg>.
            No explanation, no markdown, no code fences.
            """;

        var messages = new[]
        {
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt }
                }
            }
        };

        var result = await CallAsync(config["OpenRouter:TextModel"] ?? "google/gemini-2.5-flash", messages);

        // Clean up: extract just the SVG if the model added extra text
        if (result is not null)
        {
            var svgStart = result.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            var svgEnd = result.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
            if (svgStart >= 0 && svgEnd > svgStart)
            {
                result = result[svgStart..(svgEnd + 6)];
            }
        }

        return result;
    }

    public async Task<string?> GenerateHintAsync(string term, List<string> translations, string sourceLanguage, string targetLanguage)
    {
        var translationsStr = string.Join(", ", translations);
        var prompt = $"""
            Ein Schueler (14 Jahre, Gymnasium) lernt Vokabeln.
            Erstelle eine kurze, einpraegsame Eselsbruecke/Merkhilfe fuer:

            {term} ({sourceLanguage}) = {translationsStr} ({targetLanguage})

            Die Merkhilfe soll:
            - Maximal 1-2 Saetze lang sein
            - Kreativ und einpraegsam sein (Wortaehnlichkeiten, Bilder, Assoziationen)
            - Auf Deutsch formuliert sein
            - Keine Erklaerung drumherum, nur die Merkhilfe selbst
            """;

        var messages = new[]
        {
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt }
                }
            }
        };

        return await CallAsync(config["OpenRouter:TextModel"] ?? "google/gemini-2.5-flash", messages);
    }

    private async Task<string?> CallAsync(string model, object messages)
    {
        var apiKey = config["OpenRouter:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("OpenRouter API key not configured");
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new { model, messages, max_tokens = 4096 };
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
            logger.LogError(ex, "OpenRouter API call failed (model: {Model})", model);
            return null;
        }
    }
}
