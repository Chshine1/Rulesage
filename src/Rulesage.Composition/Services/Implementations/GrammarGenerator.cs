using System.Text.Json;
using Rulesage.Common.Types.Composition;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Implementations;

public class GrammarGenerator(JsonSerializerOptions jsonOptions) : IGrammarGenerator
{
    public Task<Grammar> GenerateAsync(
        CompositionContext context,
        CancellationToken cancellationToken = default)
    {
        var operationIrs = context.nodes.Keys;
        var nodeIrs = context.operations.Keys;

        var schema = new
        {
            type = "object",
            properties = new
            {
                useDsls = new
                {
                    type = "array",
                    items = new { type = "string", @enum = operationIrs },
                    minItems = 1
                },
                context = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            key = new { type = "string" },
                            signatureSemanticName = new { type = "string", @enum = nodeIrs }
                        },
                        required = new[] { "key", "signatureSemanticName" }
                    }
                },
                produce = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            key = new { type = "string" },
                            ast = new
                            {
                                type = "object",
                                properties = new
                                {
                                    signatureSemanticName = new { type = "string", @enum = nodeIrs },
                                    parameters = new
                                    {
                                        type = "array",
                                        items = new
                                        {
                                            type = "object",
                                            properties = new
                                            {
                                                key = new { type = "string" },
                                                filling = new
                                                {
                                                    oneOf = new object[]
                                                    {
                                                        new
                                                        {
                                                            type = "object",
                                                            properties = new { leaf = new { type = "string" } },
                                                            required = new[] { "leaf" }
                                                        },
                                                        new
                                                        {
                                                            type = "object",
                                                            properties = new { astLiteral = new { type = "array" } },
                                                            required = new[] { "astLiteral" }
                                                        },
                                                        new
                                                        {
                                                            type = "object",
                                                            properties = new { fromContext = new { type = "string" } },
                                                            required = new[] { "fromContext" }
                                                        },
                                                        new
                                                        {
                                                            type = "object",
                                                            properties = new
                                                            {
                                                                fromSubtask = new
                                                                {
                                                                    type = "object",
                                                                    properties = new
                                                                    {
                                                                        subtaskKey = new { type = "string" },
                                                                        producedKey = new { type = "string" }
                                                                    }
                                                                }
                                                            },
                                                            required = new[] { "fromSubtask" }
                                                        }
                                                    }
                                                }
                                            },
                                            required = new[] { "key", "filling" }
                                        }
                                    }
                                },
                                required = new[] { "signatureSemanticName", "parameters" }
                            }
                        },
                        required = new[] { "key", "ast" }
                    }
                },
                subtasks = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            key = new { type = "string" },
                            task = new
                            {
                                oneOf = new object[]
                                {
                                    new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            dslCall = new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    dslSemanticName = new { type = "string", @enum = operationIrs },
                                                    context = new { type = "array" }
                                                },
                                                required = new[] { "dslSemanticName" }
                                            }
                                        },
                                        required = new[] { "dslCall" }
                                    },
                                    new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            nlTask = new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    taskTemplate = new { type = "string" },
                                                    expect = new
                                                    {
                                                        type = "array",
                                                        items = new
                                                        {
                                                            type = "object",
                                                            properties = new
                                                            {
                                                                key = new { type = "string" },
                                                                signatureSemanticName = new
                                                                    { type = "string", @enum = nodeIrs }
                                                            },
                                                            required = new[] { "key", "signatureSemanticName" }
                                                        }
                                                    }
                                                },
                                                required = new[] { "taskTemplate", "expect" }
                                            }
                                        },
                                        required = new[] { "nlTask" }
                                    }
                                }
                            }
                        },
                        required = new[] { "key", "task" }
                    }
                }
            },
            required = new[] { "useDsls", "context", "produce", "subtasks" }
        };

        var definition = JsonSerializer.Serialize(schema, jsonOptions);
        return Task.FromResult(new Grammar(definition));
    }
}