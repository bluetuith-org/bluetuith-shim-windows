using Bluetuith.Shim.DataTypes;
using GoodTimeStudio.MyPhone.OBEX;
using GoodTimeStudio.MyPhone.OBEX.Map;

namespace Bluetuith.Shim.Stack.Microsoft;

internal static class WindowsModelExtensions
{
    internal static BMessagesModel ToBMessage(this BMessage bMessage)
    {
        return new BMessagesModel(bMessage.ToBMessageItem());
    }

    internal static BMessagesModel ToBMessageList(this List<BMessage> bMessages)
    {
        var list = new List<BMessageItem>();
        foreach (var bMessage in bMessages)
            list.Add(bMessage.ToBMessageItem());

        return new BMessagesModel(list);
    }

    internal static BMessageItem ToBMessageItem(this BMessage bMessage)
    {
        return new BMessageItem()
        {
            Status = bMessage.Status.ToString(),
            Body = bMessage.Body,
            Charset = bMessage.Charset,
            Folder = bMessage.Folder,
            Length = bMessage.Length,
            Sender = bMessage.Sender.Title,
            Type = bMessage.Type,
        };
    }

    internal static MessageReceivedEvent ToMessageEvent(
        this MessageReceivedEventArgs args,
        string address
    )
    {
        return new MessageReceivedEvent()
        {
            Address = address,
            Folder = args.Folder,
            Handle = args.MessageHandle,
            MessageEventType = args.EventType,
            MessageType = args.MessageType,
        };
    }

    internal static MessageListingModel ToMessageListing(this List<MessageListing> messages)
    {
        var list = new List<MessageItem>();
        foreach (var message in messages)
            list.Add(message.ToMessageItem());

        return new MessageListingModel(list);
    }

    internal static MessageItem ToMessageItem(this MessageListing message)
    {
        return new MessageItem()
        {
            AttachmentSize = message.AttachmentSize,
            RecipientAddressing = message.RecipientAddressing,
            DateTime = message.DateTime,
            Handle = message.Handle,
            ReceptionStatus = message.ReceptionStatus,
            Size = message.Size,
            Subject = message.Subject,
        };
    }
}
