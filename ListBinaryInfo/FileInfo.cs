public class FileInfo
{
    public string FilePath { get; set; }
    public string AssemblyName { get; set; }
    public string FullSigned { get; set; }
    public string Platform { get; set; }
    public string TargetFramework { get; set; }
    public string Architecture { get; set; }
    public string Signed { get; set; }
    public string FileVersion { get; set; }
    public string InformationalVersion { get; set; }
    public long FileSize { get; set; }

    public string SignedText
    {
        get
        {
            var signedText = FullSigned;
            if (Signed != "Signed" && Signed != null)
            {
                signedText += "(" + Signed + ")";
            }

            return signedText;
        }
    }

    public string PlatformText
    {
        get
        {
            var platformText = Architecture;
            if (Platform != "32BITPREF : 0" && Platform != null)
            {
                platformText += "(" + Platform + ")";
            }

            return platformText;
        }
    }

    public static FileInfo Get(string filePath, bool readModule = false)
    {
        var fileInfo = new FileInfo
        {
            FilePath = filePath,
            AssemblyName = ListBinaryInfo.GetAssemblyNameText(filePath),
            FileSize = new System.IO.FileInfo(filePath).Length
        };

        if (readModule && fileInfo.AssemblyName != null)
        {
            ListBinaryInfo.ReadModuleInfo(fileInfo);
        }

        return fileInfo;
    }

    private string sha;
    public string Sha
    {
        get
        {
            if (sha == null)
            {
                sha = Utilities.SHA1Hash(FilePath);
            }

            return sha;
        }
    }
}
