namespace HaaS.Domain.ValueObjects;

public record Identity(string Name, string[] Claims)
{
    public static readonly Identity Anonymous = new("anonymous", Array.Empty<string>());
}
