using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.Tokenizers;
using Rulesage.Retrieval.Options;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Retrieval.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddOperationRetrieval(
            Action<RetrievalOptions>? configureOptions = null)
        {
            collection.Configure(configureOptions ?? (_ => { }));

            collection.AddScoped<IOperationRetrievalService, OperationRetrievalService>();

            return collection;
        }
    }
}