namespace Rulesage.Graph.Extensions

open System.Runtime.CompilerServices
open Microsoft.Extensions.DependencyInjection
open Rulesage.Common.Repositories.Abstractions
open Rulesage.Common.Repositories.Implementations

type ServiceCollectionExtensions =
    [<Extension>]
    static member AddInfrastructure(services: IServiceCollection) =
        services
            .AddScoped<IRecordRepository, RecordRepository>()
            .AddScoped<IActionRepository, ActionRepository>()
            .AddScoped<IRuleRepository, RuleRepository>()
            .AddScoped<ICommunityRepository, CommunityRepository>()
        |> ignore

        services
