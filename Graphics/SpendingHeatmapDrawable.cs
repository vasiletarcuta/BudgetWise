using BudgetWise.Services;
using Microsoft.Maui.Graphics;

namespace BudgetWise.Graphics;

// Month calendar (Monday first) where every day is shaded by how much was spent on it.
public sealed class SpendingHeatmapDrawable : IDrawable
{
    private const float Gap = 4f;
    private const float HeaderHeight = 22f;

    private static readonly string[] Weekdays = { "M", "T", "W", "T", "F", "S", "S" };
    private static readonly Color EmptyDay = Palette.HeatEmpty;
    private static readonly Color MutedText = Palette.Muted;
    private static readonly Color MainText = Palette.Text;
    private static readonly Color DarkText = Palette.OnPrimary;
    private static readonly Color SelectedBorder = Palette.Border;

    public DateTime Month { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    // Day of month -> expenses that day, in bani.
    public IReadOnlyDictionary<int, long> DailyExpenses { get; set; } = new Dictionary<int, long>();

    // 0 means no day is selected.
    public int SelectedDay { get; set; }

    public static Color ColorForIntensity(float intensity)
    {
        float t = Math.Clamp(intensity, 0f, 1f);
        return t < 0.5f
            ? Blend(Palette.Primary, Palette.Butter, t * 2)
            : Blend(Palette.Butter, Palette.Expense, (t - 0.5f) * 2);
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        return new Color(
            from.Red + (to.Red - from.Red) * amount,
            from.Green + (to.Green - from.Green) * amount,
            from.Blue + (to.Blue - from.Blue) * amount);
    }

    public static Color EmptyDayColor => EmptyDay;

    public void Draw(ICanvas canvas, RectF rect)
    {
        var grid = GetGrid(rect.Width, rect.Height);
        if (grid.CellWidth <= 4 || grid.CellHeight <= 4)
            return;

        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontColor = MutedText;
        canvas.FontSize = 11;
        for (int column = 0; column < 7; column++)
        {
            canvas.DrawString(Weekdays[column],
                column * (grid.CellWidth + Gap), 0, grid.CellWidth, HeaderHeight,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        long max = DailyExpenses.Count == 0 ? 0 : DailyExpenses.Values.Max();

        for (int day = 1; day <= grid.Days; day++)
        {
            RectF cell = GetCell(grid, day);
            long spent = DailyExpenses.GetValueOrDefault(day);
            float intensity = max > 0 ? (float)spent / max : 0f;

            canvas.FillColor = spent > 0 ? ColorForIntensity(intensity) : EmptyDay;
            canvas.FillRectangle(cell);

            if (day == SelectedDay)
            {
                canvas.StrokeColor = SelectedBorder;
                canvas.StrokeSize = 3;
                canvas.DrawRectangle(cell.X + 1.5f, cell.Y + 1.5f, cell.Width - 3, cell.Height - 3);
            }

            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontColor = spent > 0 ? DarkText : MutedText;
            canvas.FontSize = 12;
            canvas.DrawString(day.ToString(), cell.X, cell.Y, cell.Width, cell.Height,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    // Returns the day of month under the point, or 0 when the point is not on a day.
    public int HitTest(float x, float y, float width, float height)
    {
        var grid = GetGrid(width, height);
        for (int day = 1; day <= grid.Days; day++)
        {
            if (GetCell(grid, day).Contains(x, y))
                return day;
        }

        return 0;
    }

    private readonly record struct Grid(float CellWidth, float CellHeight, int Offset, int Days);

    private Grid GetGrid(float width, float height)
    {
        int offset = ((int)Month.DayOfWeek + 6) % 7;
        int days = DateTime.DaysInMonth(Month.Year, Month.Month);
        int rows = (offset + days + 6) / 7;

        float cellWidth = (width - Gap * 6) / 7;
        float cellHeight = (height - HeaderHeight - Gap * (rows - 1)) / rows;
        return new Grid(cellWidth, Math.Min(cellHeight, cellWidth), offset, days);
    }

    private static RectF GetCell(Grid grid, int day)
    {
        int index = grid.Offset + day - 1;
        int column = index % 7;
        int row = index / 7;
        return new RectF(
            column * (grid.CellWidth + Gap),
            HeaderHeight + row * (grid.CellHeight + Gap),
            grid.CellWidth,
            grid.CellHeight);
    }
}
