using System.Security.Cryptography;
using Xunit;

public sealed class ManualEvidenceCatalogTests
{
    [Fact]
    public void CatalogHashesSupportedLocalImagesWithoutReturningPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ddcore-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
            File.WriteAllBytes(Path.Combine(directory, "screen.png"), png);
            File.WriteAllText(Path.Combine(directory, "notes.txt"), "not an image");

            var hashes = ManualEvidenceCatalog.ReadSha256(directory);

            Assert.Equal([Convert.ToHexString(SHA256.HashData(png))], hashes);
            Assert.DoesNotContain(directory, string.Join(',', hashes), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CatalogRejectsAnImageExtensionWithInvalidSignature()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ddcore-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "not-an-image.png"), "not an image");

            Assert.Throws<InvalidDataException>(() => ManualEvidenceCatalog.ReadSha256(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
