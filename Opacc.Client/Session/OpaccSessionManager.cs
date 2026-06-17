using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opacc.Client.Session.Exceptions;
using OpaccWebservice;

namespace Opacc.Client.Session;

public class OpaccSessionManager : IOpaccSessionManager, IAsyncDisposable
{
    private readonly OpaccClientOptions _options;
    private readonly ILogger<OpaccSessionManager> _logger;

    // Pro Session-Key ein eigener Lock + Session
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Interner Wrapper: Session + Lock für thread-safe Zugriff
    /// </summary>
    private class SessionEntry
    {
        public OpaccSession? Session { get; set; }
        public SemaphoreSlim Lock { get; } = new(1, 1);
    }

    public OpaccSessionManager(IOptions<OpaccClientOptions> options, ILogger<OpaccSessionManager> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Periodischer Cleanup von idle User-Sessions
        _cleanupTimer = new Timer(
            callback: _ => _ = CleanupIdleSessionsAsync(),
            state: null,
            dueTime: _options.CleanupInterval,
            period: _options.CleanupInterval
        );
    }

    // ====================================================================
    // Public API
    // ====================================================================

    public async Task<OpaccSession> GetSessionAsync(SessionCredentials? credentials = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        credentials ??= SessionCredentials.Default;
        var entry = GetOrCreateEntry(credentials.Key);

        await entry.Lock.WaitAsync(ct);
        try
        {
            // Bestehende Session prüfen
            if (entry.Session != null)
            {
                if (IsExpired(entry.Session))
                {
                    _logger.LogInformation(
                        "Session expired for {Key} (created {Created}, age {Age}min)",
                        credentials.Key,
                        entry.Session.CreatedAtUtc,
                        (DateTime.UtcNow - entry.Session.CreatedAtUtc).TotalMinutes
                    );

                    await StopSessionSafeAsync(entry.Session);
                    entry.Session = null;
                }
                else
                {
                    // Session ist gültig
                    entry.Session.MarkUsed();
                    return entry.Session;
                }
            }

            // Neue Session erstellen
            entry.Session = await CreateSessionAsync(credentials);
            return entry.Session;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    public async Task InvalidateAsync(SessionCredentials? credentials = null)
    {
        credentials ??= SessionCredentials.Default;

        if (_sessions.TryGetValue(credentials.Key, out var entry))
        {
            await entry.Lock.WaitAsync();
            try
            {
                if (entry.Session != null)
                {
                    _logger.LogInformation("Invalidating session for {Key}", credentials.Key);
                    await StopSessionSafeAsync(entry.Session);
                    entry.Session = null;
                }
            }
            finally
            {
                entry.Lock.Release();
            }
        }
    }

    public async Task InvalidateAllAsync()
    {
        _logger.LogInformation("Invalidating all {Count} sessions", _sessions.Count);

        var tasks = _sessions
            .Keys.Select(key => InvalidateAsync(new SessionCredentials { UserId = key == "default" ? null : ParseUserIdFromKey(key) }))
            .ToList();

        await Task.WhenAll(tasks);
    }

    // ====================================================================
    // Session Lifecycle
    // ====================================================================

    private async Task<OpaccSession> CreateSessionAsync(SessionCredentials credentials)
    {
        // Pool-Limit prüfen (nur für User-Sessions, nicht für Default)
        if (credentials.UserId.HasValue)
        {
            var userSessionCount = _sessions.Count(s => s.Key != "default" && s.Value.Session != null);

            if (userSessionCount >= _options.MaxUserSessions)
            {
                _logger.LogWarning("Session pool limit reached ({Max}). Cleaning up idle sessions first.", _options.MaxUserSessions);

                await CleanupIdleSessionsAsync();

                // Nochmal prüfen nach Cleanup
                userSessionCount = _sessions.Count(s => s.Key != "default" && s.Value.Session != null);

                if (userSessionCount >= _options.MaxUserSessions)
                    throw new InvalidOperationException(
                        $"Maximum number of concurrent Opacc sessions ({_options.MaxUserSessions}) reached. " + "Try again later or increase MaxUserSessions."
                    );
            }
        }

        var client = new GenericClient(_options.ServiceUrl);
        var context = new RequestContext { ClientId = _options.ClientId, Consumer = _options.ApplicationId };

        ConfigureCredentials(context, credentials, client);

        try
        {
            context.Session = await client.StartSessionAsync(context);

            _logger.LogInformation("Created new Opacc session for {Key}: {SessionId}", credentials.Key, context.Session);

            return new OpaccSession
            {
                Client = client,
                Context = context,
                SessionKey = credentials.Key,
                CreatedAtUtc = DateTime.UtcNow,
                LastUsedAtUtc = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Opacc session for {Key} at {Url}", credentials.Key, _options.ServiceUrl);

            // WCF Client aufräumen bei Fehler
            try
            {
                client.Abort();
            }
            catch
            { /* ignore */
            }

            throw new OpaccSessionException($"Could not establish Opacc session for {credentials.Key}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Konfiguriert UserId und Password auf dem RequestContext
    /// basierend auf den drei Szenarien:
    /// 1. Default (technischer User)
    /// 2. User mit eigenem Passwort (z.B. Login)
    /// </summary>
    private void ConfigureCredentials(RequestContext context, SessionCredentials credentials, GenericClient client)
    {
        if (!credentials.UserId.HasValue)
        {
            // Fall 1: Default / technischer User
            context.UserId = _options.DefaultUserId;
            context.Password = _options.DefaultPassword;
        }
        else if (!string.IsNullOrWhiteSpace(credentials.Password))
        {
            // Fall 2: User mit eigenem Passwort
            context.UserId = credentials.UserId.Value.ToString();
            // Passwort wird synchron verschlüsselt — EncryptPasswordAsync ist
            // beim WCF Client tatsächlich synchron (blockiert nicht)
            context.Password = client.EncryptPasswordAsync(credentials.Password).GetAwaiter().GetResult();
        }
    }

    // ====================================================================
    // Cleanup
    // ====================================================================

    private async Task CleanupIdleSessionsAsync()
    {
        if (_disposed)
            return;

        var idleKeys = _sessions
            .Where(kvp =>
                kvp.Key != "default" // Default-Session nie cleanuppen
                && kvp.Value.Session != null
                && IsIdle(kvp.Value.Session)
            )
            .Select(kvp => kvp.Key)
            .ToList();

        if (idleKeys.Count == 0)
            return;

        _logger.LogInformation("Cleaning up {Count} idle user sessions", idleKeys.Count);

        foreach (var key in idleKeys)
        {
            if (_sessions.TryGetValue(key, out var entry))
            {
                // Try-Lock: wenn jemand die Session gerade nutzt, überspringen
                if (entry.Lock.Wait(0))
                {
                    try
                    {
                        if (entry.Session != null && IsIdle(entry.Session))
                        {
                            await StopSessionSafeAsync(entry.Session);
                            entry.Session = null;

                            // Entry komplett entfernen
                            _sessions.TryRemove(key, out _);

                            _logger.LogDebug("Removed idle session for {Key}", key);
                        }
                    }
                    finally
                    {
                        entry.Lock.Release();
                    }
                }
            }
        }
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private SessionEntry GetOrCreateEntry(string key)
    {
        return _sessions.GetOrAdd(key, _ => new SessionEntry());
    }

    private bool IsExpired(OpaccSession session)
    {
        return DateTime.UtcNow - session.CreatedAtUtc >= _options.SessionTimeout;
    }

    private bool IsIdle(OpaccSession session)
    {
        return DateTime.UtcNow - session.LastUsedAtUtc >= _options.UserSessionIdleTimeout;
    }

    private async Task StopSessionSafeAsync(OpaccSession session)
    {
        try
        {
            await session.Client.StopSessionAsync(session.Context);
            _logger.LogDebug("Stopped session {SessionId}", session.Context.Session);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop session {SessionId} cleanly — will be abandoned", session.Context.Session);

            // WCF Client in fehlerhaftem Zustand abbrechen
            try
            {
                session.Client.Abort();
            }
            catch
            { /* ignore */
            }
        }
    }

    private static int? ParseUserIdFromKey(string key)
    {
        // "user_123_auth" → 123
        var parts = key.Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var id))
            return id;
        return null;
    }

    // ====================================================================
    // Dispose
    // ====================================================================

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Timer stoppen
        await _cleanupTimer.DisposeAsync();

        // Alle Sessions beenden
        foreach (var entry in _sessions.Values)
        {
            if (entry.Session != null)
                await StopSessionSafeAsync(entry.Session);

            entry.Lock.Dispose();
        }

        _sessions.Clear();

        _logger.LogInformation("OpaccSessionManager disposed — all sessions stopped");
    }
}
