using System.CommandLine;
using Opacc.Client.CLI.Commands;

var root = new RootCommand("Opacc CLI — Tooling für Opacc OX11");

root.AddCommand(ScaffoldCommand.Build());

return await root.InvokeAsync(args);
