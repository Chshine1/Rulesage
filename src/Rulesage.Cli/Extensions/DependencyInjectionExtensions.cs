using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddHandlers()
        {
            collection.AddScoped<OperationsHandler>();
            collection.AddScoped<NodesHandler>();
            
            return collection;
        }
    }
}