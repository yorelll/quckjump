using System;
using System.IO;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Assets;

/// <summary>Verifies that shipped Flow Launcher icon assets are real transparent RGBA PNGs.</summary>
public sealed class PluginIconAssetTests
{
    [Theory]
    [InlineData("icon.png", 128, 128)]
    [InlineData("file.png", 64, 64)]
    [InlineData("folder.png", 64, 64)]
    public void IconAsset_IsExpectedTransparentRgbaPng(string fileName, int expectedWidth, int expectedHeight)
    {
        string path = Path.Combine(AppContext.BaseDirectory, fileName);
        byte[] png = File.ReadAllBytes(path);

        Assert.True(png.Length > 33, $"{fileName} is too small to be a valid PNG.");
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(expectedWidth, ReadBigEndianInt32(png, 16));
        Assert.Equal(expectedHeight, ReadBigEndianInt32(png, 20));
        Assert.Equal(8, png[24]); // Bit depth.
        Assert.Equal(6, png[25]); // PNG color type 6 = truecolor with alpha.
        Assert.True(HasTransparentBackground(png), $"{fileName} has no transparent pixels in decoded PNG scanlines.");
    }

    private static bool HasTransparentBackground(byte[] png)
    {
        // Source PNGs are deliberately non-interlaced; after reversing their
        // scanline filters, inspect the actual alpha bytes. This verifies the
        // declared RGBA channel is not merely opaque alpha data.
        using var compressed = new MemoryStream(CollectIdatData(png));
        using var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        byte[] filteredRows = raw.ToArray();
        int width = ReadBigEndianInt32(png, 16);
        int height = ReadBigEndianInt32(png, 20);
        int stride = width * 4;
        Assert.Equal(height * (stride + 1), filteredRows.Length);

        var previous = new byte[stride];
        for (int row = 0; row < height; row++)
        {
            int filteredStart = row * (stride + 1);
            byte filter = filteredRows[filteredStart];
            var current = new byte[stride];
            for (int index = 0; index < stride; index++)
            {
                byte left = index >= 4 ? current[index - 4] : (byte)0;
                byte above = previous[index];
                byte upperLeft = index >= 4 ? previous[index - 4] : (byte)0;
                current[index] = filter switch
                {
                    0 => filteredRows[filteredStart + 1 + index],
                    1 => (byte)(filteredRows[filteredStart + 1 + index] + left),
                    2 => (byte)(filteredRows[filteredStart + 1 + index] + above),
                    3 => (byte)(filteredRows[filteredStart + 1 + index] + ((left + above) / 2)),
                    4 => (byte)(filteredRows[filteredStart + 1 + index] + Paeth(left, above, upperLeft)),
                    _ => throw new InvalidDataException($"Unsupported PNG filter type {filter}."),
                };
            }

            for (int column = 0; column < width; column++)
            {
                if (current[column * 4 + 3] == 0)
                {
                    return true;
                }
            }
            previous = current;
        }
        return false;
    }

    private static byte Paeth(byte left, byte above, byte upperLeft)
    {
        int prediction = left + above - upperLeft;
        int leftDistance = Math.Abs(prediction - left);
        int aboveDistance = Math.Abs(prediction - above);
        int upperLeftDistance = Math.Abs(prediction - upperLeft);
        return leftDistance <= aboveDistance && leftDistance <= upperLeftDistance
            ? left
            : aboveDistance <= upperLeftDistance ? above : upperLeft;
    }

    private static byte[] CollectIdatData(byte[] png)
    {
        using var data = new MemoryStream();
        int position = 8;
        while (position + 12 <= png.Length)
        {
            int length = ReadBigEndianInt32(png, position);
            string type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
            if (type == "IDAT")
            {
                data.Write(png, position + 8, length);
            }
            position += length + 12;
        }
        return data.ToArray();
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];
}
