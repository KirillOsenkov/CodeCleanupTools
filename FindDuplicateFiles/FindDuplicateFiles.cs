using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal class FindDuplicateFiles
{
    private static readonly Dictionary<string, HashSet<string>> filesByHash = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    private static void Main(string[] args)
    {
        var pattern = "*.*";
        if (args.Length == 1)
        {
            pattern = args[0];
        }

        var allFiles = Directory.GetFiles(Environment.CurrentDirectory, pattern, SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var hash = SHA1Hash(file);
            HashSet<string> bucket;
            if (!filesByHash.TryGetValue(hash, out bucket))
            {
                bucket = new HashSet<string>();
                filesByHash[hash] = bucket;
            }

            bucket.Add(file);
        }

        var sb = new StringBuilder();
        foreach (var currentBucket in filesByHash)
        {
            if (currentBucket.Value.Count > 1)
            {
                foreach (var dupe in currentBucket.Value)
                {
                    sb.AppendLine(dupe);
                }

                sb.AppendLine();
            }
        }

        Console.WriteLine(sb.ToString());
    }

    public static string ByteArrayToHexString(byte[] bytes, int digits = 0)
    {
        if (digits == 0)
        {
            digits = bytes.Length * 2;
        }

        char[] c = new char[digits];
        byte b;
        for (int i = 0; i < digits / 2; i++)
        {
            b = ((byte)(bytes[i] >> 4));
            c[i * 2] = (char)(b > 9 ? b + 87 : b + 0x30);
            b = ((byte)(bytes[i] & 0xF));
            c[i * 2 + 1] = (char)(b > 9 ? b + 87 : b + 0x30);
        }

        return new string(c);
    }

    private static string SHA1Hash(string filePath)
    {
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var hash = new SHA1Managed())
        {
            var result = hash.ComputeHash(stream);
            return ByteArrayToHexString(result);
        }
    }
}