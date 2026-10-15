using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class TextAndWhitespace
{
    public static Encoding ForceEncoding = null;
    public static bool ForceCRLF = true;
    public static bool ForceLF = false;
    public static bool ReplaceTabsWithSpaces = false;
    public static bool TrimTrailingWhitespace = false;
    public static bool ShouldRemoveConsecutiveEmptyLines = false;

    private static readonly HashSet<string> removeConsecutiveEmptyLinesFromExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cs",
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

    private static readonly HashSet<string> skipExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
        "js",
        "xsd",
        "br",
        "gz",
        "tgz",
        "7z",
        "tar",
        "rar",
        "nupkg",
        "snupkg",
        "jar",
        "wasm",
        "dat",
        "bin",
        "binlog",
        "so",
        "dylib",
        "lib",
        "obj",
        "woff",
        "woff2",
        "otf",
        "eot",
        "bmp",
        "tif",
        "tiff",
        "webp",
        "mp3",
        "mp4",
        "wav",
    };

    private static readonly HashSet<string> skipDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
    };

    static void Main(string[] args)
    {
        var pattern = "*.*";
        var paths = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var curArg = args[i];
            switch (curArg.ToLowerInvariant())
            {
                case "/?":
                case "-h":
                case "-help":
                case "/help":
                    PrintHelp();
                    return;
                default:
                    if (Directory.Exists(curArg) || File.Exists(curArg))
                    {
                        paths.Add(Path.GetFullPath(curArg));
                    }
                    else if (curArg.IndexOfAny(new[] { '\\', '/' }) >= 0)
                    {
                        Console.Error.WriteLine($"No such file or folder: {curArg}");
                        return;
                    }
                    else
                    {
                        pattern = curArg;
                    }

                    break;
            }
        }

        if (paths.Count == 0)
        {
            paths.Add(Environment.CurrentDirectory);
        }

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                // a file named on the command line is processed whatever its extension
                TryProcessFile(path, explicitlyNamed: true);
                continue;
            }

            var fileInfos = GetNonHiddenFiles(new DirectoryInfo(path), pattern);
            foreach (var file in fileInfos)
            {
                TryProcessFile(file.FullName, explicitlyNamed: false);
            }
        }
    }

    private static void TryProcessFile(string file, bool explicitlyNamed)
    {
        try
        {
            ProcessFile(file, explicitlyNamed);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed: {file}: {ex.Message}");
        }
    }

    public static void ProcessFile(string file, bool explicitlyNamed = false)
    {
        if (!explicitlyNamed && ShouldSkipFile(file))
        {
            return;
        }

        var bytes = File.ReadAllBytes(file);
        if (!TryDecode(bytes, out string text, out Encoding currentEncoding))
        {
            Console.WriteLine($"Skipped, binary or not in a known encoding: {file}");
            return;
        }

        if (IsGeneratedCode(text) || text.IndexOf('\0') > -1)
        {
            return;
        }

        var encoding = ForceEncoding ?? currentEncoding;

        var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

        var newText = ProcessText(text, extension);
        if (encoding != currentEncoding || newText != text)
        {
            File.WriteAllText(file, newText, encoding);
            Console.WriteLine(file);
        }
    }

    private static IList<FileInfo> GetNonHiddenFiles(DirectoryInfo baseDirectory, string pattern)
    {
        var fileInfos = new List<FileInfo>();
        fileInfos.AddRange(baseDirectory.GetFiles(pattern, SearchOption.TopDirectoryOnly).Where(f => (f.Attributes & FileAttributes.Hidden) == 0));

        // skip hidden directories (like .git), directories that start with '.'
        // that are not hidden (like .nuget), and build output (bin, obj).
        foreach (var directory in baseDirectory.GetDirectories("*.*", SearchOption.TopDirectoryOnly).Where(w =>
            (w.Attributes & FileAttributes.Hidden) == 0 &&
            !w.Name.StartsWith(".") &&
            !skipDirectories.Contains(w.Name)))
        {
            fileInfos.AddRange(GetNonHiddenFiles(directory, pattern));
        }

        return fileInfos;
    }

    /// <summary>
    /// Decodes the file's bytes by its byte order mark, or without one as UTF-8, else in the
    /// system's ANSI code page. Fails for a file without a UTF-16/32 byte order mark that has
    /// a zero byte (binary), and for one that doesn't decode and encode back to the same bytes,
    /// so that writing the text back can never change anything but what was processed.
    /// </summary>
    public static bool TryDecode(byte[] bytes, out string text, out Encoding encoding)
    {
        encoding = GetEncodingFromByteOrderMark(bytes);
        if (encoding != null)
        {
            return TryRoundTrip(bytes, encoding, out text);
        }

        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            text = null;
            return false;
        }

        encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (TryRoundTrip(bytes, encoding, out text))
        {
            return true;
        }

        encoding = AnsiEncoding;
        return TryRoundTrip(bytes, encoding, out text);
    }

    private static bool TryRoundTrip(byte[] bytes, Encoding encoding, out string text)
    {
        var preambleLength = encoding.GetPreamble().Length;
        var body = new ReadOnlySpan<byte>(bytes, preambleLength, bytes.Length - preambleLength);
        text = encoding.GetString(body);
        if (encoding.GetBytes(text).AsSpan().SequenceEqual(body))
        {
            return true;
        }

        text = null;
        return false;
    }

    public static Encoding GetEncodingFromByteOrderMark(byte[] bytes)
    {
        bool StartsWith(params byte[] bom) => bytes.Length >= bom.Length && bytes.AsSpan(0, bom.Length).SequenceEqual(bom);

        // UTF-32 LE before UTF-16 LE: its mark starts with UTF-16 LE's
        if (StartsWith(0xff, 0xfe, 0, 0)) return new UTF32Encoding(bigEndian: false, byteOrderMark: true);
        if (StartsWith(0, 0, 0xfe, 0xff)) return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        if (StartsWith(0xef, 0xbb, 0xbf)) return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        if (StartsWith(0xff, 0xfe)) return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        if (StartsWith(0xfe, 0xff)) return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        return null;
    }

    private static Encoding ansiEncoding;

    /// <summary>
    /// The system's ANSI code page (Encoding.Default is UTF-8 on .NET Core), Windows-1252
    /// where that is UTF-8 too.
    /// </summary>
    private static Encoding AnsiEncoding
    {
        get
        {
            if (ansiEncoding == null)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var encoding = Encoding.GetEncoding(0);
                if (encoding.CodePage == Encoding.UTF8.CodePage)
                {
                    encoding = Encoding.GetEncoding(1252);
                }

                ansiEncoding = encoding;
            }

            return ansiEncoding;
        }
    }

    public static string ProcessText(string text, string extension)
    {
        var newText = text;

        if (ForceCRLF)
        {
            newText = EnsureCrLf(newText);
        }

        if (ForceLF)
        {
            newText = EnsureLf(newText);
        }

        if (ReplaceTabsWithSpaces && replaceLeadingTabsWithSpaces.TryGetValue(extension, out int spaces))
        {
            newText = ReplaceLeadingTabsWithSpaces(newText, spaces);
        }

        if (TrimTrailingWhitespace && trimTrailingWhitespaceFromExtensions.Contains(extension))
        {
            newText = TrimTrailingWhitespaceFromEveryLine(newText);
        }

        if (ShouldRemoveConsecutiveEmptyLines && removeConsecutiveEmptyLinesFromExtensions.Contains(extension))
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

    private static bool ShouldSkipFile(string file)
    {
        var extension = Path.GetExtension(file).TrimStart('.');
        return skipExtensions.Contains(extension);
    }

    public static string TrimTrailingWhitespaceFromEveryLine(string text)
    {
        IEnumerable<string> lines = GetLines(text, includeLineBreaksInLines: true);
        lines = lines.Select(l => TrimEnd(l));
        text = string.Concat(lines);
        return text;
    }

    private static string TrimEnd(string l)
    {
        if (l.EndsWith("\r\n"))
        {
            return l.Substring(0, l.Length - 2).TrimEnd() + "\r\n";
        }
        else if (l.EndsWith("\n"))
        {
            return l.Substring(0, l.Length - 1).TrimEnd() + "\n";
        }

        return l;
    }

    private static string RemoveConsecutiveEmptyLines(string text) => text.Replace("\n\r\n\r", "\n\r").Replace("\n\n\n", "\n\n");

    private static void PrintHelp()
    {
        Console.WriteLine(@"Usage: TextAndWhitespace.exe [path ...] [pattern]

    * Converts all line endings to CRLF for every file matching the pattern
      (*.* by default) in each folder given (the current directory by default)
      and all its subdirectories, but hidden ones, ones starting with '.',
      bin and obj
    * A file given as a path is processed whatever its extension
    * Skips binary files (a zero byte without a UTF-16/32 byte order mark) and
      files that don't round-trip through their encoding (BOM, else UTF-8,
      else the ANSI code page); keeps each file's encoding
    * Replaces leading tabs with spaces (off by default)
    * Removes trailing whitespace from lines (off by default)

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

    private static string EnsureLf(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r' && i < text.Length - 1 && text[i + 1] == '\n')
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
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