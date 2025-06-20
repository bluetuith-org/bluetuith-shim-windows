using System.CommandLine;
using System.CommandLine.Parsing;
using System.Security.Principal;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim.Executor.Command;

public static class CommandExecutor
{
    public static async Task<ErrorData> RunAsync(string[] args)
    {
        OperationToken token = OperationManager.GenerateToken(0);

        ParseResult parsed = CommandParser.Parse(token, args, false);
        if (args == null || args != null && args.Length == 0)
            goto execute;

        foreach (Token parsedToken in parsed.Tokens)
        {
            if (parsedToken.Type == TokenType.Command && parsedToken.Value.ToString() == "server")
                goto execute;

            if (parsedToken.Type != TokenType.Option)
                continue;

            switch (parsedToken.Value.ToString())
            {
                case "-v":
                case "--version":
                case "-h":
                case "--help":
                    goto execute;

                default:
                    continue;
            }
        }
        if (parsed.Errors.Count > 0)
        {
            return ExecutorErrors.ErrorParsingCommand.WrapError(
                new() { { "exception", parsed.Errors[0].Message } }
            );
        }

        if (!IsAdministrator())
        {
            Console.WriteLine("Error: This program must be run as Administrator.");
            Environment.Exit(1);
        }

        execute:
        await OperationManager.ExecuteHandlerAsync(token, parsed);
        OperationManager.WaitForExtendedOperations();

        return Errors.ErrorNone;
    }

    public static Task StopAsync()
    {
        return OperationManager.CancelAllAsync();
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

internal static class ExecutorErrors
{
    public static ErrorData ErrorParsingCommand = new(
        Code: new("ERROR_PARSING_COMMAND", -10000),
        Description: "An error occurred while parsing the command-line arguments",
        Metadata: new() { { "exception", "" } }
    );
}
