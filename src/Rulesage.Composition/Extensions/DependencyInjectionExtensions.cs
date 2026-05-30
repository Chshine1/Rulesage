using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Services.Implementations;

namespace Rulesage.Composition.Extensions;

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.Members)]
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddCompositionModule()
        {
            collection.AddScoped<ICompositionContextBuilder, CompositionContextBuilder>();
            collection.AddScoped<IPlanner, Planner>();
            
            return collection;
        }
    }
}