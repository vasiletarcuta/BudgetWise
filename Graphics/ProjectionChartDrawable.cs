using BudgetWise.Services;
using System.Globalization;
using Microsoft.Maui.Graphics;

namespace BudgetWise.Graphics;

// Two balance projections over time: "nothing changes" vs. the what-if scenario.
public sealed class ProjectionChartDrawable : IDrawable
{
    private const float Left = 50f;
    private const float Right = 14f;
    private const float Top = 14f;
    private const float Bottom = 28f;

    private static readonly Color GridColor = Palette.Grid;
    private static readonly Color MutedText = Palette.Muted;
    private static readonly Color BaselineColor = Palette.Muted;
    private static readonly Color ScenarioColor = Palette.PrimaryText;

    public static Color Baseline => BaselineColor;
    public static Color Scenario => ScenarioColor;

    // Balance in RON at month 0, 1, 2, ...
    public IReadOnlyList<double> BaselineSeries { get; set; } = Array.Empty<double>();
    public IReadOnlyList<double> ScenarioSeries { get; set; } = Array.Empty<double>();

    public void Draw(ICanvas canvas, RectF rect)
    {
        int count = BaselineSeries.Count;
        if (count < 2 || ScenarioSeries.Count != count)
            return;

        float width = rect.Width - Left - Right;
        float height = rect.Height - Top - Bottom;
        if (width <= 10 || height <= 10)
            return;

        double min = Math.Min(BaselineSeries.Min(), ScenarioSeries.Min());
        double max = Math.Max(BaselineSeries.Max(), ScenarioSeries.Max());
        if (max - min < 1)
            max = min + 1;

        float X(int index) => Left + width * index / (count - 1);
        float Y(double value) => Top + height * (float)((max - value) / (max - min));

        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 10;
        canvas.FontColor = MutedText;
        canvas.StrokeSize = 1;
        canvas.StrokeColor = GridColor;

        for (int line = 0; line <= 3; line++)
        {
            double value = min + (max - min) * line / 3;
            float y = Y(value);
            canvas.DrawLine(Left, y, Left + width, y);
            canvas.DrawString(Compact(value), 0, y - 8, Left - 6, 16,
                HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        DrawSeries(canvas, BaselineSeries, BaselineColor, 3f, X, Y);
        DrawSeries(canvas, ScenarioSeries, ScenarioColor, 4f, X, Y);

        canvas.FontColor = MutedText;
        canvas.DrawString("now", Left - 20, Top + height + 6, 40, 16,
            HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString($"+{count - 1} mo", Left + width - 30, Top + height + 6, 44, 16,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawSeries(
        ICanvas canvas, IReadOnlyList<double> series, Color color, float thickness,
        Func<int, float> x, Func<double, float> y)
    {
        var path = new PathF();
        path.MoveTo(x(0), y(series[0]));
        for (int index = 1; index < series.Count; index++)
            path.LineTo(x(index), y(series[index]));

        canvas.StrokeColor = color;
        canvas.StrokeSize = thickness;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(path);

        canvas.FillColor = color;
        int last = series.Count - 1;
        canvas.FillCircle(x(last), y(series[last]), thickness + 1.5f);
    }

    private static string Compact(double value)
    {
        double abs = Math.Abs(value);
        return abs >= 1000
            ? (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "k"
            : value.ToString("0", CultureInfo.InvariantCulture);
    }
}
