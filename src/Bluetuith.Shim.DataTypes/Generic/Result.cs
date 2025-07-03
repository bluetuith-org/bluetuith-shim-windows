using System.Text;
using System.Text.Json;

namespace Bluetuith.Shim.DataTypes;

public interface IResult
{
    public string ToConsoleString();
    public void WriteJsonToStream(Utf8JsonWriter writer);
}

public abstract record class Result : IResult
{
    public virtual string ToConsoleString()
    {
        return "";
    }

    public virtual void WriteJsonToStream(Utf8JsonWriter writer) { }
}

public record class GenericResult<T> : Result
{
    public Func<string> ConsoleFunc { get; set; }
    public Action<Utf8JsonWriter> JsonNodeFunc { get; set; }

    public GenericResult(Func<string> consoleFunc, Action<Utf8JsonWriter> jsonNodeFunc)
    {
        ConsoleFunc = consoleFunc;
        JsonNodeFunc = jsonNodeFunc;
    }

    public override string ToConsoleString()
    {
        return ConsoleFunc != null ? ConsoleFunc() : base.ToConsoleString();
    }

    public override void WriteJsonToStream(Utf8JsonWriter writer)
    {
        if (JsonNodeFunc != null)
        {
            JsonNodeFunc(writer);
        }
        else
        {
            base.WriteJsonToStream(writer);
        }
    }

    public static GenericResult<T> Empty()
    {
        return new GenericResult<T>(
            consoleFunc: delegate
            {
                return "";
            },
            jsonNodeFunc: delegate { }
        );
    }
}

public static class ResultExtensions
{
    public static GenericResult<List<string>> ToResult(
        this List<string> list,
        string consoleObjectName,
        string jsonObjectName
    )
    {
        return new GenericResult<List<string>>(
            consoleFunc: () =>
            {
                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine($"= {consoleObjectName} =");
                foreach (var item in list)
                {
                    stringBuilder.AppendLine(item);
                }

                return stringBuilder.ToString();
            },
            jsonNodeFunc: (writer) =>
            {
                writer.WritePropertyName(jsonObjectName);
                list.SerializeAll(writer);
            }
        );
    }

    public static GenericResult<T> ToResult<T>(
        this T value,
        string consoleObjectName,
        string jsonObjectName
    )
        where T : IConvertible
    {
        return new GenericResult<T>(
            consoleFunc: () =>
            {
                return $"{consoleObjectName}: {value}";
            },
            jsonNodeFunc: (writer) =>
            {
                writer.WritePropertyName(jsonObjectName);
                value.SerializeAll(writer);
            }
        );
    }
}
