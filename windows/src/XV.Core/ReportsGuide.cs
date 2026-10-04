using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.Rendering;

namespace XV.Core;

public static partial class Reports
{
    const string GuideBlue = "#4F46E5";

    /// <summary>
    /// Writes the user guide as one PDF holding the English and the Hinglish guide. The cover and the top of every page have
    /// "English | Hinglish" links, and every chapter has a link to the same chapter in the other language, so the reader switches at once.
    /// Screenshots are taken from <paramref name="shotsDir"/> when the files exist (sample data only).
    /// </summary>
    public static int GuidePdf(string path, string? shotsDir, string version, string serverName = "", bool hinglishFirst = false)
    {
        EnsureFonts();
        var doc = new Document();
        doc.Info.Title = GuideContent.Product + " - User Guide / Upyog Margdarshika";
        doc.Info.Author = GuideContent.Maker;
        doc.Styles[StyleNames.Normal]!.Font.Name = "XV Sans";
        doc.Styles[StyleNames.Normal]!.Font.Size = 10;

        void Link(Paragraph p, string bookmark, string text, bool bold = true, string color = GuideBlue, double size = 0)
        {
            var h = p.AddHyperlink(bookmark, HyperlinkType.Bookmark);
            var f = h.AddFormattedText(text, bold ? TextFormat.Bold | TextFormat.Underline : TextFormat.Underline);
            f.Font.Color = C(color); if (size > 0) f.Font.Size = size;
        }

        var cover = doc.AddSection();
        cover.PageSetup.PageFormat = PageFormat.A4;
        cover.PageSetup.LeftMargin = cover.PageSetup.RightMargin = Unit.FromCentimeter(2);
        cover.PageSetup.TopMargin = Unit.FromCentimeter(2); cover.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        var band = cover.AddTable(); band.AddColumn(Unit.FromCentimeter(17));
        var br = band.AddRow(); br.Shading.Color = C(GuideBlue); br.TopPadding = br.BottomPadding = Unit.FromPoint(40);
        var bp = br.Cells[0].AddParagraph(); bp.Format.LeftIndent = Unit.FromPoint(16);
        var t1 = bp.AddFormattedText("USER GUIDE", TextFormat.Bold); t1.Font.Size = 34; t1.Font.Color = Colors.White;
        bp.AddLineBreak();
        var t1b = bp.AddFormattedText("Upyog Margdarshika (Hinglish)", TextFormat.Bold); t1b.Font.Size = 17; t1b.Font.Color = C("#C7D2FE");
        bp.AddLineBreak();
        var t2 = bp.AddFormattedText(GuideContent.Product + " - Command Center and Gatekeeper phone app"); t2.Font.Size = 13; t2.Font.Color = C("#E0E7FF");
        var meta = cover.AddParagraph($"Version {version}   •   Created by {GuideContent.Maker}");
        meta.Format.SpaceBefore = Unit.FromPoint(14); meta.Format.Font.Size = 11; meta.Format.Font.Color = C("#475569");

        var pick = cover.AddParagraph(); pick.Format.SpaceBefore = Unit.FromPoint(22);
        pick.AddFormattedText("Choose language  /  Bhasha chuniye:   ", TextFormat.Bold).Font.Size = 12;
        Link(pick, "part-en", "[ English ]", size: 14); pick.AddText("      "); Link(pick, "part-hi", "[ Hinglish ]", size: 14);
        var lead = cover.AddParagraph("Click a language to open that guide. Every page has the same switch at the top, and every chapter has a link to the same chapter in the other language.\nKisi bhi bhasha par click kijiye. Har page ke upar yahi switch hai, aur har chapter mein doosri bhasha ke usi chapter ka link hai.");
        lead.Format.SpaceBefore = Unit.FromPoint(8); lead.Format.Font.Size = 10; lead.Format.Font.Color = C("#475569");

        var body = doc.AddSection();
        body.PageSetup.PageFormat = PageFormat.A4;
        body.PageSetup.LeftMargin = body.PageSetup.RightMargin = Unit.FromCentimeter(2);
        body.PageSetup.TopMargin = Unit.FromCentimeter(2.2); body.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        body.PageSetup.HeaderDistance = Unit.FromCentimeter(0.9);
        var head = body.Headers.Primary.AddParagraph();
        head.Format.Alignment = ParagraphAlignment.Right; head.Format.Font.Size = 9;
        head.AddText("Language / Bhasha:  "); Link(head, "part-en", "English", size: 9); head.AddText("  |  "); Link(head, "part-hi", "Hinglish", size: 9);
        var footer = body.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7.5; footer.Format.Font.Color = C("#71717A");
        footer.AddText($"{GuideContent.Product} • User Guide • {GuideContent.Maker} • Page "); footer.AddPageField(); footer.AddText(" of "); footer.AddNumPagesField();

        var shown = 0;
        void Part(string code, GuideSection[] sections, string other, string otherName, string partTitle, string contentsTitle, string noteWord, bool first)
        {
            var title = first ? body.AddParagraph() : body.AddParagraph();
            if (!first) title.Format.PageBreakBefore = true;
            title.AddBookmark("part-" + code);
            title.Format.Font.Size = 24; title.Format.Font.Bold = true; title.Format.Font.Color = C(GuideBlue);
            title.AddText(partTitle);
            var sw = body.AddParagraph(); sw.Format.SpaceAfter = Unit.FromPoint(6);
            sw.AddText(otherName == "Hinglish" ? "Switch to: " : "Is guide ko English mein padhne ke liye: "); Link(sw, "part-" + other, otherName);
            var toc = body.AddParagraph(contentsTitle); toc.Format.Font.Bold = true; toc.Format.Font.Size = 13; toc.Format.Font.Color = C(GuideBlue); toc.Format.SpaceBefore = Unit.FromPoint(10);
            for (var i = 0; i < sections.Length; i++)
            {
                var tp = body.AddParagraph(); tp.Format.SpaceBefore = Unit.FromPoint(3); tp.Format.LeftIndent = Unit.FromPoint(6);
                Link(tp, $"{code}-{i}", sections[i].Title, bold: false, color: "#1E293B");
            }
            for (var i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                var h = body.AddParagraph();
                h.AddBookmark($"{code}-{i}");
                h.AddText(s.Title);
                h.Format.Font.Size = 16; h.Format.Font.Bold = true; h.Format.Font.Color = C(GuideBlue);
                h.Format.SpaceBefore = Unit.FromPoint(14); h.Format.SpaceAfter = Unit.FromPoint(2); h.Format.KeepWithNext = true;
                if (i == 0) h.Format.PageBreakBefore = true;
                var sw2 = body.AddParagraph(); sw2.Format.SpaceAfter = Unit.FromPoint(4); sw2.Format.Font.Size = 8.5; sw2.Format.KeepWithNext = true;
                sw2.AddText(otherName == "Hinglish" ? "Read this chapter in: " : "Yeh chapter English mein: "); Link(sw2, $"{other}-{i}", otherName, size: 8.5);
                var intro = body.AddParagraph(s.Intro); intro.Format.SpaceAfter = Unit.FromPoint(6); intro.Format.Font.Color = C("#334155");

                for (var k = 0; k < s.Steps.Length; k++)
                {
                    var p = body.AddParagraph(); p.Format.LeftIndent = Unit.FromPoint(22); p.Format.FirstLineIndent = Unit.FromPoint(-22); p.Format.SpaceAfter = Unit.FromPoint(3);
                    var n = p.AddFormattedText($"{k + 1}.  ", TextFormat.Bold); n.Font.Color = C(GuideBlue);
                    p.AddText(s.Steps[k]);
                }
                foreach (var tip in s.Tips)
                {
                    var tb = body.AddTable(); tb.AddColumn(Unit.FromCentimeter(17));
                    var r = tb.AddRow(); r.Shading.Color = C("#FEF3C7"); r.TopPadding = r.BottomPadding = Unit.FromPoint(4);
                    var tp = r.Cells[0].AddParagraph(); tp.Format.LeftIndent = Unit.FromPoint(6); tp.Format.Font.Size = 9;
                    tp.AddFormattedText(noteWord + "  ", TextFormat.Bold); tp.AddText(tip);
                    body.AddParagraph().Format.SpaceAfter = Unit.FromPoint(2);
                }
                foreach (var shot in s.Shots)
                {
                    var file = shotsDir == null ? "" : Path.Combine(shotsDir, shot + ".png");
                    if (!File.Exists(file)) continue;
                    int w = 0, h2 = 0;
                    try { (w, h2) = PngSize(file); } catch { continue; }
                    var width = Math.Min(w >= 1000 ? 16.0 : w * 0.0185, 16.0);
                    var height = width * h2 / w;
                    if (height > 20) { height = 20; width = height * w / h2; }
                    var cap = body.AddParagraph(); cap.Format.KeepWithNext = true; cap.Format.SpaceBefore = Unit.FromPoint(6);
                    cap.Format.Font.Size = 8; cap.Format.Font.Color = C("#64748B");
                    cap.AddText((code == "en" ? "Screen: " : "Screen: ") + shot.Substring(shot.IndexOf('-') + 1).Replace('-', ' '));
                    var img = body.AddImage(file);
                    img.LockAspectRatio = true; img.Width = Unit.FromCentimeter(width);
                    img.LineFormat.Color = C("#CBD5E1"); img.LineFormat.Width = 0.5;
                    shown++;
                }
            }
        }
        var en = (code: "en", s: GuideContent.Sections);
        var hi = (code: "hi", s: GuideContentHi.Sections);
        if (hinglishFirst)
        {
            Part("hi", hi.s, "en", "English", "USER GUIDE - Hinglish", "Chapters", "Dhyan dijiye:", true);
            Part("en", en.s, "hi", "Hinglish", "USER GUIDE - English", "Contents", "Note:", false);
        }
        else
        {
            Part("en", en.s, "hi", "Hinglish", "USER GUIDE - English", "Contents", "Note:", true);
            Part("hi", hi.s, "en", "English", "USER GUIDE - Hinglish", "Chapters", "Dhyan dijiye:", false);
        }
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
        return shown;
    }

    static (int, int) PngSize(string file)
    {
        using var fs = File.OpenRead(file);
        var b = new byte[24]; fs.ReadExactly(b);
        return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
    }
}
