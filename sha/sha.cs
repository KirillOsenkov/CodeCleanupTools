using System;
using System.IO;
using System.Security.Cryptography;

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
            return;
        }

        var sha1 = Utilities.SHA1Hash(filePath);
        var sha256 = Utilities.SHA256Hash(filePath);
        var md5 = Utilities.Hash(filePath, MD5Cng.Create());
        Console.WriteLine($"SHA1:   {sha1}");
        Console.WriteLine($"SHA256: {sha256}");
        Console.WriteLine($"MD5:    {md5}");
    }
}