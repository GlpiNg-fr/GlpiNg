using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using GlpiNg.Modules.Abstractions.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GlpiNg.Web.Components.Pages.Reports;

/// <summary>
/// Génère les exports (CSV/XLSX/ODS/PDF) d'un <see cref="ReportResult"/> pour le menu Exporter de
/// <see cref="Detail"/>. Même famille de formats que l'export de la liste des ordinateurs (voir
/// ComputerExportWriter, module Inventory), mais écrit une fois pour tous les rapports : ce writer
/// ne connaît que des tableaux de texte, jamais un modèle de données de module.
///
/// Un rapport ayant plusieurs tableaux, chaque format le rend à sa manière : un onglet par tableau
/// en XLSX/ODS, des sections successives en CSV et en PDF. Les liens des cellules sont perdus —
/// un classeur ou un PDF se lit hors de l'application, où ils ne mèneraient nulle part.
/// </summary>
internal static class ReportExportWriter
{
    static ReportExportWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] BuildCsv(ReportResult report)
    {
        StringBuilder sb = new();

        foreach (ReportTable table in report.Tables)
        {
            sb.AppendLine(CsvEscape(table.Title));
            sb.AppendLine(string.Join(';', table.Columns.Select(column => CsvEscape(column.Label))));

            foreach (ReportRow row in table.Rows)
            {
                sb.AppendLine(string.Join(';', row.Cells.Select(cell => CsvEscape(cell.Text))));
            }

            sb.AppendLine();
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
    }

    private static string CsvEscape(string value)
    {
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    public static byte[] BuildXlsx(ReportResult report)
    {
        using XLWorkbook workbook = new();
        List<string> usedNames = [];

        foreach (ReportTable table in report.Tables)
        {
            IXLWorksheet sheet = workbook.Worksheets.Add(SheetName(table.Title, usedNames));

            for (int col = 0; col < table.Columns.Count; col++)
            {
                IXLCell cell = sheet.Cell(1, col + 1);
                cell.Value = table.Columns[col].Label;
                cell.Style.Font.Bold = true;
            }

            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                ReportRow row = table.Rows[rowIndex];

                for (int col = 0; col < row.Cells.Count; col++)
                {
                    IXLCell cell = sheet.Cell(rowIndex + 2, col + 1);
                    cell.Value = row.Cells[col].Text;
                    cell.Style.Font.Bold = row.IsTotal;
                }
            }

            sheet.Columns().AdjustToContents();
        }

        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Nom d'onglet acceptable par un tableur : 31 caractères au plus, sans les caractères que le
    /// format interdit, et unique dans le classeur — deux tableaux peuvent porter le même titre
    /// une fois tronqués, et le classeur serait alors refusé à l'enregistrement.
    /// </summary>
    private static string SheetName(string title, List<string> usedNames)
    {
        string cleaned = new(title.Where(character => !"[]:*?/\\".Contains(character)).ToArray());
        cleaned = cleaned.Trim();

        if (cleaned.Length == 0)
        {
            cleaned = "Tableau";
        }

        string candidate = cleaned.Length > 31 ? cleaned[..31] : cleaned;
        int suffix = 2;

        while (usedNames.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            string marker = $" ({suffix++})";
            candidate = (cleaned.Length + marker.Length > 31 ? cleaned[..(31 - marker.Length)] : cleaned) + marker;
        }

        usedNames.Add(candidate);
        return candidate;
    }

    public static byte[] BuildOds(ReportResult report)
    {
        using MemoryStream stream = new();

        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Le "mimetype" doit être la toute première entrée de l'archive et rester non compressé :
            // c'est ce qui permet à un lecteur ODS de reconnaître le format sans lire tout le zip.
            ZipArchiveEntry mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (Stream entryStream = mimetypeEntry.Open())
            using (StreamWriter writer = new(entryStream, new UTF8Encoding(false)))
            {
                writer.Write("application/vnd.oasis.opendocument.spreadsheet");
            }

            ZipArchiveEntry manifestEntry = archive.CreateEntry("META-INF/manifest.xml");
            using (Stream entryStream = manifestEntry.Open())
            using (StreamWriter writer = new(entryStream, new UTF8Encoding(false)))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2">
                      <manifest:file-entry manifest:full-path="/" manifest:version="1.2" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet"/>
                      <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/>
                    </manifest:manifest>
                    """);
            }

            ZipArchiveEntry contentEntry = archive.CreateEntry("content.xml");
            using (Stream entryStream = contentEntry.Open())
            using (StreamWriter writer = new(entryStream, new UTF8Encoding(false)))
            {
                writer.Write(BuildOdsContentXml(report));
            }
        }

        return stream.ToArray();
    }

    private static string BuildOdsContentXml(ReportResult report)
    {
        StringBuilder sb = new();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.2"><office:body><office:spreadsheet>
            """);

        List<string> usedNames = [];

        foreach (ReportTable table in report.Tables)
        {
            sb.Append($"""<table:table table:name="{XmlEscape(SheetName(table.Title, usedNames))}">""");

            AppendOdsRow(sb, [.. table.Columns.Select(column => column.Label)]);

            foreach (ReportRow row in table.Rows)
            {
                AppendOdsRow(sb, [.. row.Cells.Select(cell => cell.Text)]);
            }

            sb.Append("</table:table>");
        }

        sb.Append("</office:spreadsheet></office:body></office:document-content>");
        return sb.ToString();
    }

    private static void AppendOdsRow(StringBuilder sb, string[] values)
    {
        sb.Append("<table:table-row>");
        foreach (string value in values)
        {
            sb.Append("""<table:table-cell office:value-type="string"><text:p>""");
            sb.Append(XmlEscape(value));
            sb.Append("</text:p></table:table-cell>");
        }

        sb.Append("</table:table-row>");
    }

    private static string XmlEscape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    public static byte[] BuildPdf(ReportDefinition definition, ReportResult report, bool landscape)
    {
        Document document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text(definition.Title).FontSize(16).Bold();
                    header.Item().Text(definition.Description).FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().Text($"Généré le {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(10).Column(content =>
                {
                    foreach (ReportTable table in report.Tables)
                    {
                        content.Item().PaddingTop(8).Text(table.Title).FontSize(11).Bold();

                        if (table.Rows.Count == 0)
                        {
                            content.Item().PaddingTop(2).Text(table.EmptyMessage ?? "Aucune donnée.").FontColor(Colors.Grey.Darken1);
                            continue;
                        }

                        content.Item().PaddingTop(4).Table(pdfTable =>
                        {
                            pdfTable.ColumnsDefinition(columns =>
                            {
                                foreach (ReportColumn _ in table.Columns)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            pdfTable.Header(header =>
                            {
                                foreach (ReportColumn column in table.Columns)
                                {
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(column.Label).Bold();
                                }
                            });

                            foreach (ReportRow row in table.Rows)
                            {
                                foreach (ReportCell cell in row.Cells)
                                {
                                    var cellText = pdfTable.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                        .Text(cell.Text);

                                    if (row.IsTotal)
                                    {
                                        cellText.Bold();
                                    }
                                }
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
