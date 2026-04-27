using Microsoft.FSharp.Collections;
using Rulesage.Common.Types;
using Rulesage.DslComposer;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Synthesis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class AstSynthesizer(
    IDslEntryResolver entryResolver,
    ILlmService llmService,
    IDslComposer nlTaskResolver,
    IEnumerable<AstNodeSignature> signatures) : IAstSynthesizer
{
    private readonly Dictionary<int, AstNodeSignature> _signatures = signatures.ToDictionary(s => s.id);

    private readonly Dictionary<FilledAst, SynthesizedValue> _cache = new();

    public async Task<Dictionary<string, SynthesizedValue>> SynthesizeAsync(DslEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (entry.context.Any())
            throw new ArgumentException("Entrypoint DslEntry cannot carry context");

        var results = new Dictionary<string, SynthesizedValue>();
        foreach (var (key, filledAst) in entry.produce)
        {
            results[key] = await SynthesizeFilledAstAsync(entry, filledAst, new Dictionary<string, SynthesizedValue>(),
                cancellationToken);
        }

        return results;
    }

    private async Task<SynthesizedValue> SynthesizeFilledAstAsync(
        DslEntry currentEntry,
        FilledAst filledAst,
        Dictionary<string, SynthesizedValue> context,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(filledAst, out var cached))
            return cached;

        if (!_signatures.ContainsKey(filledAst.astId))
            throw new InvalidOperationException($"Ast signature id not found, ID: {filledAst.astId}");

        var paramDict = MapModule.Empty<string, SynthesizedValue>();
        foreach (var (paramKey, filling) in filledAst.paramaterFillings)
        {
            paramDict.Add(paramKey, await ResolveFillingAsync(currentEntry, filling, context, cancellationToken));
        }

        var result = SynthesizedValue.NewSynAst(filledAst.astId, paramDict);
        _cache[filledAst] = result;
        return result;
    }

    private async Task<SynthesizedValue> ResolveFillingAsync(
        DslEntry currentEntry,
        AstParametersFilling filling,
        Dictionary<string, SynthesizedValue> context,
        CancellationToken cancellationToken = default)
    {
        if (filling.IsLeaf)
        {
            var leaf = filling.AsLeaf();
            return leaf.IsLiteralLeaf
                ? SynthesizedValue.NewSynLeaf(leaf.AsLiteralLeaf())
                : SynthesizedValue.NewSynLeaf(await llmService.CompleteAsync(leaf.AsNlLeaf(), cancellationToken));
        }

        if (filling.IsAstLiteral)
        {
            var valueList = filling.AsAstLiteral();
            var subParams = MapModule.Empty<string, SynthesizedValue>();
            foreach (var (key, subFilling) in valueList)
            {
                subParams.Add(key, await ResolveFillingAsync(currentEntry, subFilling, context));
            }
            return SynthesizedValue.NewSynAst(0, subParams);
        }

        if (filling.IsFromContext)
        {
            var key = filling.AsFromContext();
            return !context.TryGetValue(key, out var val) ? throw new InvalidOperationException($"Missing context key: {key}") : val;
        }

        if (!filling.IsFromSubtask) throw new ArgumentOutOfRangeException(nameof(filling));

        var fromSub = filling.AsFromSubtask();
        return await ExecuteSubtaskAndGetResultAsync(currentEntry, fromSub.Item1, fromSub.Item2,
            context, cancellationToken);
    }

    private async Task<SynthesizedValue> ExecuteSubtaskAndGetResultAsync(
        DslEntry currentEntry,
        string subtaskKey,
        string productionKey,
        Dictionary<string, SynthesizedValue> context,
        CancellationToken cancellationToken = default)
    {
        if (currentEntry.subtasks.All(s => s.Item1 != subtaskKey))
            throw new InvalidOperationException($"Subtask not found: {subtaskKey}");

        var subtask = currentEntry.subtasks.First(s => s.Item1 == subtaskKey).Item2;

        Dictionary<string, SynthesizedValue> produceResults;
        if (subtask.IsDslCall)
        {
            var dslCall = subtask.AsDslCall();
            var subContext = new Dictionary<string, SynthesizedValue>();
            foreach (var (ctxKey, filling) in dslCall.Item2)
            {
                subContext[ctxKey] = await ResolveFillingAsync(currentEntry, filling, context, cancellationToken);
            }

            var targetEntry = await entryResolver.ResolveAsync(dslCall.Item1, cancellationToken);
            var synthesizer = new AstSynthesizer(entryResolver, llmService, nlTaskResolver, _signatures.Values);
            produceResults =
                await synthesizer.SynthesizeAsync(targetEntry, cancellationToken)
                    .ConfigureAwait(false);
        }
        else if (subtask.IsNlTask)
        {
            var nlTask = subtask.AsNlTask();
            var generatedEntry = await nlTaskResolver.ComposeAsync(
                nlTask.Item1,
                nlTask.Item2.Select(e => (e.Item1, e.Item2)).ToArray(), cancellationToken);
            var synthesizer = new AstSynthesizer(entryResolver, llmService, nlTaskResolver, _signatures.Values);
            produceResults = await synthesizer.SynthesizeAsync(generatedEntry, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Unknown subtask category");
        }

        return !produceResults.TryGetValue(productionKey, out var result)
            ? throw new InvalidOperationException($"Subtask not producing required key：{productionKey}")
            : result;
    }
}