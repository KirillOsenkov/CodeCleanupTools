using System;
using System.IO;

class dos2unix
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            PrintHelp();
            return;
        }

        string input = args[0];

        if (!File.Exists(input))
        {
            Console.WriteLine($"Input file {input} doesn't exist");
            return;
        }

        Convert(input);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("A tool to convert a file from CRLF to LF. Rewrites the file in-place.");
        Console.WriteLine("    Usage: dos2unix <filepath>");
    }

    private static void Convert(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var text = string.Join("\n", lines);
        File.WriteAllText(filePath, text);
    }
}