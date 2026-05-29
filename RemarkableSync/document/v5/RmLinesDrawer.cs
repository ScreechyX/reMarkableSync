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
            // Highlights first so ink renders on top
            foreach (RmStroke stroke in layer.Objects)
            {
                if (stroke.IsVisible() && IsHighlighter(stroke.Pen))
                    DrawStroke(stroke, ref graphics);
            }
            foreach (RmStroke stroke in layer.Objects)
            {
                if (stroke.IsVisible() && !IsHighlighter(stroke.Pen))
                {
                    DrawStroke(stroke, ref graphics);
                }
            }
        }

        static private bool IsHighlighter(RmPen pen) =>
            pen == RmPen.HIGHLIGHTER_1 || pen == RmPen.HIGHLIGHTER_2;

        // Alpha 64 = 25% opacity for highlights, matching reMarkable's rendering
        static private Color PenColorToColor(RmPenColor penColor, bool highlight)
        {
            int alpha = highlight ? 64 : 255;
            switch (penColor)
            {
                case RmPenColor.GREY:
                case RmPenColor.GRAY_OVERLAP:
                    return Color.FromArgb(alpha, 128, 128, 128);
                case RmPenColor.WHITE:
                    return Color.FromArgb(alpha, 255, 255, 255);
                case RmPenColor.YELLOW:
                    return Color.FromArgb(alpha, 255, 248, 0);
                case RmPenColor.GREEN:
                    return Color.FromArgb(alpha, 0, 168, 0);
                case RmPenColor.PINK:
                    return Color.FromArgb(alpha, 255, 82, 162);
                case RmPenColor.BLUE:
                    return Color.FromArgb(alpha, 0, 85, 255);
                case RmPenColor.RED:
                    return Color.FromArgb(alpha, 255, 0, 0);
                case RmPenColor.BLACK:
                default:
                    return Color.FromArgb(alpha, 0, 0, 0);
            }
        }

        static private void DrawStroke(RmStroke stroke, ref Graphics graphics)
        {
            bool highlight = IsHighlighter(stroke.Pen);
            Color color = PenColorToColor(stroke.Colour, highlight);
            // v5 Width field is already the actual stroke width in pixels
            float width = stroke.Width;

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
