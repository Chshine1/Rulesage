namespace Rulesage.Shared.Services.Abstractions;

public interface ITextCleaner
{
    IEnumerable<string> Clean(int size, IEnumerable<string> texts);
}