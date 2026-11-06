using BudgetWise.Services;
using System.Globalization;
using Microsoft.Maui.Graphics;

namespace BudgetWise.Graphics;

public sealed record MonthlyChartPoint(string Month, long IncomeMinor, long ExpenseMinor);

public sealed class MonthlyBarsDrawable : IDrawable
{
    private static readonly Color GridColor = Palette.Grid;
    private static readonly Color MutedText = Palette.Muted;
    private static readonly Color IncomeColor = Palette.Income;
    private static readonly Color ExpenseColor = Palette.Expense;

    public IReadOnlyList<MonthlyChartPoint> Points { get; set; } = Array.Empty<MonthlyChartPoint>();

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Points.Count == 0 || rect.Width < 80 || rect.Height < 80)
            return;

        float left = 8;
        float right = rect.Width - 8;
        float top = 12;
        float bottom = rect.Height - 30;
        float chartHeight = bottom - top;
        float chartWidth = right - left;

        if (chartHeight <= 0 || chartWidth <= 0)
            return;

        double maxAmount = Math.Max(1, Points.Max(point =>
            Math.Max((double)point.IncomeMinor, point.ExpenseMinor)));

        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;
        for (int line = 0; line <= 3; line++)
        {
            float y = top + chartHeight * line / 3;
            canvas.DrawLine(left, y, right, y);
        }

        float groupWidth = chartWidth / Points.Count;
        float barWidth = Math.Min(15, groupWidth * 0.27f);
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 10;
        canvas.FontColor = MutedText;

        for (int index = 0; index < Points.Count; index++)
        {
            MonthlyChartPoint point = Points[index];
            float centerX = left + groupWidth * (index + 0.5f);
            float incomeHeight = (float)(point.IncomeMinor / maxAmount * (chartHeight - 4));
            float expenseHeight = (float)(point.ExpenseMinor / maxAmount * (chartHeight - 4));

            if (incomeHeight > 0)
            {
                canvas.FillColor = IncomeColor;
                canvas.FillRectangle(centerX - barWidth - 2, bottom - incomeHeight, barWidth, incomeHeight);
            }

            if (expenseHeight > 0)
            {
                canvas.FillColor = ExpenseColor;
                canvas.FillRectangle(centerX + 2, bottom - expenseHeight, barWidth, expenseHeight);
            }

            canvas.DrawString(point.Month, left + groupWidth * index, bottom + 5, groupWidth, 20,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
