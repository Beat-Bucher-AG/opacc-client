using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Opacc.Client.Session;
using Opacc.Client.Transport;

namespace Opacc.Client.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpaccClient(this IServiceCollection services, Action<OpaccClientOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IOpaccSessionManager, OpaccSessionManager>();
        services.AddScoped<IOpaccTransport, OpaccTransport>();
        services.AddScoped<IOpaccClient>(sp =>
        {
            var client = new OpaccClient(sp.GetRequiredService<IOpaccTransport>());
            // Make this scoped instance available as ambient context so models
            // created with new() can call .DeleteAsync() without passing the client.
            OpaccClientContext.Current = client;
            return client;
        });
        return services;
    }
}
