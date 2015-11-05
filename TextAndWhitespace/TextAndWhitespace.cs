using System;
using System.Collections.Generic;
using static System.Console;
using System.IO;
using System.Linq;
using System.Text;

class TextAndWhitespace
{
    private static readonly HashSet<string> removeConsecutiveEmptyLinesFromExtensions = new HashSet<string>
    {
        "cs",
        "ps1",
        "psm1",
    };

    private static readonly HashSet<string> trimLeadingTabsFromExtensions = new HashSet<string>
    {
        "cs",
        "ps1",
        "psm1",
        "xaml",
    };

    private static readonly HashSet<string> trimTrailingWhitespaceFromExtensions = new HashSet<string>
    {
        "cs",
        "ps1",
        "psm1",
        "csproj",
        "xaml",
    };

    private static readonly HashSet<string> binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "exe",
        "dll",
        "pdb",
        "zip",
        "png",
        "jpg",
        "snk",
        "ico",
        "ani",
        "gif",
        "ttf",
        "pfx",
        "lex",
    };

    static void Main(string[] args)
    {
        if (args.Length > 1)
        {
            PrintHelp();
            return;
        }

        var pattern = "*.*";
        if (args.Length == 1)
        {
            if (args[0] == "/?" || args[0] == "-h" || args[0] == "-help" || args[0] == "/help")
            {
                PrintHelp();
                return;
            }

            pattern = args[0];
        }

        var folder = Environment.CurrentDirectory;
        var files = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (IsBinary(file))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            if (GetFileEncoding(file) == Encoding.Default)
            {
                if (ContainsExtendedAscii(text, file))
                {
                    WriteLine($"Skipped: Extended ASCII characters: {file}");
                    continue;
                }
            }

            if (IsGeneratedCode(text) || text.IndexOf('\0') > -1)
            {
                continue;
            }

            var newText = text;
            newText = EnsureCrLf(newText);

            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

            if (removeConsecutiveEmptyLinesFromExtensions.Contains(extension))
            {
                newText = RemoveConsecutiveEmptyLines(newText);
            }

            if (trimLeadingTabsFromExtensions.Contains(extension))
            {
                newText = TrimLeadingTabsFromEveryLine(newText);
            }

            if (trimTrailingWhitespaceFromExtensions.Contains(extension))
            {
                newText = TrimTrailingWhitespaceFromEveryLine(newText);
            }

            if (newText != text)
            {
                File.WriteAllText(file, newText, Encoding.UTF8);
            }
        }
    }

    private static bool ContainsExtendedAscii(string text, string file)
    {
        foreach (var ch in text)
        {
            if (ch >= 0x80 || ch <= 0xFF)
            {
                return true;
            }
        }

        return false;
    }

    private static Encoding GetFileEncoding(string file)
    {
        using (var sr = new StreamReader(file, Encoding.Default))
        {
            sr.Read();
            return sr.CurrentEncoding;
        }
    }

    private static bool IsBinary(string file)
    {
        var extension = Path.GetExtension(file).TrimStart('.');
        return binaryExtensions.Contains(extension);
    }

    public static string TrimLeadingTabsFromEveryLine(string text)
    {
        IEnumerable<string> lines = GetLines(text, true);
        string newText = string.Empty;

        foreach (var line in lines)
        {
            var firstNonSpace = line.IndexOfFirstNonSpaceCharacter();
            if (firstNonSpace == -1)
            {
                newText += line;
            }
            else
            {
                var s1 = line.Substring(0, firstNonSpace);
                var s2 = line.Substring(firstNonSpace);
                var s3 = s1.Replace("\t", "    ");
                newText += s3 + s2;
            }
        }

        return newText;
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
        Console.WriteLine(@"Usage: TextAndWhitespace.exe [pattern]

    * Converts all line endings to CRLF for every file in the current
      directory and all subdirectories
    * Sets the encoding to UTF8 with BOM
    * Ensures the file ends with a line break

Pattern is optional and defaults to *.cs.

For .cs, .csproj and .xaml files: 

    * Removes trailing spaces from every line

For .cs files the tool additionally:

    * replaces two consecutive empty lines with just one 
      (WARNING: the tool is currently blind and will mutate contents 
      of string literals as well)
");
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