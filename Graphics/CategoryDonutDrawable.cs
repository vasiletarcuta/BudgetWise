using BudgetWise.Services;
using System.Globalization;
using Microsoft.Maui.Graphics;

namespace BudgetWise.Graphics;

public sealed record CategorySlice(string Name, long AmountMinor, Color Color);

public sealed class CategoryDonutDrawable : IDrawable
{
    private static readonly Color ChartBackground = Palette.CardAlt;
    private static readonly Color MutedText = Palette.Muted;
    private static readonly Color MainText = Palette.Text;

    public IReadOnlyList<CategorySlice> Slices { get; set; } = Array.Empty<CategorySlice>();

    public string CenterCaption { get; set; } = "TOTAL EXPENSES";

    public void Draw(ICanvas canvas, RectF rect)
    {
        float size = Math.Min(rect.Width - 24, rect.Height - 24);
        if (size <= 30)
            return;

        float x = rect.Center.X - size / 2;
        float y = rect.Center.Y - size / 2;
        long total = Slices.Where(slice => slice.AmountMinor > 0).Sum(slice => slice.AmountMinor);

        canvas.FillColor = Palette.Grid;
        canvas.FillEllipse(x, y, size, size);

        float centerX = rect.Center.X;
        float centerY = rect.Center.Y;
        float outerRadius = size / 2;
        float innerRadius = outerRadius * 0.59f;

        if (total > 0)
        {
            float startAngle = -90;
            foreach (CategorySlice slice in Slices)
            {
                if (slice.AmountMinor <= 0)
                    continue;

                float sweep = (float)((double)slice.AmountMinor / total * 360);
                canvas.FillColor = slice.Color;

                if (sweep >= 359.99f)
                {
                    canvas.FillEllipse(x, y, size, size);
                }
                else
                {
                    var segment = new PathF();
                    int steps = Math.Max(2, (int)Math.Ceiling(sweep));

                    PointF outerStart = GetPoint(centerX, centerY, outerRadius, startAngle);
                    segment.MoveTo(outerStart.X, outerStart.Y);

                    for (int step = 1; step <= steps; step++)
                    {
                        PointF outerPoint = GetPoint(
                            centerX, centerY, outerRadius, startAngle + sweep * step / steps);
                        segment.LineTo(outerPoint.X, outerPoint.Y);
                    }

                    for (int step = steps; step >= 0; step--)
                    {
                        PointF innerPoint = GetPoint(
                            centerX, centerY, innerRadius, startAngle + sweep * step / steps);
                        segment.LineTo(innerPoint.X, innerPoint.Y);
                    }

                    segment.Close();
                    canvas.FillPath(segment);
                }

                startAngle += sweep;
            }
        }

        float innerSize = innerRadius * 2;
        float innerX = centerX - innerRadius;
        float innerY = centerY - innerRadius;
        canvas.FillColor = ChartBackground;
        canvas.FillEllipse(innerX, innerY, innerSize, innerSize);

        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontColor = MutedText;
        canvas.FontSize = 11;
        canvas.DrawString(CenterCaption, innerX, rect.Center.Y - 19, innerSize, 18,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontColor = MainText;
        canvas.FontSize = 17;
        string amount = (total / 100m).ToString("N0", CultureInfo.GetCultureInfo("ro-RO"));
        canvas.DrawString(amount + " RON", innerX, rect.Center.Y + 1, innerSize, 26,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static PointF GetPoint(float centerX, float centerY, float radius, float angleDegrees)
    {
        float angle = angleDegrees * MathF.PI / 180;
        return new PointF(
            centerX + radius * MathF.Cos(angle),
            centerY + radius * MathF.Sin(angle));
    }
}
