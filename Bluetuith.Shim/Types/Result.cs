using System.Text;
using System.Text.Json.Nodes;
using Bluetuith.Shim.Extensions;

namespace Bluetuith.Shim.Types;

public interface IResult
{
    public string ToConsoleString();
    public (string, JsonNode) ToJsonNode();
}

public abstract record class Result : IResult
{
    public virtual string ToConsoleString()
    {
        return "";
    }

    public virtual (string, JsonNode) ToJsonNode()
    {
        return ("", (JsonObject)[]);
    }
}

public record class GenericResult<T> : Result
{
    public Func<string> ConsoleFunc { get; set; }
    public Func<(string, JsonNode)> JsonNodeFunc { get; set; }

    public GenericResult(Func<string> consoleFunc, Func<(string, JsonNode)> jsonNodeFunc)
    {
        ConsoleFunc = consoleFunc;
        JsonNodeFunc = jsonNodeFunc;
    }

    public override string ToConsoleString()
    {
        return ConsoleFunc != null ? ConsoleFunc() : base.ToConsoleString();
    }

    public override (string, JsonNode) ToJsonNode()
    {
        return JsonNodeFunc != null ? JsonNodeFunc() : base.ToJsonNode();
    }

    public static GenericResult<T> Empty()
    {
        return new GenericResult<T>(
            consoleFunc: () =>
            {
                return "";
            },
            jsonNodeFunc: () =>
            {
                return ("", (JsonObject)[]);
            }
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
            jsonNodeFunc: () =>
            {
                return (jsonObjectName, list.SerializeAll());
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
            jsonNodeFunc: () =>
            {
                return (jsonObjectName, value.SerializeAll());
            }
        );
    }
}
