using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpaccWebservice;

namespace Opacc.Client.Session;

public interface IOpaccSessionManager : IAsyncDisposable
{
    /// <summary>
    /// Gibt eine gültige Session zurück. Erstellt automatisch eine neue,
    /// falls keine existiert oder die bestehende abgelaufen ist.
    /// </summary>
    Task<OpaccSession> GetSessionAsync(SessionCredentials? credentials = null, CancellationToken ct = default);

    /// <summary>
    /// Invalidiert eine spezifische Session (z.B. nach einem Fehler).
    /// Die nächste Anfrage erstellt automatisch eine neue.
    /// </summary>
    Task InvalidateAsync(SessionCredentials? credentials = null);

    /// <summary>
    /// Invalidiert alle Sessions und stoppt den Cleanup-Timer.
    /// </summary>
    Task InvalidateAllAsync();
}
