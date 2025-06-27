using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX;
using MixERP.Net.VCards.Serializer;

namespace Bluetuith.Shim.Stack.Data.Models;

public record class BMessagesModel : IResult
{
    private readonly List<BMessage> bMessageList;

    public BMessagesModel(List<BMessage> listing)
    {
        bMessageList = listing;
    }

    public BMessagesModel(BMessage message)
    {
        bMessageList = [message];
    }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        foreach (BMessage message in bMessageList)
        {
            stringBuilder.AppendLine();

            if (message.Sender != null)
            {
                stringBuilder.AppendLine(
                    $"Sender: {message.Sender.FirstName} {message.Sender.LastName}"
                );
            }

            stringBuilder.AppendLine($"Folder: {message.Folder}, Status: {message.Status}");
            stringBuilder.AppendLine($"{message.Body}");
        }

        return stringBuilder.ToString();
    }

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WriteStartArray(DataSerializableContext.BMessagePropertyName);
        foreach (BMessage message in bMessageList)
        {
            new BMessageItem(message).SerializeSelected(writer, DataSerializableContext.Default);
        }
        writer.WriteEndArray();
    }
}

internal class BMessageItem(BMessage bMessage)
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = bMessage.Status.ToString();

    [JsonPropertyName("type")]
    public string Type { get; set; } = bMessage.Type;

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = bMessage.Folder;

    [JsonPropertyName("charset")]
    public string Charset { get; set; } = bMessage.Charset;

    [JsonPropertyName("length")]
    public int Length { get; set; } = bMessage.Length;

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = bMessage.Sender.Serialize();

    [JsonPropertyName("body")]
    public string Body { get; set; } = bMessage.Body;
}
