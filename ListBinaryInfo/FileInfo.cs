using System;

public class FileInfo
{
    public string FilePath { get; set; }

    // read from Cecil
    public string TargetFramework { get; set; }
    public string FileVersion { get; set; }
    public string InformationalVersion { get; set; }

    // set by sn
    public string Signed { get; set; }
    public string FullSigned { get; set; }

    // set by corflags
    public string Architecture { get; set; }
    public string Platform { get; set; }

    private bool? isManagedAssembly;
    public bool IsManagedAssembly
    {
        get
        {
            if (isManagedAssembly == null)
            {
                isManagedAssembly = GetIsManagedAssembly(FilePath);
            }

            return isManagedAssembly.Value;
        }
    }

    private static bool GetIsManagedAssembly(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (!filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            !filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            !filePath.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!PEFile.PEFileReader.IsManagedAssembly(filePath))
        {
            return false;
        }

        return true;
    }

    private string assemblyName;
    public string AssemblyName
    {
        get
        {
            if (assemblyName == null)
            {
                if (IsManagedAssembly)
                {
                    assemblyName = ListBinaryInfo.GetAssemblyNameText(FilePath);
                }
                else
                {
                    assemblyName = ListBinaryInfo.NotAManagedAssembly;
                }
            }

            return assemblyName;
        }
    }

    private string signedText = null;
    private bool readSignedText;
    public string SignedText
    {
        get
        {
            if (!readSignedText)
            {
                readSignedText = true;

                if (IsManagedAssembly)
                {
                    ListBinaryInfo.CheckSigned(this);

                    signedText = FullSigned ?? "";
                    if (Signed != "Signed" && Signed != null)
                    {
                        signedText += "(" + Signed + ")";
                    }
                }
            }

            return signedText;
        }
    }

    private string platformText = null;
    private bool readPlatformText;
    public string PlatformText
    {
        get
        {
            if (!readPlatformText)
            {
                readPlatformText = true;

                if (IsManagedAssembly)
                {
                    ListBinaryInfo.CheckPlatform(this);

                    platformText = Architecture;
                    if (Platform != "32BITPREF : 0" && Platform != null)
                    {
                        platformText += "(" + Platform + ")";
                    }
                }
            }

            return platformText;
        }
    }

    public static FileInfo Get(string filePath, bool readModule = false)
    {
        var fileInfo = new FileInfo
        {
            FilePath = filePath
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

    private long fileSize = -1;
    public long FileSize 
    {
        get
        {
            if (fileSize == -1)
            {
                fileSize = new System.IO.FileInfo(FilePath).Length;
            }

            return fileSize;
        }
    }
}
