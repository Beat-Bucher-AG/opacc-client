using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Operations.Exceptions;

/// <summary>
/// Exception für Opacc Business-Fehler (z.B. "BO nicht gefunden", "Storno gesperrt" etc.)
/// </summary>
public class OpaccRequestException : Exception
{
    public string MessageId { get; }

    public OpaccRequestException(string messageId, string message)
        : base($"Opacc Error [{messageId}]: {message}")
    {
        MessageId = messageId;
    }
}
