using Microsoft.Extensions.DependencyInjection;
using Rulesage.Retrieval.Options;

namespace Rulesage.Retrieval.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddOperationRetrieval(
            Action<RetrievalOptions>? configureOptions = null)
        {
            collection.Configure(configureOptions ?? (_ => { }));

            collection.AddScoped<IRuleRetrievalService, RuleRetrievalService>();

            return collection;
        }
    }
}