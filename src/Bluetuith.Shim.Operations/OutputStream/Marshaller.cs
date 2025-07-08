using System.Text.Json;
using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Operations;

internal interface IMarshaller
{
    void Write(OperationToken token, Utf8JsonWriter writer);
}

internal readonly ref struct ResultMarshaller<T>(T result, bool hasData, bool isError) : IMarshaller
    where T : IResult
{
    private static readonly JsonEncodedText StatusText = JsonEncodedText.Encode("status");
    private static readonly JsonEncodedText StatusErrorText = JsonEncodedText.Encode("error");
    private static readonly JsonEncodedText StatusOkText = JsonEncodedText.Encode("ok");
    private static readonly JsonEncodedText OperationIdText = JsonEncodedText.Encode(
        "operation_id"
    );
    private static readonly JsonEncodedText RequestIdText = JsonEncodedText.Encode("request_id");
    private static readonly JsonEncodedText DataText = JsonEncodedText.Encode("data");

    void IMarshaller.Write(OperationToken token, Utf8JsonWriter writer)
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
}

internal readonly ref struct EventMarshaller<T>(T eventData) : IMarshaller
    where T : IEvent
{
    private static readonly JsonEncodedText EventIdText = JsonEncodedText.Encode("event_id");
    private static readonly JsonEncodedText EventActionText = JsonEncodedText.Encode(
        "event_action"
    );
    private static readonly JsonEncodedText EventText = JsonEncodedText.Encode("event");

    void IMarshaller.Write(OperationToken token, Utf8JsonWriter writer)
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
