using System.CommandLine;
using Bluetuith.Shim.Executor.Operations;
using DotMake.CommandLine;

namespace Bluetuith.Shim.Executor.Command;

public static class CommandParser
{
    public static ParseResult Parse(OperationToken token, string[] args, bool noconsole)
    {
        var argslist = args.ToList();
        argslist.InsertRange(
            0,
            [
                "--operation-id",
                token.OperationId.ToString(),
                "--request-id",
                token.RequestId.ToString(),
            ]
        );

        CliSettings settings = null;
        if (noconsole)
            settings = new CliSettings() { Output = TextWriter.Null };

        return Cli.Parse<CommandDefinitions>([.. argslist], settings);
    }

    public static ParseResult ParseWith<T>(string[] args)
    {
        return Cli.Parse<T>(args);
    }
}
