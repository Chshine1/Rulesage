using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Extensions;

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.Members)]
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddHandlers()
        {
            collection.AddScoped<RulesHandler>();
            collection.AddScoped<NodesHandler>();
            
            return collection;
        }
    }
}