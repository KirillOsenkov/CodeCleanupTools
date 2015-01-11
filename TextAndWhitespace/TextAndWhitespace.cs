using System;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        var folder = args[0];
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            if (IsGeneratedCode(text))
            {
                continue;
            }

            text = text.Replace("\n\r\n\r", "\n\r");

            text = EnsureCrLf(text);
            File.WriteAllText(file, text, Encoding.UTF8);

            var lines = File.ReadLines(file);
            lines = lines.Select(l => l.TrimEnd()).ToArray();
            File.WriteAllLines(file, lines, Encoding.UTF8);
        }
    }

    public static bool IsGeneratedCode(string text)
    {
        return text.Contains("This code was generated");
    }

    private static string EnsureCrLf(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder();

        char previous = '\0';

        if (text[0] == '\n')
        {
            sb.Append('\r');
        }

        foreach (var ch in text)
        {
            if (previous != '\r' && previous != '\0' && ch == '\n')
            {
                sb.Append('\r');
            }

            if (previous == '\r' && ch != '\n')
            {
                sb.Append('\n');
            }

            sb.Append(ch);
            previous = ch;
        }

        return sb.ToString();
    }
}