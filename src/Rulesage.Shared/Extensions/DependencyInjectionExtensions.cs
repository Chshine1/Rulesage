using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using Npgsql;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Repositories.Implementations;
using Rulesage.Shared.Services.Abstractions;
using Rulesage.Shared.Services.Implementations;

namespace Rulesage.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddSharedModule(string dbConnectionString, string onnxModelPath, string vocabPath)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };
            jsonOptions.Converters.Add(new JsonFSharpConverter());
            jsonOptions.MakeReadOnly();
            collection.AddSingleton(jsonOptions);

            collection.AddSingleton<Tokenizer>(WordPieceTokenizer.Create(vocabPath,
                new WordPieceOptions
                {
                    SpecialTokens = new Dictionary<string, int>
                    {
                        ["[PAD]"] = 0,
                        ["[UNK]"] = 100,
                        ["[CLS]"] = 101,
                        ["[SEP]"] = 102,
                        ["[MASK]"] = 103
                    }
                }));
            collection.AddSingleton<IRuleIdfService, RuleIdfService>();
            collection.AddSingleton<IEmbeddingService>(sp =>
                new OnnxEmbeddingService(sp.GetRequiredService<Tokenizer>(), onnxModelPath));
            collection.AddSingleton<ILlmService, OpenAiCompatibleService>();

            collection.AddSingleton(sp =>
            {
                var builder =
                    new NpgsqlDataSourceBuilder(dbConnectionString).UseLoggerFactory(
                        sp.GetRequiredService<ILoggerFactory>());
                builder.UseVector();
                return builder.Build();
            });

            collection.AddScoped<IRecordRepository, RecordRepository>();
            collection.AddScoped<IRuleRepository, RuleRepository>();

            return collection;
        }
    }
}