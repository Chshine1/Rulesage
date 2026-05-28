using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Services.Implementations;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
public class TextCleanerConfig
{
    public float TfIdfThreshold { get; init; }
}

internal class TextCleaner(IOptions<TextCleanerConfig> config) : ITextCleaner
{   
    private readonly TextCleanerConfig _config = config.Value;

    public IEnumerable<string> Clean(int size, IEnumerable<string> texts)
    {
        var tokenizedDocs = texts
            .Select(desc => desc.Split([' ', '\n', '\t', '.', ',', '"', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        var df = tokenizedDocs
            .SelectMany(words => words.Distinct())
            .GroupBy(word => word)
            .ToDictionary(g => g.Key, g => g.Count());

        return tokenizedDocs.Select(words =>
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
                    var idf = Idf(w);
                    var tfidf = tf * idf;
                    return (Word: w, Tfidf: tfidf);
                })
                .OrderByDescending(p => p.Tfidf)
                .Take(Math.Min(k, distinctWords.Length))
                .Select(p => p.Word)
                .ToHashSet();

            var cleaned = string.Join(" ", words.Where(topWords.Contains));
            return cleaned;
        });

        double Idf(string word)
        {
            return df.TryGetValue(word, out var docFreq) ? Math.Log((size + 1.0) / (docFreq + 1.0)) : 0.0;
        }
    }
}