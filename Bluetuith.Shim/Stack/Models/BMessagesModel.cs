using System.Text;
using System.Text.Json.Nodes;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX;
using MixERP.Net.VCards.Serializer;

namespace Bluetuith.Shim.Stack.Models;

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

    public (string, JsonNode) ToJsonNode()
    {
        JsonArray jsonArray = [];
        foreach (BMessage message in bMessageList)
        {
            jsonArray.Add(
                new JsonObject()
                {
                    ["status"] = message.Status.ToString(),
                    ["type"] = message.Type,
                    ["folder"] = message.Folder,
                    ["charset"] = message.Charset,
                    ["length"] = message.Length,
                    ["sender"] = message.Sender.Serialize(),
                    ["body"] = message.Body,
                }
            );
        }

        return ("bmessage_list", jsonArray.SerializeSelected());
    }
}
