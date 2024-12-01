namespace Aimmy2.Extensions;

public static class StringExtensions
{
    public static string NewLineAfter(this string s, string c = ".") => s.Replace(c, $"{c}{Environment.NewLine}");
}