using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Session.Exceptions;

public class OpaccSessionException : Exception
{
    public OpaccSessionException(string message)
        : base(message) { }

    public OpaccSessionException(string message, Exception inner)
        : base(message, inner) { }
}
