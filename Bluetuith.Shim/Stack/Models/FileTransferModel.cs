using System.Text;
using System.Text.Json.Nodes;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim.Stack.Models;

public interface IFileTransfer : IFileTransferEvent { }

public abstract record class FileTransferBaseModel : FileTransferEventBaseModel, IFileTransfer { }

public record class FileTransferModel : FileTransferBaseModel, IResult
{
    public FileTransferModel() { }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();

        AppendEventProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public (string, JsonNode) ToJsonNode()
    {
        return ("file_transfer", (this as IFileTransfer).SerializeAll());
    }
}
