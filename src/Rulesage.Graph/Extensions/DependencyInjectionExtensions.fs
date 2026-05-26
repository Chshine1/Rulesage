namespace Rulesage.Graph.Extensions

open System.Runtime.CompilerServices
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Rulesage.Graph
open Rulesage.Graph.Services.Abstractions
open Rulesage.Graph.Services.Implementations

type ServiceCollectionExtensions =
    [<Extension>]
    static member AddGraphModule(services: IServiceCollection, config: IConfiguration) =
        services.Configure<GraphConfig>(config.GetSection("Graph")) |> ignore

        services
            .AddScoped<IStructureBuilder, StructureBuilder>()
            .AddScoped<IDescriptionCleaner, DescriptionCleaner>()
            .AddScoped<ISemanticGraphBuilder, SemanticGraphBuilder>()
            .AddScoped<IGraphFuser, GraphFuser>()
            .AddScoped<IGraphBuilder, GraphBuilder>()
            .AddScoped<ILabelPropagator, LabelPropagator>()
            .AddScoped<IGraphDotExporter, GraphDotExporter>()
        |> ignore

        services
