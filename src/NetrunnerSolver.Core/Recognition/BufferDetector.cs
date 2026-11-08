using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public sealed record BufferReading(int SlotCount, bool HasContent, Box Bounds);

public static class BufferDetector
{
    private const double SpacingTolerance = 0.15;
    private const double MinCoverage = 0.5;
    private const int MaxSlots = 12;
    private const double MaxInteriorFill = 0.5;

    public static BufferReading? Detect(BgraImage image, GridGeometry grid)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(grid);
        var bounds = grid.Bounds;
        var top = Math.Clamp((int)Math.Floor(bounds.Y - (1.2 * bounds.Height)), 0, image.Height);
        var bottom = Math.Clamp((int)Math.Ceiling(bounds.Y - (0.3 * grid.PitchY)), 0, image.Height);
        var width = image.Width;
        var height = bottom - top;
        if (height < 4)
        {
            return null;
        }

        var mask = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                mask[(y * width) + x] = IsOutline(image, x, top + y);
            }
        }

        var radius = Math.Max(1, (int)Math.Round(0.12 * grid.CellHeight));
        var dilated = Dilate(mask, width, height, radius);
        var band = new Band(dilated, mask, width, height, top, radius);
        var squares = Components(dilated, width, height)
            .Where(b => b.Width / b.Height is >= 0.75 and <= 1.33
                && b.Height >= grid.CellHeight * 0.8
                && b.Height <= grid.CellHeight * 2.5)
            .Select(b => b.Offset(0, top))
            .Where(band.IsHollow)
            .ToList();

        var slots = BestRow(squares, band);
        if (slots.Count < 2)
        {
            return null;
        }

        var hasContent = slots.Any(slot => BrightInside(image, slot) > 0.15);
        var union = new Box(
            slots.Min(s => s.X),
            slots.Min(s => s.Y),
            slots.Max(s => s.Right) - slots.Min(s => s.X),
            slots.Max(s => s.Bottom) - slots.Min(s => s.Y));
        return new BufferReading(slots.Count, hasContent, union);
    }

    private static bool IsOutline(BgraImage image, int x, int y)
    {
        var i = ((y * image.Width) + x) * 4;
        var pixels = image.Pixels;
        byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
        return Math.Max(r, g) >= 55 && g >= b + 10;
    }

    private static bool[] Dilate(bool[] mask, int width, int height, int radius)
    {
        var horizontal = new bool[mask.Length];
        for (var y = 0; y < height; y++)
        {
            var last = -radius - 1;
            for (var x = 0; x < width; x++)
            {
                if (mask[(y * width) + x])
                {
                    last = x;
                }

                if (x - last <= radius)
                {
                    horizontal[(y * width) + x] = true;
                }
            }

            last = width + radius + 1;
            for (var x = width - 1; x >= 0; x--)
            {
                if (mask[(y * width) + x])
                {
                    last = x;
                }

                if (last - x <= radius)
                {
                    horizontal[(y * width) + x] = true;
                }
            }
        }

        var result = new bool[mask.Length];
        for (var x = 0; x < width; x++)
        {
            var last = -radius - 1;
            for (var y = 0; y < height; y++)
            {
                if (horizontal[(y * width) + x])
                {
                    last = y;
                }

                if (y - last <= radius)
                {
                    result[(y * width) + x] = true;
                }
            }

            last = height + radius + 1;
            for (var y = height - 1; y >= 0; y--)
            {
                if (horizontal[(y * width) + x])
                {
                    last = y;
                }

                if (last - y <= radius)
                {
                    result[(y * width) + x] = true;
                }
            }
        }

        return result;
    }

    private static List<Box> Components(bool[] mask, int width, int height)
    {
        var seen = new bool[mask.Length];
        var boxes = new List<Box>();
        var queue = new Queue<int>();
        for (var start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || seen[start])
            {
                continue;
            }

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                int x = index % width, y = index / width;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);
            }

            boxes.Add(new Box(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }

        return boxes;

        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            var i = (y * width) + x;
            if (mask[i] && !seen[i])
            {
                seen[i] = true;
                queue.Enqueue(i);
            }
        }
    }

    private static List<Box> BestRow(List<Box> squares, Band band)
    {
        var best = new List<Box>();
        foreach (var seed in squares)
        {
            var size = (seed.Width + seed.Height) / 2;
            var row = squares
                .Where(s => Math.Abs(s.CenterY - seed.CenterY) < 0.3 * size
                    && Math.Abs(s.Height - seed.Height) <= SpacingTolerance * size)
                .OrderBy(s => s.X)
                .ToList();
            var step = Step(row, size) ?? Probe(seed, size, band);
            var slots = new List<Box> { seed };
            if (step is { } pitch)
            {
                for (var box = seed.Offset(-pitch, 0); slots.Count < MaxSlots && band.IsSlot(box); box = box.Offset(-pitch, 0))
                {
                    slots.Insert(0, box);
                }

                for (var box = seed.Offset(pitch, 0); slots.Count < MaxSlots && band.IsSlot(box); box = box.Offset(pitch, 0))
                {
                    slots.Add(box);
                }
            }

            if (slots.Count > best.Count)
            {
                best = slots;
            }
        }

        return best;
    }

    private static double? Step(List<Box> row, double size)
    {
        double? step = null;
        for (var i = 1; i < row.Count; i++)
        {
            var gap = row[i].CenterX - row[i - 1].CenterX;
            if (gap >= 0.95 * size && (step is null || gap < step))
            {
                step = gap;
            }
        }

        return step;
    }

    private static double? Probe(Box seed, double size, Band band)
    {
        for (var d = Math.Ceiling(size); d <= 2.2 * size; d++)
        {
            if (band.IsSlot(seed.Offset(d, 0)) || band.IsSlot(seed.Offset(-d, 0)))
            {
                return d;
            }
        }

        return null;
    }

    private static double BrightInside(BgraImage image, Box slot)
    {
        var inner = slot.Inflate(-slot.Width / 4, -slot.Height / 4);
        int x0 = (int)Math.Ceiling(inner.X), y0 = (int)Math.Ceiling(inner.Y);
        int x1 = (int)Math.Floor(inner.Right), y1 = (int)Math.Floor(inner.Bottom);
        var total = 0;
        var bright = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(image.Height, y1); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(image.Width, x1); x++)
            {
                total++;
                if (image.Brightness(x, y) > 150)
                {
                    bright++;
                }
            }
        }

        return total == 0 ? 0 : (double)bright / total;
    }

    private sealed record Band(bool[] Lit, bool[] Raw, int Width, int Height, int Top, int Inset)
    {
        public bool IsSlot(Box box) => Coverage(box) >= MinCoverage && IsHollow(box);

        public bool IsHollow(Box box)
        {
            var inner = box.Inflate(-box.Width / 4, -box.Height / 4);
            var total = 0;
            var lit = 0;
            for (var y = (int)Math.Ceiling(inner.Y) - Top; y < (int)Math.Floor(inner.Bottom) - Top; y++)
            {
                for (var x = (int)Math.Ceiling(inner.X); x < (int)Math.Floor(inner.Right); x++)
                {
                    total++;
                    if (x >= 0 && y >= 0 && x < Width && y < Height && Raw[(y * Width) + x])
                    {
                        lit++;
                    }
                }
            }

            return total > 0 && lit < total * MaxInteriorFill;
        }

        public double Coverage(Box box)
        {
            var left = (int)Math.Round(box.X) + Inset;
            var right = (int)Math.Round(box.Right) - 1 - Inset;
            var top = (int)Math.Round(box.Y) - Top + Inset;
            var bottom = (int)Math.Round(box.Bottom) - 1 - Top - Inset;
            if (right <= left || bottom <= top)
            {
                return 0;
            }

            var total = 0;
            var lit = 0;
            for (var x = left; x <= right; x++)
            {
                Count(x, top);
                Count(x, bottom);
            }

            for (var y = top + 1; y < bottom; y++)
            {
                Count(left, y);
                Count(right, y);
            }

            return total == 0 ? 0 : (double)lit / total;

            void Count(int x, int y)
            {
                total++;
                if (x >= 0 && y >= 0 && x < Width && y < Height && Lit[(y * Width) + x])
                {
                    lit++;
                }
            }
        }
    }
}
