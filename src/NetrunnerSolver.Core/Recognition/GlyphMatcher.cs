using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public static class GlyphMatcher
{
    private const int FeatureWidth = 32;
    private const int FeatureHeight = 16;
    private const double Margin = 0.05;
    private const int MinContrast = 30;

    public static byte Classify(BgraImage image, Box target, IReadOnlyList<Token> samples, double minScore = 0.80)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(samples);
        var wanted = Features(image, target);
        if (wanted is null || samples.Count == 0)
        {
            return BreachCodes.Unknown;
        }

        var scores = new Dictionary<byte, double>();
        foreach (var sample in samples)
        {
            var features = Features(image, sample.Box);
            if (features is null)
            {
                continue;
            }

            var score = Correlate(wanted, features);
            if (!scores.TryGetValue(sample.Code, out var current) || score > current)
            {
                scores[sample.Code] = score;
            }
        }

        var ranked = scores.OrderByDescending(kv => kv.Value).ToList();
        if (ranked.Count == 0 || ranked[0].Value < minScore)
        {
            return BreachCodes.Unknown;
        }

        return ranked.Count > 1 && ranked[0].Value - ranked[1].Value < Margin ? BreachCodes.Unknown : ranked[0].Key;
    }

    private static double[]? Features(BgraImage image, Box box)
    {
        BgraImage crop;
        try
        {
            crop = image.Crop(box.Inflate(box.Width * 0.25, box.Height * 0.25));
        }
        catch (ArgumentException)
        {
            return null;
        }

        var ink = InkBounds(crop);
        if (ink is null)
        {
            return null;
        }

        var (inkX, inkY, inkWidth, inkHeight) = ink.Value;
        var values = new double[FeatureWidth * FeatureHeight];
        for (var y = 0; y < FeatureHeight; y++)
        {
            for (var x = 0; x < FeatureWidth; x++)
            {
                values[(y * FeatureWidth) + x] = Sample(
                    crop,
                    inkX + ((x + 0.5) * inkWidth / FeatureWidth) - 0.5,
                    inkY + ((y + 0.5) * inkHeight / FeatureHeight) - 0.5);
            }
        }

        var mean = values.Average();
        var deviation = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Length);
        if (deviation < 1e-6)
        {
            return null;
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (values[i] - mean) / deviation;
        }

        return values;
    }

    private static (int X, int Y, int Width, int Height)? InkBounds(BgraImage crop)
    {
        int min = 255, max = 0;
        for (var y = 0; y < crop.Height; y++)
        {
            for (var x = 0; x < crop.Width; x++)
            {
                var v = crop.Brightness(x, y);
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }

        if (max - min < MinContrast)
        {
            return null;
        }

        var threshold = min + ((max - min) / 2);
        int left = crop.Width, top = crop.Height, right = -1, bottom = -1;
        for (var y = 0; y < crop.Height; y++)
        {
            for (var x = 0; x < crop.Width; x++)
            {
                if (crop.Brightness(x, y) >= threshold)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < left ? null : (left, top, right - left + 1, bottom - top + 1);
    }

    private static double Sample(BgraImage image, double x, double y)
    {
        x = Math.Clamp(x, 0, image.Width - 1);
        y = Math.Clamp(y, 0, image.Height - 1);
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        int x1 = Math.Min(x0 + 1, image.Width - 1), y1 = Math.Min(y0 + 1, image.Height - 1);
        double fx = x - x0, fy = y - y0;
        var top = (image.Brightness(x0, y0) * (1 - fx)) + (image.Brightness(x1, y0) * fx);
        var bottom = (image.Brightness(x0, y1) * (1 - fx)) + (image.Brightness(x1, y1) * fx);
        return (top * (1 - fy)) + (bottom * fy);
    }

    private static double Correlate(double[] a, double[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum / a.Length;
    }
}
