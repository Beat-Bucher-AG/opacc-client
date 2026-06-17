using Microsoft.Extensions.DependencyInjection;
using Opacc.Client.Transport;

namespace Opacc.Client.Sentry.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Sentry performance tracing for all Opacc operations by decorating
    /// the registered <see cref="IOpaccTransport"/> with <see cref="SentryOpaccTransport"/>.
    /// Must be called after <c>services.AddOpaccClient()</c>.
    /// </summary>
    public static IServiceCollection AddOpaccSentry(this IServiceCollection services)
    {
        var innerDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(IOpaccTransport))
            ?? throw new InvalidOperationException(
                "IOpaccTransport is not registered. Call AddOpaccClient() before AddOpaccSentry().");

        services.Remove(innerDescriptor);

        services.Add(ServiceDescriptor.Describe(
            typeof(IOpaccTransport),
            sp => new SentryOpaccTransport(ResolveInner(sp, innerDescriptor)),
            innerDescriptor.Lifetime));

        return services;
    }

    private static IOpaccTransport ResolveInner(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IOpaccTransport instance)
            return instance;

        if (descriptor.ImplementationFactory is { } factory)
            return (IOpaccTransport)factory(sp);

        return (IOpaccTransport)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
    }
}
