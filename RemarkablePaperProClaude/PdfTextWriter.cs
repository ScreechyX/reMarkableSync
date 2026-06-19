using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Generates a minimal, dependency-free PDF from plain text using the standard
    /// Helvetica font. The reMarkable renders PDFs with a real PDF engine, so the
    /// file must carry a valid cross-reference table — this builder tracks byte
    /// offsets to emit one. Text is laid out with a simple word-wrap and paginated.
    /// </summary>
    public static class PdfTextWriter
    {
        // A4 portrait, in PostScript points (1/72"). The device scales pages to fit.
        private const double PageWidth = 595.0;
        private const double PageHeight = 842.0;
        private const double Margin = 54.0;
        private const double FontSize = 22.0;
        private const double Leading = 30.0;

        public static byte[] Create(string text)
        {
            List<string> lines = LayoutLines(text ?? string.Empty);
            List<List<string>> pages = Paginate(lines);
            if (pages.Count == 0)
                pages.Add(new List<string>());
            return Build(pages);
        }

        private static List<string> LayoutLines(string text)
        {
            // Helvetica is proportional; estimate average glyph width to pick a wrap width.
            int maxChars = Math.Max(10, (int)((PageWidth - 2 * Margin) / (FontSize * 0.5)));
            var outLines = new List<string>();
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

            foreach (string rawParagraph in normalized.Split('\n'))
            {
                string paragraph = rawParagraph.TrimEnd();
                if (paragraph.Length == 0)
                {
                    outLines.Add(string.Empty);
                    continue;
                }

                var current = new StringBuilder();
                foreach (string rawWord in paragraph.Split(' '))
                {
                    string word = rawWord;
                    if (word.Length == 0)
                        continue;

                    // Hard-wrap a single word that is longer than a whole line.
                    while (word.Length > maxChars)
                    {
                        if (current.Length > 0)
                        {
                            outLines.Add(current.ToString());
                            current.Length = 0;
                        }
                        outLines.Add(word.Substring(0, maxChars));
                        word = word.Substring(maxChars);
                    }

                    if (current.Length == 0)
                        current.Append(word);
                    else if (current.Length + 1 + word.Length <= maxChars)
                        current.Append(' ').Append(word);
                    else
                    {
                        outLines.Add(current.ToString());
                        current.Length = 0;
                        current.Append(word);
                    }
                }

                if (current.Length > 0)
                    outLines.Add(current.ToString());
            }

            return outLines;
        }

        private static List<List<string>> Paginate(List<string> lines)
        {
            int linesPerPage = Math.Max(1, (int)((PageHeight - 2 * Margin) / Leading));
            var pages = new List<List<string>>();
            for (int i = 0; i < lines.Count; i += linesPerPage)
            {
                var page = new List<string>();
                for (int j = i; j < Math.Min(i + linesPerPage, lines.Count); j++)
                    page.Add(lines[j]);
                pages.Add(page);
            }
            return pages;
        }

        private static byte[] Build(List<List<string>> pages)
        {
            int pageCount = pages.Count;
            const int fontObj = 3;
            const int firstPageObj = 4;

            // Objects are emitted in object-number order: 1 Catalog, 2 Pages, 3 Font,
            // then (page, content) pairs so page i -> obj (4 + 2i), content -> (5 + 2i).
            var objects = new List<string>();

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");

            var kids = new StringBuilder();
            for (int i = 0; i < pageCount; i++)
            {
                if (i > 0) kids.Append(' ');
                kids.Append((firstPageObj + i * 2).ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
            }
            objects.Add($"<< /Type /Pages /Kids [ {kids} ] /Count {pageCount} >>");

            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

            for (int i = 0; i < pageCount; i++)
            {
                int pageObjNum = firstPageObj + i * 2;
                int contentObjNum = pageObjNum + 1;

                objects.Add(
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Fmt(PageWidth)} {Fmt(PageHeight)}] " +
                    $"/Resources << /Font << /F1 {fontObj} 0 R >> >> /Contents {contentObjNum} 0 R >>");
                objects.Add(BuildContentStream(pages[i]));
            }

            // Latin-1 keeps a strict 1 byte per char so the xref offsets stay accurate.
            Encoding enc = Encoding.GetEncoding("ISO-8859-1");
            var ms = new MemoryStream();
            Action<string> write = s =>
            {
                byte[] b = enc.GetBytes(s);
                ms.Write(b, 0, b.Length);
            };

            write("%PDF-1.4\n");
            write("%\xE2\xE3\xCF\xD3\n"); // binary marker so tools treat the file as binary

            int n = objects.Count;
            var offsets = new int[n + 1];
            for (int i = 0; i < n; i++)
            {
                int objNum = i + 1;
                offsets[objNum] = (int)ms.Length;
                write($"{objNum} 0 obj\n{objects[i]}\nendobj\n");
            }

            int xrefOffset = (int)ms.Length;
            write($"xref\n0 {n + 1}\n");
            write("0000000000 65535 f \n");
            for (int objNum = 1; objNum <= n; objNum++)
                write($"{offsets[objNum].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");

            write($"trailer\n<< /Size {n + 1} /Root 1 0 R >>\n");
            write($"startxref\n{xrefOffset}\n");
            write("%%EOF\n");

            return ms.ToArray();
        }

        private static string BuildContentStream(List<string> lines)
        {
            var sb = new StringBuilder();
            sb.Append("BT\n");
            sb.Append($"/F1 {Fmt(FontSize)} Tf\n");
            sb.Append($"{Fmt(Leading)} TL\n");
            sb.Append($"1 0 0 1 {Fmt(Margin)} {Fmt(PageHeight - Margin)} Tm\n");

            bool first = true;
            foreach (string line in lines)
            {
                if (!first)
                    sb.Append("T*\n"); // advance one line using the leading set above
                sb.Append('(').Append(EscapePdfString(line)).Append(") Tj\n");
                first = false;
            }
            sb.Append("ET");

            string streamData = sb.ToString();
            int length = Encoding.GetEncoding("ISO-8859-1").GetByteCount(streamData);
            return $"<< /Length {length} >>\nstream\n{streamData}\nendstream";
        }

        private static string EscapePdfString(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (c == '\\' || c == '(' || c == ')')
                    sb.Append('\\').Append(c);
                else if (c == '\t')
                    sb.Append("    ");
                else if (c < 32)
                    sb.Append(' ');
                else if (c > 255)
                    sb.Append('?'); // outside WinAnsi/Latin-1; keep the byte stream valid
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Fmt(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
