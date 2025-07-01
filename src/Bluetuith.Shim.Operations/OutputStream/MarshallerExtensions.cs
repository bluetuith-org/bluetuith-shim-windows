using System.Text.Json;
using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Operations;

public static class MarshallerExtensions
{
    private static readonly JsonEncodedText StatusText = JsonEncodedText.Encode("status");
    private static readonly JsonEncodedText StatusErrorText = JsonEncodedText.Encode("error");
    private static readonly JsonEncodedText StatusOkText = JsonEncodedText.Encode("ok");
    private static readonly JsonEncodedText OperationIdText = JsonEncodedText.Encode(
        "operation_id"
    );
    private static readonly JsonEncodedText RequestIdText = JsonEncodedText.Encode("request_id");
    private static readonly JsonEncodedText DataText = JsonEncodedText.Encode("data");
    private static readonly JsonEncodedText EventIdText = JsonEncodedText.Encode("event_id");
    private static readonly JsonEncodedText EventActionText = JsonEncodedText.Encode(
        "event_action"
    );
    private static readonly JsonEncodedText EventText = JsonEncodedText.Encode("event");

    public static void WriteResult<T>(
        this T result,
        OperationToken token,
        bool isError,
        bool hasData,
        Utf8JsonWriter writer
    )
        where T : IResult
    {
        writer.WriteStartObject();
        if (isError)
            writer.WriteString(StatusText, StatusErrorText);
        else
            writer.WriteString(StatusText, StatusOkText);

        writer.WriteNumber(OperationIdText, token.OperationId);
        writer.WriteNumber(RequestIdText, token.RequestId);

        if (hasData)
        {
            if (!isError)
                writer.WriteStartObject(DataText);

            result.WriteJsonToStream(writer);

            if (!isError)
                writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    public static void WriteEvent<T>(this T eventData, Utf8JsonWriter writer)
        where T : IEvent
    {
        writer.WriteStartObject();

        writer.WriteNumber(EventIdText, eventData.Event.Value);
        writer.WriteString(EventActionText, eventData.Action.ToJsonEncodedText());

        writer.WriteStartObject(EventText);
        eventData.WriteJsonToStream(writer);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
