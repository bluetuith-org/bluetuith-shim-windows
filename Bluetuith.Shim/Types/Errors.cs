using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bluetuith.Shim.Types;

public interface IError : IErrorEvent
{
    public Dictionary<string, object> Metadata { get; }
}

public interface IErrorEvent : IEvent
{
    public ErrorCode Code { get; }
    public string Description { get; }
}

public record class ErrorCode
{
    public static ErrorCode ERROR_NONE = new("ERROR_NONE", 0);
    public static ErrorCode ERROR_UNEXPECTED = new("ERROR_UNEXPECTED", -1);
    public static ErrorCode ERROR_OPERATION_CANCELLED = new("ERROR_OPERATION_CANCELLED", -2);
    public static ErrorCode ERROR_OPERATION_IN_PROGRESS = new("ERROR_OPERATION_IN_PROGRESS", -3);
    public static ErrorCode ERROR_UNSUPPORTED = new("ERROR_UNSUPPORTED", -100);

    public string Name { get; }
    public int Value { get; }

    public ErrorCode(string name, int code)
    {
        Name = name;
        Value = code;
    }

    public sealed override string ToString()
    {
        return @$"{Name} ({Value})";
    }
}

public record class ErrorEvent : IErrorEvent, IEvent
{
    public ErrorCode Code { get; }
    public string Description { get; }

    public EventType Event => EventTypes.EventError;

    public IEvent.EventAction Action => IEvent.EventAction.Added;

    public ErrorEvent(ErrorCode Code, string Description)
    {
        this.Code = Code;
        this.Description = Description;
    }

    public string ToConsoleString()
    {
        if (Code == Errors.ErrorNone.Code)
        {
            return "";
        }

        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine($"ERROR: {Description} ({Code})");

        return stringBuilder.ToString();
    }

    public (string, JsonNode) ToJsonNode()
    {
        return Code == Errors.ErrorNone.Code
            ? ("error", [])
            : (
                "error",
                new JsonObject()
                {
                    ["code"] = Code.Value,
                    ["name"] = Code.Name,
                    ["description"] = Description,
                }
            );
    }
}

public record class ErrorData : ErrorEvent, IError, IResult
{
    public Dictionary<string, object> Metadata { get; }

    public ErrorData(ErrorCode Code, string Description, Dictionary<string, object> Metadata)
        : base(Code, Description)
    {
        this.Metadata = Metadata;
    }

    public new string ToConsoleString()
    {
        if (Code == Errors.ErrorNone.Code)
        {
            return "";
        }

        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine($"ERROR: {Description} ({Code})");
        foreach ((var property, var value) in Metadata)
        {
            stringBuilder.AppendLine($"{property}: {value}");
        }

        return stringBuilder.ToString();
    }

    public new (string, JsonNode) ToJsonNode()
    {
        return Code == Errors.ErrorNone.Code
            ? ("error", [])
            : (
                "error",
                new JsonObject()
                {
                    ["code"] = Code.Value,
                    ["name"] = Code.Name,
                    ["description"] = Description,
                    ["metadata"] = JsonSerializer.SerializeToNode(Metadata),
                }
            );
    }
}

public partial class Errors
{
    public static readonly ErrorData ErrorNone = new(
        Code: ErrorCode.ERROR_NONE,
        Description: "",
        Metadata: []
    );

    public static readonly ErrorData ErrorUnexpected = new(
        Code: ErrorCode.ERROR_UNEXPECTED,
        Description: "An unexpected error occurred",
        Metadata: new() { { "exception", "" } }
    );

    public static readonly ErrorData ErrorOperationCancelled = new(
        Code: ErrorCode.ERROR_OPERATION_CANCELLED,
        Description: "An operation was cancelled",
        Metadata: new() { { "operation", "" } }
    );

    public static readonly ErrorData ErrorOperationInProgress = new(
        Code: ErrorCode.ERROR_OPERATION_IN_PROGRESS,
        Description: "The specified operation is in progress",
        Metadata: new() { { "operation", "" } }
    );

    public static readonly ErrorData ErrorUnsupported = new(
        Code: ErrorCode.ERROR_UNSUPPORTED,
        Description: "This operation is unsupported",
        Metadata: new() { { "exception", "" } }
    );
}

public static class ErrorExtensions
{
    public static ErrorData AddMetadata(this ErrorData e, string key, object value)
    {
        if (e.Metadata.ContainsKey(key))
        {
            e.Metadata[key] = value;
        }
        else
        {
            e.Metadata.Add(key, value);
        }

        return e;
    }

    public static ErrorData WrapError(this ErrorData e, Dictionary<string, object> dict)
    {
        foreach ((var property, var value) in dict)
        {
            e.AddMetadata(property, value);
        }

        return e;
    }
}

[Serializable]
public class ErrorException
{
    public string Message { get; } = "";
    public System.Collections.IDictionary ExceptionMetadata { get; }

    public ErrorException(Exception inner)
    {
        Message = inner.Message;
        ExceptionMetadata = inner.Data;
    }
}
