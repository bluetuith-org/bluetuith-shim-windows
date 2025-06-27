using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using MixERP.Net.VCards;
using MixERP.Net.VCards.Models;

namespace Bluetuith.Shim.Stack.Data.Models;

public interface IVcard
{
    [JsonPropertyName("vcard_type")]
    string VCardType { get; }

    [JsonPropertyName("vcard_data")]
    string VCards { get; }
}

public record class VcardModel : IResult, IVcard
{
    public string VCardType { get; } = "";
    public string VCards { get; } = "";

    public VcardModel() { }

    public VcardModel(string vcardType, string vcards)
    {
        VCardType = vcardType;
        VCards = vcards;
    }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();

        stringBuilder.AppendLine($" = {VCardType} = ");

#nullable enable
        foreach (VCard? card in Deserializer.GetVCards(VCards))
        {
            if (card == null)
                continue;

            stringBuilder.AppendLine($"# {card.FormattedName}:");
            stringBuilder.AppendLine("Telephones:");
            if (card.Telephones != null)
            {
                foreach (Telephone? telephone in card.Telephones)
                {
                    if (telephone == null)
                        continue;

                    stringBuilder.AppendLine($"{telephone.Number}");
                }
            }

            stringBuilder.AppendLine("");
        }
#nullable disable

        return stringBuilder.ToString();
    }

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(DataSerializableContext.VcardPropertyName);
        (this as IVcard).SerializeSelected(writer, DataSerializableContext.Default);
    }
}
