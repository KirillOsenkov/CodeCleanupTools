using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class bin2hex
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("A tool to convert binary files to text (hex) and back to binary.");
            Console.WriteLine("  Usage: bin2hex <input> <output> [<column-width>]*");
            Console.WriteLine("  If the input is a binary file, writes a human-readable hex contents to the output file (will overwrite)");
            Console.WriteLine("  If the input is a text hex file produced by this tool, writes the bytes to the output as binary. In a hex file, whitespace is ignored.");
            Console.WriteLine("  By default the tool will output bytes on a single line. Specify one or more space-separated numbers after input and output to create columns.");
            Console.WriteLine("  Example: bin2hex foo.dll foo.txt 8         - uses one 8-byte column");
            Console.WriteLine("  Example: bin2hex foo.dll foo.txt 4 4       - uses two 4-byte columns");
            Console.WriteLine("  Example: bin2hex foo.dll foo.txt 8 8 8     - uses three 8-byte columns");
            return;
        }

        var input = args[0];
        var output = args[1];

        if (!File.Exists(input))
        {
            Console.WriteLine($"Input file {input} doesn't exist");
            return;
        }

        if (File.Exists(output))
        {
            Console.WriteLine("WARNING: overwriting file " + output);
        }

        var columns = new List<int>();
        if (args.Length > 2)
        {
            for (int i = 2; i < args.Length; i++)
            {
                int column;
                if (!int.TryParse(args[i], out column))
                {
                    Console.WriteLine($"Argument specified after input and output must be an integer for column width (you specified '{args[i]}')");
                    return;
                }

                if (column <= 0 || column > 10000)
                {
                    Console.WriteLine("A column must be between 1 and 10000 bytes wide, you specified " + column.ToString());
                    return;
                }

                columns.Add(column);
            }
        }

        Convert(input, output, columns.ToArray());
    }

    private static void Convert(string input, string output, int[] columns = null)
    {
        var bytes = File.ReadAllBytes(input);
        bool isValidHexFile = IsValidHexFile(bytes);
        if (isValidHexFile)
        {
            bytes = ConvertToBytes(bytes);
            File.WriteAllBytes(output, bytes);
        }
        else
        {
            var text = ConvertToHex(bytes, columns);
            File.WriteAllText(output, text);
        }
    }

    private static byte[] ConvertToBytes(byte[] bytes)
    {
        var result = new List<byte>();
        char firstHalf = '\0';

        for (int i = 0; i < bytes.Length; i++)
        {
            char c = (char)bytes[i];

            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                // just ignore the whitespace
                continue;
            }

            if (firstHalf == '\0')
            {
                firstHalf = c;
            }
            else
            {
                byte b = (byte)((GetHexVal(firstHalf) << 4) + (GetHexVal(c)));
                result.Add(b);
                firstHalf = '\0';
            }
        }

        return result.ToArray();
    }

    public static byte GetHexVal(char hex)
    {
        int val = (int)hex;
        //For uppercase A-F letters:
        //return val - (val < 58 ? 48 : 55);
        //For lowercase a-f letters:
        //return (byte)(val - (val < 58 ? 48 : 87));
        //Or the two combined, but a bit slower:
        return (byte)(val - (val < 58 ? 48 : (val < 97 ? 55 : 87)));
    }

    private static bool IsValidHexFile(byte[] bytes)
    {
        int digitCount = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            char c = (char)bytes[i];

            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                // just ignore the whitespace
                continue;
            }

            if ((c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'F') ||
                (c >= 'a' && c <= 'f'))
            {
                digitCount++;
            }
            else
            {
                // neither a whitespace nor a digit - invalid file
                return false;
            }
        }

        return (digitCount % 2) == 0;
    }

    private static string ConvertToHex(byte[] bytes, int[] columns = null)
    {
        var sb = new StringBuilder(bytes.Length * 3 + bytes.Length * 2 / 8);

        if (columns == null || columns.Length == 0)
        {
            columns = new[] { bytes.Length };
        }

        int sum = 0;
        for (int i = 0; i < columns.Length; i++)
        {
            var current = columns[i];
            columns[i] += sum;
            sum += current;
        }

        int column = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            var b1 = ((byte)(b >> 4));
            var c1 = (char)(b1 > 9 ? b1 + 55 : b1 + 0x30);
            var b2 = ((byte)(b & 0xF));
            var c2 = (char)(b2 > 9 ? b2 + 55 : b2 + 0x30);

            sb.Append(c1);
            sb.Append(c2);

            for (int j = 0; j < columns.Length; j++)
            {
                if (column == (columns[j] - 1) && column < sum - 1)
                {
                    sb.Append(' ');
                }
            }

            if (column < sum - 1)
            {
                sb.Append(' ');
            }

            column++;

            if (column >= sum)
            {
                column = 0;
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}