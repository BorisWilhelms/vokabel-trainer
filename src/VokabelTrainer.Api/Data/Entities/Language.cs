namespace VokabelTrainer.Api.Data.Entities;

public class Language
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public string? FlagSvg { get; set; }
}
