using System.CommandLine;
using Bluetuith.Shim.DataTypes;
using DotMake.CommandLine;

namespace Bluetuith.Shim.Operations;

public static class CommandParser
{
    private static readonly CliSettings _settings = new() { Output = TextWriter.Null };

    public static ParseResult Parse(OperationToken token, string[] args, bool noconsole)
    {
        args =
        [
            .. new string[]
            {
                "--operation-id",
                token.OperationId.ToString(),
                "--request-id",
                token.RequestId.ToString(),
            },
            .. args,
        ];

        CliSettings settings = null;
        if (noconsole)
            settings = _settings;

        return Cli.Parse<CommandDefinitions>(args, settings);
    }

    public static ParseResult ParseWith<T>(string[] args)
    {
        return Cli.Parse<T>(args);
    }
}
