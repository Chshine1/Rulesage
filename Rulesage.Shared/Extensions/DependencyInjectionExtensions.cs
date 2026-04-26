using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Rulesage.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddSharedModule(JsonSerializerOptions? options = null)
        {
            options ??= new JsonSerializerOptions();
            options.Converters.Add(new JsonFSharpConverter());
            collection.AddSingleton(options);

            return collection;
        }
    }
}