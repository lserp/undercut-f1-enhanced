using System.Text;
using SkiaSharp;

namespace UndercutF1.Console.Graphics;

public static class Sixel
{
    private const char CarriageReturn = '$';
    private const char LineFeed = '-';
    private const byte ColourSpace = 4;

    /// <summary>
    /// Converts the given list of pixels (representing an image) in to a set of encoded sixels.
    /// Only the sixel data is returned, without the escape code header and trailer.
    /// </summary>
    public static string ImageToSixel(SKColor[] pixels, int width)
    {
        pixels = pixels.Select(ReducedColorSpace).ToArray();

        var colourIdToColour = pixels
            .Distinct()
            .Where(x => x.Alpha > 0)
            .Select((colour, i) => (colour, i))
            .ToDictionary(x => x.colour, x => x.i);

        var sixels = pixels
            .Chunk(width) // Make a 2d array of pixels
            .Chunk(6) // Process chunks of 6 rows at a time, full width
            .SelectMany(rowChunk => SixelChars(colourIdToColour, rowChunk))
            .ToArray();

        var colourRegisters = GetColourRegister(colourIdToColour);
        var sixelData = new string(sixels);

        return colourRegisters + sixelData;
    }

    private static string SixelChars(Dictionary<SKColor, int> colourMap, SKColor[][] rows)
    {
        // rows will be a 6 * width array of colours
        // process each row and create sixel characters
        // each row needs to be processed for each colour
        var sixels = new StringBuilder();
        foreach (var (colour, colourId) in colourMap)
        {
            var rowOfSixelsForColour = SixelCharsForColour(colour, colourId, rows);
            if (rowOfSixelsForColour.Any(x => x > 63))
            {
                // Only append the sixels if they actually draw any colour
                sixels.Append(rowOfSixelsForColour);
            }
        }

        // Add a line feed as all colours have been output for the current chunk of rows
        sixels.Append(LineFeed);

        return sixels.ToString();
    }

    private static string SixelCharsForColour(SKColor colour, int colourId, SKColor[][] rows)
    {
        // TODO: Instead of outputting a sixel for every column,
        // we should use the !repeat feature to output adjacent identical columns efficently
        var sixels = new char[rows[0].Length];
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] == colour)
                {
                    // Use the row number to set the correct bit of the sixel for the current column
                    sixels[column] |= (char)(1 << row);
                }
            }
        }

        // Output a line for the current colour, ending with a carriage return so the cursor is ready to draw the next colour
        return $"#{colourId}"
            + new string(sixels.Select(c => (char)(c + 63)).ToArray())
            + CarriageReturn;
    }

    private static string GetColourRegister(Dictionary<SKColor, int> colours)
    {
        var register = new StringBuilder();
        foreach (var (colour, id) in colours)
        {
            // colour register is #id;colourType;red;green;blue
            // colourType=2 is RGB
            // r/g/b is out of 100 instead of 255
            register.Append(
                $"#{id};2;{colour.Red * 100 / ColourSpace};{colour.Green * 100 / ColourSpace};{colour.Blue * 100 / ColourSpace}"
            );
        }
        return register.ToString();
    }

    private static SKColor ReducedColorSpace(SKColor colour) =>
        new(
            (byte)(colour.Red * ColourSpace / 255),
            (byte)(colour.Green * ColourSpace / 255),
            (byte)(colour.Blue * ColourSpace / 255),
            (byte)(colour.Alpha > 50 ? 1 : 0)
        );
}
