using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Session;

public record SessionCredentials
{
    public int? UserId { get; init; }
    public string? Password { get; init; }

    /// <summary>
    /// Cache-Key für den Session-Pool
    /// </summary>
    public string Key => UserId.HasValue ? $"user_{UserId}_{(Password != null ? "auth" : "delegated")}" : "default";

    public static SessionCredentials Default => new();

    public static SessionCredentials ForUser(int userId, string? password = null) => new() { UserId = userId, Password = password };
}
