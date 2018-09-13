using System;
using System.IO;

class dos2unix
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        string input = args[0];
        string output = null;
        if (args.Length >= 2)
        {
            output = args[1];
        }

        if (!File.Exists(input))
        {
            Console.WriteLine($"Input file {input} doesn't exist");
            return;
        }

        if (File.Exists(output))
        {
            Console.WriteLine("WARNING: overwriting file " + output);
        }

        Convert(input, output);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("A tool to convert a file from CRLF to LF");
        Console.WriteLine("  Usage: dos2unix <input> <output>");
    }

    private static void Convert(string input, string output, int[] columns = null)
    {
        var lines = File.ReadAllLines(input);
        var text = string.Join("\n", lines);
        File.WriteAllText(output, text);
    }
}