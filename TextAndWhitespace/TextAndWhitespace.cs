using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class TextAndWhitespace
{
    private static readonly HashSet<string> removeConsecutiveEmptyLinesFromExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ps1",
        "psm1",
    };

    private static readonly HashSet<string> trimTrailingWhitespaceFromExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cs",
        "ps1",
        "psm1",
        "csproj",
        "xaml",
    };

    private static readonly Dictionary<string, int> replaceLeadingTabsWithSpaces = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "cs", 4 },
        { "csproj", 2 },
        { "ps1", 4 },
        { "psm1", 4 },
        { "xaml", 2 },
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
        var fileInfos = GetNonHiddenFiles(new DirectoryInfo(folder), pattern);
        foreach (var file in fileInfos)
        {
            ProcessFile(file.FullName);
        }
    }

    private static IList<FileInfo> GetNonHiddenFiles(DirectoryInfo baseDirectory, string pattern)
    {
        var fileInfos = new List<System.IO.FileInfo>();
        fileInfos.AddRange(baseDirectory.GetFiles(pattern, SearchOption.TopDirectoryOnly).Where(f => (f.Attributes & FileAttributes.Hidden) == 0));

        // skip hidden directories (like .git) and directories that start with '.'
        // that are not hidden (like .nuget).
        foreach (var directory in baseDirectory.GetDirectories("*.*", SearchOption.TopDirectoryOnly).Where(w => (w.Attributes & FileAttributes.Hidden) == 0 && !w.Name.StartsWith(".")))
        {
            fileInfos.AddRange(GetNonHiddenFiles(directory, pattern));
        }

        return fileInfos;
    }

    public static void ProcessFile(string file)
    {
        if (IsBinary(file))
        {
            return;
        }

        var text = File.ReadAllText(file);
        if (text.Contains('\uFFFD'))
        {
            // it's not Unicode, let's try Default
            text = File.ReadAllText(file, Encoding.Default);
        }

        var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

        var newText = ProcessText(text, extension);
        if (newText != text)
        {
            File.WriteAllText(file, newText);
        }
    }

    public static string ProcessText(string text, string extension)
    {
        if (IsGeneratedCode(text) || text.IndexOf('\0') > -1)
        {
            return text;
        }

        var newText = text;
        newText = EnsureCrLf(newText);

        int spaces = 4;
        if (replaceLeadingTabsWithSpaces.TryGetValue(extension, out spaces))
        {
            newText = ReplaceLeadingTabsWithSpaces(newText, spaces);
        }

        if (trimTrailingWhitespaceFromExtensions.Contains(extension))
        {
            newText = TrimTrailingWhitespaceFromEveryLine(newText);
        }

        if (removeConsecutiveEmptyLinesFromExtensions.Contains(extension))
        {
            newText = RemoveConsecutiveEmptyLines(newText);
        }

        return newText;
    }

    public static string ReplaceLeadingTabsWithSpaces(string text, int spacesPerTab)
    {
        var sb = new StringBuilder(text.Length);
        var spaces = new string(' ', spacesPerTab);

        bool atBeginningOfLine = true;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' || c == '\n')
            {
                atBeginningOfLine = true;
                sb.Append(c);
            }
            else if (c == '\t')
            {
                if (atBeginningOfLine)
                {
                    sb.Append(spaces);
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == ' ')
            {
                sb.Append(c);
            }
            else
            {
                atBeginningOfLine = false;
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static bool IsBinary(string file)
    {
        var extension = Path.GetExtension(file).TrimStart('.');
        return binaryExtensions.Contains(extension);
    }

    public static string TrimTrailingWhitespaceFromEveryLine(string text)
    {
        IEnumerable<string> lines = GetLines(text);
        lines = lines.Select(l => l.TrimEnd());
        text = string.Join(Environment.NewLine, lines);
        return text;
    }

    private static string RemoveConsecutiveEmptyLines(string text) => text.Replace("\n\r\n\r", "\n\r");

    private static void PrintHelp()
    {
        Console.WriteLine(@"Usage: TextAndWhitespace.exe [pattern]

    * Converts all line endings to CRLF for every file in the current
      directory and all subdirectories
    * Sets the encoding to UTF8 without signature (no-BOM)
    * Replaces leading tabs with spaces
    * Removes trailing whitespace from lines

");
    }

    public static bool IsGeneratedCode(string text) => text.Contains("This code was generated");

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