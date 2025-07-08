using Bluetuith.Shim.DataTypes;
using ConsoleAppFramework;
using DotNext;

namespace Bluetuith.Shim.Operations;

public static class CommandParser
{
    private static readonly ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();
    private static readonly UserDataSlot<OperationToken> slot = new();

    internal sealed class ParserFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
    {
        public override async Task InvokeAsync(
            ConsoleAppContext context,
            CancellationToken cancellationToken
        )
        {
            OperationToken token = OperationToken.None;
            int exitCode = 0;

            try
            {
                if (!context.Arguments.GetUserData().TryGet(slot, out token))
                    throw new InvalidDataException(
                        "Token was not found from the attached data slot"
                    );

                context = context with { State = token };

                await Next.InvokeAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                exitCode = 1;

                Output.Error(
                    ExecutorErrors.ErrorParsingCommand.AddMetadata("exception", ex.Message),
                    token
                );
            }
            finally
            {
                OperationManager.RemoveToken(token);

                if (!Output.IsOnSocket)
                    Environment.ExitCode = exitCode;
            }
        }
    }

    static CommandParser()
    {
        app.UseFilter<ParserFilter>();

        if (Output.IsOnSocket)
        {
            ConsoleApp.Log = static delegate { };
            ConsoleApp.LogError = static msg =>
                Output.Event(
                    Errors.ErrorUnexpected.AddMetadata("command_error", msg),
                    OperationToken.None
                );
        }
    }

    public static void Parse(OperationToken token, string[] args)
    {
        args.GetUserData().Set(slot, token);

        app.Run(args);
    }
}
