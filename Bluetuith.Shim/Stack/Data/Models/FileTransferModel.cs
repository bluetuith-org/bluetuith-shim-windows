using System.Text;
using System.Text.Json;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Data.Events;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim.Stack.Data.Models;

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

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(DataSerializableContext.FileTransferPropertyName);
        (this as IFileTransfer).SerializeAll(writer, DataSerializableContext.Default);
    }
}
