using System;
using System.IO;
using System.Text;

class bin2hex
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: bin2hex <inputbinary> <outputtext>");
            return;
        }

        var input = args[0];
        var output = args[1];

        if (!File.Exists(input))
        {
            Console.WriteLine($"File {input} doesn't exist");
            return;
        }

        if (File.Exists(output))
        {
            Console.WriteLine("WARNING: overwriting file " + output);
        }

        ConvertToHex(input, output);
    }

    private static void ConvertToHex(string input, string output)
    {
        var bytes = File.ReadAllBytes(input);
        var text = ConvertToHex(bytes);
        File.WriteAllText(output, text);
    }

    private static string ConvertToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 3 + bytes.Length * 2 / 8);

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
            if (column < 7)
            {
                sb.Append(' ');
            }

            column++;
            if (column == 8)
            {
                column = 0;
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}