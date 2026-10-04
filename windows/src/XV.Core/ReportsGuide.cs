using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.Rendering;

namespace XV.Core;

public static partial class Reports
{
    const string GuideBlue = "#4F46E5";

    /// <summary>Writes the full user guide as a PDF. Screenshots are taken from <paramref name="shotsDir"/> when the files exist (sample data only).</summary>
    public static int GuidePdf(string path, string? shotsDir, string version, string serverName = "")
    {
        EnsureFonts();
        var doc = new Document();
        doc.Info.Title = GuideContent.Product + " - User Guide";
        doc.Info.Author = GuideContent.Maker;
        doc.Styles[StyleNames.Normal]!.Font.Name = "XV Sans";
        doc.Styles[StyleNames.Normal]!.Font.Size = 10;

        var cover = doc.AddSection();
        cover.PageSetup.PageFormat = PageFormat.A4;
        cover.PageSetup.LeftMargin = cover.PageSetup.RightMargin = Unit.FromCentimeter(2);
        cover.PageSetup.TopMargin = Unit.FromCentimeter(2); cover.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        var band = cover.AddTable(); band.AddColumn(Unit.FromCentimeter(17));
        var br = band.AddRow(); br.Shading.Color = C(GuideBlue); br.TopPadding = br.BottomPadding = Unit.FromPoint(40);
        var bp = br.Cells[0].AddParagraph(); bp.Format.LeftIndent = Unit.FromPoint(16);
        var t1 = bp.AddFormattedText("USER GUIDE", TextFormat.Bold); t1.Font.Size = 34; t1.Font.Color = Colors.White;
        bp.AddLineBreak();
        var t2 = bp.AddFormattedText(GuideContent.Product + " - Command Center and Gatekeeper phone app"); t2.Font.Size = 13; t2.Font.Color = C("#E0E7FF");
        var meta = cover.AddParagraph($"Version {version}   •   Created by {GuideContent.Maker}");
        meta.Format.SpaceBefore = Unit.FromPoint(14); meta.Format.Font.Size = 11; meta.Format.Font.Color = C("#475569");
        var lead = cover.AddParagraph("How to operate every function of the server and the gate phones, step by step. The pictures show sample data only.");
        lead.Format.SpaceBefore = Unit.FromPoint(10); lead.Format.Font.Size = 11;

        var toc = cover.AddParagraph("Contents"); toc.Format.Font.Bold = true; toc.Format.Font.Size = 14; toc.Format.Font.Color = C(GuideBlue); toc.Format.SpaceBefore = Unit.FromPoint(24);
        foreach (var s in GuideContent.Sections)
        {
            var p = cover.AddParagraph(s.Title); p.Format.SpaceBefore = Unit.FromPoint(3); p.Format.LeftIndent = Unit.FromPoint(6);
        }

        var body = doc.AddSection();
        body.PageSetup.PageFormat = PageFormat.A4;
        body.PageSetup.LeftMargin = body.PageSetup.RightMargin = Unit.FromCentimeter(2);
        body.PageSetup.TopMargin = Unit.FromCentimeter(1.8); body.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        var footer = body.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7.5; footer.Format.Font.Color = C("#71717A");
        footer.AddText($"{GuideContent.Product} • User Guide • {GuideContent.Maker} • Page "); footer.AddPageField(); footer.AddText(" of "); footer.AddNumPagesField();

        var shown = 0;
        foreach (var s in GuideContent.Sections)
        {
            var h = body.AddParagraph(s.Title);
            h.Format.Font.Size = 16; h.Format.Font.Bold = true; h.Format.Font.Color = C(GuideBlue);
            h.Format.SpaceBefore = Unit.FromPoint(14); h.Format.SpaceAfter = Unit.FromPoint(4); h.Format.KeepWithNext = true;
            var intro = body.AddParagraph(s.Intro); intro.Format.SpaceAfter = Unit.FromPoint(6); intro.Format.Font.Color = C("#334155");

            for (var i = 0; i < s.Steps.Length; i++)
            {
                var p = body.AddParagraph(); p.Format.LeftIndent = Unit.FromPoint(22); p.Format.FirstLineIndent = Unit.FromPoint(-22); p.Format.SpaceAfter = Unit.FromPoint(3);
                var n = p.AddFormattedText($"{i + 1}.  ", TextFormat.Bold); n.Font.Color = C(GuideBlue);
                p.AddText(s.Steps[i]);
            }
            foreach (var tip in s.Tips)
            {
                var tb = body.AddTable(); tb.AddColumn(Unit.FromCentimeter(17));
                var r = tb.AddRow(); r.Shading.Color = C("#FEF3C7"); r.TopPadding = r.BottomPadding = Unit.FromPoint(4);
                var tp = r.Cells[0].AddParagraph(); tp.Format.LeftIndent = Unit.FromPoint(6); tp.Format.Font.Size = 9;
                tp.AddFormattedText("Note:  ", TextFormat.Bold); tp.AddText(tip);
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
                cap.AddText("Screen: " + shot.Substring(shot.IndexOf('-') + 1).Replace('-', ' '));
                var img = body.AddImage(file);
                img.LockAspectRatio = true; img.Width = Unit.FromCentimeter(width);
                img.LineFormat.Color = C("#CBD5E1"); img.LineFormat.Width = 0.5;
                shown++;
            }
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
