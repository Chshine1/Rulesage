using Rulesage.Shared.Services.Abstractions.TextCleaner;

namespace Rulesage.Shared.Services.Implementations.TextCleaner;

internal class InMemoryDocumentSpace : IDocumentSpace
{
    private readonly int _documentCount;
    private readonly Dictionary<string, int> _documentFrequency;

    public InMemoryDocumentSpace(IReadOnlyList<string> documents)
    {
        _documentCount = documents.Count;
        _documentFrequency = documents
            .SelectMany(doc => Tokenize(doc).Distinct())
            .GroupBy(word => word)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public double GetIdf(string word)
    {
        var docFreq = _documentFrequency.GetValueOrDefault(word);
        return Math.Log((_documentCount + 1.0) / (docFreq + 1.0));
    }

    public string[] Tokenize(string text)
        => text.Split([' ', '\n', '\t', '.', ',', '"', '(', ')'],
            StringSplitOptions.RemoveEmptyEntries);
}