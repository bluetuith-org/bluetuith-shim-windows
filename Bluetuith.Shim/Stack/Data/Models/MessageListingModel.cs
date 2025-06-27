using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX.Map;

namespace Bluetuith.Shim.Stack.Data.Models;

public record class MessageListingModel : IResult
{
    private readonly List<MessageListing> messageList;

    public MessageListingModel(List<MessageListing> listing)
    {
        messageList = listing;
    }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        foreach (MessageListing message in messageList)
        {
            stringBuilder.AppendLine();

            stringBuilder.AppendLine(
                $"Message-Handle:{message.Handle} Type: {message.Type}, Size: {message.Size}, Date:{message.DateTime}"
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
        writer.WriteStartArray(DataSerializableContext.MessageListPropertyName);
        foreach (MessageListing message in messageList)
        {
            new Message(message).SerializeSelected(writer, DataSerializableContext.Default);
        }
        writer.WriteEndArray();
    }
}

internal class Message(MessageListing messageListing)
{
    [JsonPropertyName("handle")]
    public string Handle { get; set; } = messageListing.Handle;

    [JsonPropertyName("attachment_size")]
    public int AttachmentSize { get; set; } = messageListing.AttachmentSize;

    [JsonPropertyName("size")]
    public int Size { get; set; } = messageListing.Size;

    [JsonPropertyName("date_time")]
    public string DateTime { get; set; } = messageListing.DateTime;

    [JsonPropertyName("recipient_addressing")]
    public string RecipientAddressing { get; set; } = messageListing.RecipientAddressing;

    [JsonPropertyName("reception_status")]
    public string ReceptionStatus { get; set; } = messageListing.ReceptionStatus;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = messageListing.Subject;
}
