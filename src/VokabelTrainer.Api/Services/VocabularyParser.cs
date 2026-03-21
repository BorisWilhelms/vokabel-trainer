namespace VokabelTrainer.Api.Services;

public static class VocabularyParser
{
    public record ParsedEntry(string Term, List<string> Translations);

    private static readonly char[] TranslationSeparators = [',', ';', '|'];

    public static List<ParsedEntry> Parse(string rawInput)
    {
        var results = new List<ParsedEntry>();

        foreach (var line in rawInput.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
                continue;

            var term = line[..equalsIndex].Trim();
            var translationsRaw = line[(equalsIndex + 1)..];

            var translations = translationsRaw
                .Split(TranslationSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (term.Length > 0 && translations.Count > 0)
                results.Add(new ParsedEntry(term, translations));
        }

        return results;
    }
}
