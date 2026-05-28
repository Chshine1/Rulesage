namespace Rulesage.Shared.Services.Abstractions.TextCleaner;

public interface ITextCleaner
{
    IEnumerable<string> Clean(IDocumentSpace documentSpace, IEnumerable<string> texts);
}