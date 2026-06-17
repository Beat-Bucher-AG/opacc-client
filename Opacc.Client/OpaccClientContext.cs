namespace Opacc.Client;

/// <summary>
/// Ambient per-async-context IOpaccClient.
/// Set automatically when OpaccClient is constructed (once per DI scope / request).
/// Allows models created with new() to call .DeleteAsync() without explicitly passing the client.
/// </summary>
public static class OpaccClientContext
{
    private static readonly AsyncLocal<IOpaccClient?> _current = new();

    /// <summary>The IOpaccClient active in the current async context.</summary>
    public static IOpaccClient? Current
    {
        get => _current.Value;
        internal set => _current.Value = value;
    }
}
