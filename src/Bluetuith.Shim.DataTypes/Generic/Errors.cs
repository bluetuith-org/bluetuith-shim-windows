using System.Text;
using System.Text.Json;

namespace Bluetuith.Shim.DataTypes;

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
    protected static readonly JsonEncodedText ErrorText = JsonEncodedText.Encode("error");
    protected static readonly JsonEncodedText CodeText = JsonEncodedText.Encode("code");
    protected static readonly JsonEncodedText DescriptionText = JsonEncodedText.Encode(
        "description"
    );

    public ErrorCode Code { get; }

    public string Description { get; }

    public EventType Event => EventTypes.EventError;

    IEvent.EventAction IEvent.Action
    {
        get => IEvent.EventAction.Added;
        set => throw new InvalidDataException();
    }

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

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(ErrorText);

        writer.WriteNumber(CodeText, Code.Value);
        writer.WriteString(DescriptionText, Description);

        writer.WriteEndObject();
    }
}

public record class ErrorData : ErrorEvent, IError, IResult
{
    private static readonly JsonEncodedText MetadataText = JsonEncodedText.Encode("metadata");

    public Dictionary<string, object> Metadata { get; set; }

    public ErrorData(ErrorCode Code, string Description)
        : base(Code, Description) { }

    public new string ToConsoleString()
    {
        if (Code == Errors.ErrorNone.Code)
        {
            return "";
        }

        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine($"ERROR: {Description} ({Code})");

        if (Metadata?.Count > 0)
        {
            foreach ((var property, var value) in Metadata)
            {
                stringBuilder.AppendLine($"{property}: {value}");
            }
        }

        return stringBuilder.ToString();
    }

    public new void WriteJsonToStream(Utf8JsonWriter writer)
    {
        if (Code == Errors.ErrorNone.Code)
            return;

        writer.WriteStartObject(ErrorText);

        writer.WriteNumber(CodeText, Code.Value);
        writer.WriteString(DescriptionText, Description);

        if (Metadata?.Count > 0)
        {
            writer.WritePropertyName(MetadataText);
            Metadata.SerializeAll(writer, TypesSerializableContext.Default);
        }

        writer.WriteEndObject();
    }
}

public partial class Errors
{
    public static readonly ErrorData ErrorNone = new(Code: ErrorCode.ERROR_NONE, Description: "");

    public static readonly ErrorData ErrorUnexpected = new(
        Code: ErrorCode.ERROR_UNEXPECTED,
        Description: "An unexpected error occurred"
    );

    public static readonly ErrorData ErrorOperationCancelled = new(
        Code: ErrorCode.ERROR_OPERATION_CANCELLED,
        Description: "An operation was cancelled"
    );

    public static readonly ErrorData ErrorOperationInProgress = new(
        Code: ErrorCode.ERROR_OPERATION_IN_PROGRESS,
        Description: "The specified operation is in progress"
    );

    public static readonly ErrorData ErrorUnsupported = new(
        Code: ErrorCode.ERROR_UNSUPPORTED,
        Description: "This operation is unsupported"
    );
}

public static class ErrorExtensions
{
    public static ErrorData AddMetadata(this ErrorData e, string key, object value)
    {
        e.Metadata ??= [];
        if (!e.Metadata.TryAdd(key, value))
        {
            e.Metadata[key] = value;
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
