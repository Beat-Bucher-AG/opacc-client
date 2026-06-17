using System.Runtime.CompilerServices;

namespace Opacc.Client;

/// <summary>
/// Associates loaded model instances with the IOpaccClient that fetched them.
/// Uses ConditionalWeakTable so entries are GC'd together with the model — no leaks.
/// </summary>
internal static class ModelClientRegistry
{
    private static readonly ConditionalWeakTable<IOpaccModel, IOpaccClient> _table = new();

    internal static void Associate(IOpaccModel model, IOpaccClient client)
        => _table.AddOrUpdate(model, client);

    internal static IOpaccClient? GetClient(IOpaccModel model)
        => _table.TryGetValue(model, out var client) ? client : OpaccClientContext.Current;
}
