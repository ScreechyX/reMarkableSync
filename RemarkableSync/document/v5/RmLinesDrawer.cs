using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace RemarkableSync.document.v5
{
    public class RmLinesDrawer
    {
        static public List<Bitmap> DrawPages(List<RmPage> pages)
        {
            List<Bitmap> images = (from page in pages
                                   select DrawPage(page)).ToList();
            return images;
        }

        static public Bitmap DrawPage(RmPage page)
        {
            Bitmap image = new Bitmap(RmConstants.X_MAX, RmConstants.Y_MAX);

            Graphics graphics = Graphics.FromImage(image);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            graphics.Clear(Color.White);

            foreach (RmLayer layer in page.Objects)
            {
                DrawLayer(layer, ref graphics);
            }

            return image;
        }

        static private void DrawLayer(RmLayer layer, ref Graphics graphics)
        {
            foreach (RmStroke stroke in layer.Objects)
            {
                if (stroke.IsVisible())
                {
                    DrawStroke(stroke, ref graphics);
                }
            }
        }

        static private bool IsHighlighter(RmPen pen) =>
            pen == RmPen.HIGHLIGHTER_1 || pen == RmPen.HIGHLIGHTER_2;

        static private Color PenColorToColor(RmPenColor penColor, bool highlight)
        {
            int alpha = highlight ? 100 : 255;
            switch (penColor)
            {
                case RmPenColor.GREY:
                case RmPenColor.GRAY_OVERLAP:
                    return Color.FromArgb(alpha, 128, 128, 128);
                case RmPenColor.WHITE:
                    return Color.FromArgb(alpha, 255, 255, 255);
                case RmPenColor.YELLOW:
                    return Color.FromArgb(alpha, 255, 235, 50);
                case RmPenColor.GREEN:
                    return Color.FromArgb(alpha, 0, 180, 0);
                case RmPenColor.PINK:
                    return Color.FromArgb(alpha, 255, 105, 180);
                case RmPenColor.BLUE:
                    return Color.FromArgb(alpha, 50, 100, 255);
                case RmPenColor.RED:
                    return Color.FromArgb(alpha, 220, 30, 30);
                case RmPenColor.BLACK:
                default:
                    return Color.FromArgb(alpha, 0, 0, 0);
            }
        }

        static private void DrawStroke(RmStroke stroke, ref Graphics graphics)
        {
            bool highlight = IsHighlighter(stroke.Pen);
            Color color = PenColorToColor(stroke.Colour, highlight);
            float width = highlight ? stroke.Width * 8f : stroke.Width;

            Pen pen = new Pen(color, width);

            GraphicsPath path = new GraphicsPath();
            Point[] points = new Point[stroke.Objects.Count];
            for (int i = 0; i < stroke.Objects.Count; ++i)
            {
                RmSegment segment = (RmSegment)stroke.Objects[i];
                points[i] = new Point((int)segment.X, (int)segment.Y);
            }
            path.AddLines(points);
            graphics.DrawPath(pen, path);

            pen.Dispose();
        }

    }
}
