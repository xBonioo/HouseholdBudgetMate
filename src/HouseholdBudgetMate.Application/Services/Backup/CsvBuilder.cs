using System.Globalization;
using System.Text;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal sealed class CsvBuilder
{
    private readonly StringBuilder _builder = new();

    public void AddRow(params object?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                _builder.Append(',');
            }

            _builder.Append(Escape(Format(values[i])));
        }

        _builder.AppendLine();
    }

    public byte[] ToUtf8BytesWithBom()
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(_builder.ToString());
        var result = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);
        return result;
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => string.Empty,
            decimal amount => amount.ToString("0.00", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Escape(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
