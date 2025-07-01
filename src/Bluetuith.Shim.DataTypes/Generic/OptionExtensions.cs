using System.Text;

namespace Bluetuith.Shim.DataTypes;

public static class OptionExtensions
{
    public static void AppendString<T>(
        this Nullable<T> option,
        string name,
        ref StringBuilder stringBuilder
    )
        where T : struct
    {
        if (option.HasValue && option.Value is var value)
        {
            string print;
            if (value is bool v)
                print = v ? "yes" : "no";
            else
                print = $"{value}";

            stringBuilder.AppendLine($"{name}: {print}");
        }
    }
}
