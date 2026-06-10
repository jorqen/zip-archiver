namespace ZipArchiver.Core;

/// <summary>Форматирование размера в байтах в человекочитаемый вид.</summary>
public static class ByteSize
{
    private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];

    public static string Format(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {Units[0]}" : $"{value:0.#} {Units[unit]}";
    }
}
