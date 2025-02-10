using System.Text;
using DotNext;

namespace Bluetuith.Shim.Extensions;

public static class OptionExtensions
{
    public static void AppendString<T>(
        this Optional<T> option,
        string name,
        ref StringBuilder stringBuilder
    )
    {
        if (option.TryGet(out var value))
        {
            var print = "";

            if (value is bool v)
                print = v ? "yes" : "no";
            else
                print = $"{value}";

            stringBuilder.AppendLine($"{name}: {print}");
        }
    }
}
