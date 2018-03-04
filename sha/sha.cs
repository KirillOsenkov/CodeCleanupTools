using System;
using System.IO;

internal class Sha
{
    private static void Main(string[] args)
    {
        string filePath = null;
        if (args.Length == 1)
        {
            filePath = args[0];
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine("File doesn't exist:" + filePath);
        }

        var hash = Utilities.SHA1Hash(filePath);
        Console.WriteLine(hash);
    }
}