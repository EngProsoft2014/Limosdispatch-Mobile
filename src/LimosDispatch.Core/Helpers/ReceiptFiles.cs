using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;

public static class ReceiptFiles
{
    public const int MaximumBytes = 1_500_000;
    public static string DetectMime(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        if (bytes.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        throw new InvalidOperationException("Choose a PDF, JPG, PNG or WebP receipt file.");
    }
    public static Receipt Create(string name, byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes) throw new InvalidOperationException("Receipt files must be between 1 byte and 1.5 MB.");
        return new Receipt { Name = Path.GetFileName(name), Data = $"data:{DetectMime(bytes)};base64,{Convert.ToBase64String(bytes)}" };
    }
    public static byte[] Read(Receipt receipt)
    {
        var comma = receipt.Data.IndexOf(',');
        if (comma < 0 || receipt.Data.Length > 2_000_100) throw new InvalidOperationException("The receipt is invalid or exceeds 1.5 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(receipt.Data[(comma + 1)..]); }
        catch (FormatException) { throw new InvalidOperationException("The receipt file could not be read."); }
        if (bytes.Length == 0 || bytes.Length > MaximumBytes) throw new InvalidOperationException("The receipt is invalid or exceeds 1.5 MB.");
        var expected = $"data:{DetectMime(bytes)};base64,";
        if (!receipt.Data.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The receipt file type does not match its contents.");
        return bytes;
    }
}
