using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class TextAndWhitespace
{
    static void Main(string[] args)
    {
        if (args.Length > 1)
        {
            PrintHelp();
            return;
        }
        
        var pattern = "*.cs";
        if (args.Length == 1)
        {
            pattern = args[0];
        }

        var folder = Environment.CurrentDirectory;
        var files = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var newText = text;

            if (IsGeneratedCode(text))
            {
                continue;
            }

            newText = EnsureCrLf(newText);

            if (".cs".Equals(Path.GetExtension(file), StringComparison.OrdinalIgnoreCase))
            {
                newText = RemoveConsecutiveEmptyLines(newText);
                newText = TrimTrailingWhitespaceFromEveryLine(newText);
            }

            if (newText != text)
            {
                File.WriteAllText(file, newText, Encoding.UTF8);
            }
        }
    }

    public static string TrimTrailingWhitespaceFromEveryLine(string text)
    {
        IEnumerable<string> lines = GetLines(text);
        lines = lines.Select(l => l.TrimEnd());
        text = string.Join(Environment.NewLine, lines);
        return text;
    }

    private static string RemoveConsecutiveEmptyLines(string text)
    {
        return text.Replace("\n\r\n\r", "\n\r");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"Usage: TextAndWhitespace.exe
       For every .cs file in the current directory and all subdirectories:
        * Converts all line endings to CRLF
        * Removes two consecutive empty lines leaving just one
        * Removes trailing spaces from every line");
    }

    public static bool IsGeneratedCode(string text)
    {
        return text.Contains("This code was generated");
    }

    public static string[] GetLines(string text, bool includeLineBreaksInLines = false)
    {
        var lineLengths = GetLineLengths(text);
        int position = 0;
        var lines = new string[lineLengths.Length];
        for (int i = 0; i < lineLengths.Length; i++)
        {
            var lineLength = lineLengths[i];
            if (!includeLineBreaksInLines)
            {
                if (lineLength >= 2 &&
                    text[position + lineLength - 2] == '\r' && 
                    text[position + lineLength - 1] == '\n')
                {
                    lineLength -= 2;
                }
                else if (lineLength >= 1 &&
                    (text[position + lineLength - 1] == '\r' || 
                    text[position + lineLength - 1] == '\n'))
                {
                    lineLength--;
                }
            }

            lines[i] = text.Substring(position, lineLength);
            position += lineLengths[i];
        }

        return lines;
    }

    public static int[] GetLineLengths(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException();
        }

        if (text.Length == 0)
        {
            return new int[0];
        }

        var result = new List<int>();
        int currentLineLength = 0;
        bool previousWasCarriageReturn = false;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (previousWasCarriageReturn)
                {
                    currentLineLength++;
                    result.Add(currentLineLength);
                    currentLineLength = 0;
                    previousWasCarriageReturn = false;
                }
                else
                {
                    currentLineLength++;
                    previousWasCarriageReturn = true;
                }
            }
            else if (text[i] == '\n')
            {
                previousWasCarriageReturn = false;
                currentLineLength++;
                result.Add(currentLineLength);
                currentLineLength = 0;
            }
            else
            {
                currentLineLength++;
                previousWasCarriageReturn = false;
            }
        }

        result.Add(currentLineLength);

        if (previousWasCarriageReturn)
        {
            result.Add(0);
        }

        return result.ToArray();
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