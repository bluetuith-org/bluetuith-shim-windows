using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Operations;

public static class CommandExecutor
{
    public static ErrorData Run(string[] args)
    {
        OperationToken token = OperationManager.GenerateToken(0);

        OperationManager.ExecuteHandler(token, args);
        OperationManager.WaitForExtendedOperations();

        return Errors.ErrorNone;
    }

    public static Task StopAsync()
    {
        return OperationManager.CancelAllAsync();
    }
}

public static class ExecutorErrors
{
    public static ErrorData ErrorParsingCommand = new(
        Code: new("ERROR_PARSING_COMMAND", -10000),
        Description: "An error occurred while parsing the command-line arguments"
    );
}
