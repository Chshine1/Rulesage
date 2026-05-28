using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Rulesage.Shared.Services.Abstractions.TextCleaner;

namespace Rulesage.Shared.Services.Implementations.TextCleaner;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
public class TextCleanerConfig
{
    public float TfIdfThreshold { get; init; }
}

internal class TextCleaner(IOptions<TextCleanerConfig> config) : ITextCleaner
{
    private readonly TextCleanerConfig _config = config.Value;

    public IEnumerable<string> Clean(IDocumentSpace documentSpace, IEnumerable<string> texts)
    {
        var tokenizedDocs = texts
            .Select(documentSpace.Tokenize)
            .ToList();

        foreach (var words in tokenizedDocs)
        {
            var tfMap = words
                .GroupBy(w => w)
                .ToDictionary(g => g.Key, g => 1.0 + Math.Log(g.Count()));

            var k = Math.Max(5, (int)(_config.TfIdfThreshold * words.Length));
            var distinctWords = words.Distinct().ToArray();

            var topWords = distinctWords
                .Select(w =>
                {
                    var tf = tfMap.GetValueOrDefault(w, 0.0);
                    var idf = documentSpace.GetIdf(w);
                    return (Word: w, Tfidf: tf * idf);
                })
                .OrderByDescending(p => p.Tfidf)
                .Take(Math.Min(k, distinctWords.Length))
                .Select(p => p.Word)
                .ToHashSet();

            yield return string.Join(" ", words.Where(topWords.Contains));
        }
    }
}