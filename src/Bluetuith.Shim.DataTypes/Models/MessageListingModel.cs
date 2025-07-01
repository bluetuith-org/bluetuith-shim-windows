using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bluetuith.Shim.DataTypes;

public record class MessageListingModel : IResult
{
    private readonly List<MessageItem> messageList;

    public MessageListingModel(List<MessageItem> listing)
    {
        messageList = listing;
    }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        foreach (MessageItem message in messageList)
        {
            stringBuilder.AppendLine();

            stringBuilder.AppendLine(
                $"Message-Handle:{message.Handle} Size: {message.Size}, Date:{message.DateTime}"
            );
            stringBuilder.AppendLine(
                $"Recipient: {message.RecipientAddressing}, Status: {message.ReceptionStatus}"
            );
            stringBuilder.AppendLine($"Subject: {message.Subject}");

            _ =
                message.AttachmentSize > 0
                    ? stringBuilder.AppendLine(
                        $"Attachments: yes, {message.AttachmentSize / 1024} kb"
                    )
                    : stringBuilder.AppendLine($"Attachments: no");
        }

        return stringBuilder.ToString();
    }

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WriteStartArray(ModelEventSerializableContext.MessageListPropertyName);
        foreach (MessageItem message in messageList)
        {
            message.SerializeSelected(writer, ModelEventSerializableContext.Default);
        }
        writer.WriteEndArray();
    }
}

public class MessageItem
{
    [JsonPropertyName("handle")]
    public string Handle { get; set; }

    [JsonPropertyName("attachment_size")]
    public int AttachmentSize { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("date_time")]
    public string DateTime { get; set; }

    [JsonPropertyName("recipient_addressing")]
    public string RecipientAddressing { get; set; }

    [JsonPropertyName("reception_status")]
    public string ReceptionStatus { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; }
}
