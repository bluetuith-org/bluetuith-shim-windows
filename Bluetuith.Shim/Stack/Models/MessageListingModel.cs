using System.Text;
using System.Text.Json.Nodes;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX.Map;

namespace Bluetuith.Shim.Stack.Models;

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

    public (string, JsonNode) ToJsonNode()
    {
        JsonArray jsonArray = [];
        foreach (MessageListing message in messageList)
        {
            jsonArray.Add(
                new JsonObject()
                {
                    ["handle"] = message.Handle,
                    ["type"] = message.Type,
                    ["size"] = message.Size,
                    ["attachmentSize"] = message.AttachmentSize,
                    ["date"] = message.DateTime,
                    ["recipientAddressing"] = message.RecipientAddressing,
                    ["receptionStatus"] = message.ReceptionStatus,
                    ["subject"] = message.Subject,
                }
            );
        }

        return ("message_list", jsonArray.SerializeSelected());
    }
}
