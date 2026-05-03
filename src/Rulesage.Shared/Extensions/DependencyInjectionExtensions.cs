using System.Text.Json;
using System.Text.Json.Serialization;
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
        public IServiceCollection AddSharedModule(string dbConnectionString, string onnxModelPath, string vocabPath,
            JsonSerializerOptions? jsonOptions = null)
        {
            jsonOptions ??= new JsonSerializerOptions();
            jsonOptions.Converters.Add(new JsonFSharpConverter());
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
            collection.AddSingleton<IOperationIdfService, OperationIdfService>();
            collection.AddSingleton<IEmbeddingService>(sp =>
                new OnnxEmbeddingService(sp.GetRequiredService<Tokenizer>(), onnxModelPath));

            collection.AddSingleton(sp =>
                new NpgsqlDataSourceBuilder(dbConnectionString)
                    .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>()).Build());

            collection.AddScoped<INodeRepository, NodeRepository>();
            collection.AddScoped<IOperationRepository, OperationRepository>();

            return collection;
        }
    }
}