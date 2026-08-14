using Microsoft.Maui.Graphics;

namespace Firetrack.Converters   // or Firetrack.Helpers – adjust to match your folder
{
    public class ChartDrawable : IDrawable
    {
        public List<float> DataPoints { get; set; } = new();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // If not enough data, show a message
            if (DataPoints.Count < 2)
            {
                canvas.FontColor = Colors.Gray;
                canvas.FontSize = 14;
                // ✅ Use the RectF overload with alignment
                canvas.DrawString("No data available", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
                return;
            }

            float width = dirtyRect.Width;
            float height = dirtyRect.Height;
            float padding = 30;

            float maxVal = DataPoints.Max();
            float minVal = DataPoints.Min();
            float range = maxVal - minVal;
            if (range == 0) range = 1;

            float graphWidth = width - 2 * padding;
            float graphHeight = height - 2 * padding;

            // Axes
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 1;
            canvas.DrawLine(padding, padding, padding, height - padding);
            canvas.DrawLine(padding, height - padding, width - padding, height - padding);

            // Data line
            var path = new PathF();
            for (int i = 0; i < DataPoints.Count; i++)
            {
                float x = padding + (i / (float)(DataPoints.Count - 1)) * graphWidth;
                float y = height - padding - ((DataPoints[i] - minVal) / range) * graphHeight;
                if (i == 0) path.MoveTo(x, y);
                else path.LineTo(x, y);
            }

            canvas.StrokeColor = Colors.DeepSkyBlue;
            canvas.StrokeSize = 3;
            canvas.DrawPath(path);

            // Data points
            for (int i = 0; i < DataPoints.Count; i++)
            {
                float x = padding + (i / (float)(DataPoints.Count - 1)) * graphWidth;
                float y = height - padding - ((DataPoints[i] - minVal) / range) * graphHeight;
                canvas.FillColor = Colors.White;
                canvas.StrokeColor = Colors.DeepSkyBlue;
                canvas.StrokeSize = 2;
                canvas.DrawCircle(x, y, 4);
                canvas.FillCircle(x, y, 4);
            }
        }
    }
}