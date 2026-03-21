namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Services;

public class VocabularyParserTests
{
    [Fact]
    public void Parse_SimpleEntry_ReturnsTermAndTranslations()
    {
        var result = VocabularyParser.Parse("res = Sache, Ding, Angelegenheit");
        result.Should().HaveCount(1);
        result[0].Term.Should().Be("res");
        result[0].Translations.Should().BeEquivalentTo(["Sache", "Ding", "Angelegenheit"]);
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsAll()
    {
        var input = "res = Sache, Ding\namo = lieben, moegen";
        var result = VocabularyParser.Parse(input);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_SemicolonSeparator_SplitsTranslations()
    {
        var result = VocabularyParser.Parse("pax = Frieden; Ruhe");
        result[0].Translations.Should().BeEquivalentTo(["Frieden", "Ruhe"]);
    }

    [Fact]
    public void Parse_PipeSeparator_SplitsTranslations()
    {
        var result = VocabularyParser.Parse("rex = Koenig | Herrscher");
        result[0].Translations.Should().BeEquivalentTo(["Koenig", "Herrscher"]);
    }

    [Fact]
    public void Parse_MixedSeparators_SplitsAll()
    {
        var result = VocabularyParser.Parse("res = Sache, Ding; Angelegenheit | Vermoegen");
        result[0].Translations.Should().HaveCount(4);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var result = VocabularyParser.Parse("  res  =  Sache ,  Ding  ");
        result[0].Term.Should().Be("res");
        result[0].Translations.Should().BeEquivalentTo(["Sache", "Ding"]);
    }

    [Fact]
    public void Parse_SkipsEmptyLines()
    {
        var input = "res = Sache\n\n\namo = lieben";
        var result = VocabularyParser.Parse(input);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_LineWithoutEquals_IsSkipped()
    {
        var result = VocabularyParser.Parse("this has no equals sign");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleTranslation_Works()
    {
        var result = VocabularyParser.Parse("bellum = Krieg");
        result[0].Translations.Should().BeEquivalentTo(["Krieg"]);
    }
}
