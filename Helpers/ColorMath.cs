namespace GameLauncher;
public static class ColorMath
{
    public static string ToHex(System.Windows.Media.Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static (int C, int M, int Y, int K) RgbToCmyk(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;
        double k = 1 - Math.Max(rd, Math.Max(gd, bd));

        if (Math.Abs(k - 1.0) < 0.00001)
        {
            return (0, 0, 0, 100);
        }

        double c = (1 - rd - k) / (1 - k);
        double m = (1 - gd - k) / (1 - k);
        double y = (1 - bd - k) / (1 - k);
        return ((int)Math.Round(c * 100), (int)Math.Round(m * 100), (int)Math.Round(y * 100), (int)Math.Round(k * 100));
    }

    public static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double h;
        double s;
        double l = (max + min) / 2;

        if (Math.Abs(max - min) < 0.00001)
        {
            h = 0;
            s = 0;
        }
        else
        {
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

            if (Math.Abs(max - rd) < 0.00001)
            {
                h = (gd - bd) / d + (gd < bd ? 6 : 0);
            }
            else if (Math.Abs(max - gd) < 0.00001)
            {
                h = (bd - rd) / d + 2;
            }
            else
            {
                h = (rd - gd) / d + 4;
            }

            h *= 60;
        }

        return (h, s, l);
    }

    public static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        if (Math.Abs(delta) < 0.00001)
        {
            h = 0;
        }
        else if (Math.Abs(max - rd) < 0.00001)
        {
            h = 60 * (((gd - bd) / delta) % 6);
        }
        else if (Math.Abs(max - gd) < 0.00001)
        {
            h = 60 * (((bd - rd) / delta) + 2);
        }
        else
        {
            h = 60 * (((rd - gd) / delta) + 4);
        }

        if (h < 0)
        {
            h += 360;
        }

        s = Math.Abs(max) < 0.00001 ? 0 : delta / max;
        v = max;
    }

    public static System.Windows.Media.Color HsvToRgb(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0 % 2) - 1));
        double m = v - c;

        double r1;
        double g1;
        double b1;

        if (h < 60)
        {
            r1 = c; g1 = x; b1 = 0;
        }
        else if (h < 120)
        {
            r1 = x; g1 = c; b1 = 0;
        }
        else if (h < 180)
        {
            r1 = 0; g1 = c; b1 = x;
        }
        else if (h < 240)
        {
            r1 = 0; g1 = x; b1 = c;
        }
        else if (h < 300)
        {
            r1 = x; g1 = 0; b1 = c;
        }
        else
        {
            r1 = c; g1 = 0; b1 = x;
        }

        return System.Windows.Media.Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    public static System.Windows.Media.Color? TryParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var value = hex.Trim();
        if (!value.StartsWith('#'))
        {
            value = $"#{value}";
        }

        if (value.Length != 7)
        {
            return null;
        }

        try
        {
            var r = Convert.ToByte(value.Substring(1, 2), 16);
            var g = Convert.ToByte(value.Substring(3, 2), 16);
            var b = Convert.ToByte(value.Substring(5, 2), 16);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }
        catch
        {
            return null;
        }
    }
}