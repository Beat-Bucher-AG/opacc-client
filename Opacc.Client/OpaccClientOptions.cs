using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client;

public class OpaccClientOptions
{
    /// <summary>
    /// URL des Opacc ServiceBus WebService (WCF Endpoint)
    /// </summary>
    public string ServiceUrl { get; set; } = "";

    /// <summary>
    /// Opacc Mandant / Client ID
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Application ID / Consumer Name
    /// </summary>
    public string ApplicationId { get; set; } = "";

    /// <summary>
    /// Default Opacc User ID (wird verwendet wenn kein spezifischer User angegeben)
    /// </summary>
    public string DefaultUserId { get; set; } = "";

    /// <summary>
    /// Default Opacc Passwort (Klartext — wird NICHT verschlüsselt,
    /// da der Default-User ein technischer User ist)
    /// </summary>
    public string DefaultPassword { get; set; } = "";

    /// <summary>
    /// Session Timeout — sollte unter dem Opacc-Server-Timeout liegen (Standard: 60 Min)
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Maximale Anzahl gleichzeitiger User-Sessions im Pool.
    /// Verhindert Ressourcen-Erschöpfung bei vielen parallelen Usern.
    /// </summary>
    public int MaxUserSessions { get; set; } = 50;

    /// <summary>
    /// Wie lange eine User-Session nach letzter Nutzung im Pool bleibt,
    /// bevor sie beim nächsten Cleanup entfernt wird.
    /// </summary>
    public TimeSpan UserSessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Intervall für den Cleanup-Timer der idle User-Sessions entfernt.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
}
