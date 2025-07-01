using System.Text;
using System.Text.Json;

namespace Bluetuith.Shim.DataTypes;

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
        writer.WritePropertyName(ModelEventSerializableContext.FileTransferPropertyName);
        (this as IFileTransfer).SerializeAll(writer, ModelEventSerializableContext.Default);
    }
}
