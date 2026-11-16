using System.Security.Cryptography;

public static class ManualEvidenceCatalog
{
    private const int MaximumFileCount = 256;
    private const long MaximumFileSizeBytes = 50 * 1024 * 1024;
    private const long MaximumTotalSizeBytes = 250 * 1024 * 1024;

    public static IReadOnlySet<string> ReadSha256(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullPath = Path.GetFullPath(directory);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException("The local evidence directory was not found.");
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The evidence directory cannot be a reparse point.");

        var entries = Directory.EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
            .Take(MaximumFileCount + 1)
            .ToArray();
        if (entries.Length > MaximumFileCount)
            throw new InvalidDataException($"Evidence directory exceeds the {MaximumFileCount}-file limit.");

        var paths = entries
            .Where(static path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp")
            .Order(StringComparer.Ordinal)
            .ToArray();

        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var path in paths)
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Evidence images cannot be reparse points.");
            if (info.Length is <= 0 or > MaximumFileSizeBytes)
                throw new InvalidDataException("An evidence image is empty or exceeds the 50 MiB limit.");
            totalBytes = checked(totalBytes + info.Length);
            if (totalBytes > MaximumTotalSizeBytes)
                throw new InvalidDataException("Evidence directory exceeds the 250 MiB total limit.");

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = new byte[12];
            stream.ReadExactly(header);
            if (!MatchesExtension(Path.GetExtension(path), header))
                throw new InvalidDataException("An evidence file extension does not match a supported image signature.");

            stream.Position = 0;
            hashes.Add(Convert.ToHexString(SHA256.HashData(stream)));
        }

        return hashes;
    }

    private static bool MatchesExtension(string extension, ReadOnlySpan<byte> bytes) => extension.ToLowerInvariant() switch
    {
        ".png" => bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".jpg" or ".jpeg" => bytes[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
        ".webp" => bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        ".bmp" => bytes[..2].SequenceEqual("BM"u8),
        _ => false,
    };
}
