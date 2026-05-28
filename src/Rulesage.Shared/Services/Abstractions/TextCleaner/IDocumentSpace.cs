namespace Rulesage.Shared.Services.Abstractions.TextCleaner;

public interface IDocumentSpace
{
    string[] Tokenize(string source);
    double GetIdf(string word);
}