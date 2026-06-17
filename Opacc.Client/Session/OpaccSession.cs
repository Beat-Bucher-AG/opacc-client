using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpaccWebservice;

namespace Opacc.Client.Session;

public class OpaccSession
{
    public GenericClient Client { get; init; } = null!;
    public RequestContext Context { get; init; } = null!;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastUsedAtUtc { get; set; } = DateTime.UtcNow;
    public string SessionKey { get; init; } = "";

    public void MarkUsed() => LastUsedAtUtc = DateTime.UtcNow;
}
