using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Data;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace CRMBlazorServerRBS.Controllers
{
    public partial class ExportController : Controller
    {
        public IQueryable ApplyQuery<T>(IQueryable<T> items, IQueryCollection query = null) where T : class
        {
            if (query != null)
            {
                if (query.ContainsKey("$expand"))
                {
                    var propertiesToExpand = query["$expand"].ToString().Split(',');
                    foreach (var p in propertiesToExpand)
                    {
                        items = items.Include(p);
                    }
                }

                var filter = query.ContainsKey("$filter") ? query["$filter"].ToString() : null;
                if (!string.IsNullOrEmpty(filter))
                {
                    items = items.Where(filter);
                }

                if (query.ContainsKey("$orderBy"))
                {
                    items = items.OrderBy(query["$orderBy"].ToString());
                }

                if (query.ContainsKey("$skip"))
                {
                    items = items.Skip(int.Parse(query["$skip"].ToString()));
                }

                if (query.ContainsKey("$top"))
                {
                    items = items.Take(int.Parse(query["$top"].ToString()));
                }

                if (query.ContainsKey("$select"))
                {
                    var s = query["$select"];
                    var q = $"new ({query["$select"].ToString()})";

                    return items.Select(q);
                }
            }

            return items;
        }

        public FileStreamResult ToCSV(IQueryable query, string fileName = null)
        {
            var columns = GetProperties(query.ElementType);

            var sb = new StringBuilder();

            foreach (var item in query)
            {
                var row = new List<string>();

                foreach (var column in columns)
                {
                    row.Add($"{GetValue(item, column.Key)}".Trim());
                }

                sb.AppendLine(string.Join(",", row.ToArray()));
            }


            var result = new FileStreamResult(new MemoryStream(UTF8Encoding.Default.GetBytes($"{string.Join(",", columns.Select(c => c.Key))}{System.Environment.NewLine}{sb.ToString()}")), "text/csv");
            result.FileDownloadName = (!string.IsNullOrEmpty(fileName) ? fileName : "Export") + ".csv";

            return result;
        }

        public FileStreamResult ToExcel(IQueryable query, string fileName = null)
        {
            var columns = GetProperties(query.ElementType).ToList();
            var stream  = new MemoryStream();

            // ── 1. Pre-collect rows into memory so we can measure column widths ──
            // Each cell: (displayText, dataType, styleIndex, rawNumericText)
            var dataRows = new List<List<(string Text, CellValues Type, uint Style, string Raw)>>();

            foreach (var item in query)
            {
                var cells = new List<(string, CellValues, uint, string)>();
                foreach (var col in columns)
                {
                    var value    = GetValue(item, col.Key);
                    var strValue = $"{value}".Trim();

                    var underlying = col.Value.IsGenericType &&
                                     col.Value.GetGenericTypeDefinition() == typeof(Nullable<>)
                                     ? Nullable.GetUnderlyingType(col.Value) : col.Value;
                    var typeCode = Type.GetTypeCode(underlying);

                    if (typeCode == TypeCode.DateTime && !string.IsNullOrWhiteSpace(strValue))
                    {
                        var oaDate = ((DateTime)value).ToOADate()
                                          .ToString(CultureInfo.InvariantCulture);
                        cells.Add((strValue, CellValues.Number, 1U, oaDate)); // style 1 = date
                    }
                    else if (typeCode == TypeCode.Boolean)
                    {
                        cells.Add((strValue, CellValues.Boolean, 3U, null));
                    }
                    else if (IsNumeric(typeCode))
                    {
                        var raw = value != null
                                  ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
                        cells.Add((raw, CellValues.Number, 3U, null));
                    }
                    else
                    {
                        cells.Add((strValue, CellValues.String, 3U, null));
                    }
                }
                dataRows.Add(cells);
            }

            // ── 2. Calculate auto column widths (max content, capped at 55 chars ≈ 400 px) ──
            const double MaxWidth = 55.0;
            const double MinWidth = 8.0;
            const double Padding  = 2.0;

            var colWidths = columns.Select(c => (double)c.Key.Length).ToArray();
            foreach (var row in dataRows)
                for (int c = 0; c < row.Count; c++)
                    colWidths[c] = Math.Max(colWidths[c], row[c].Text.Length);

            for (int c = 0; c < colWidths.Length; c++)
                colWidths[c] = Math.Min(Math.Max(colWidths[c] + Padding, MinWidth), MaxWidth);

            // ── 3. Build the spreadsheet ──────────────────────────────────────
            using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var worksheet     = new Worksheet();

                var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                GenerateWorkbookStylesPartContent(stylesPart);

                var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                sheets.Append(new Sheet
                {
                    Id      = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name    = "Sheet1"
                });
                workbookPart.Workbook.Save();

                // Column widths — must appear before SheetData
                var colsElem = new Columns();
                for (int c = 0; c < colWidths.Length; c++)
                {
                    colsElem.Append(new Column
                    {
                        Min         = (UInt32Value)(uint)(c + 1),
                        Max         = (UInt32Value)(uint)(c + 1),
                        Width       = colWidths[c],
                        CustomWidth = true
                    });
                }
                worksheet.Append(colsElem);

                // Freeze top header row
                var sheetViews = new SheetViews();
                var sheetView  = new SheetView { TabSelected = true, WorkbookViewId = 0U };
                sheetView.Append(new Pane
                {
                    VerticalSplit    = 1,
                    TopLeftCell      = "A2",
                    ActivePane       = PaneValues.BottomLeft,
                    State            = PaneStateValues.Frozen
                });
                sheetViews.Append(sheetView);
                worksheet.InsertAt(sheetViews, 0);

                var sheetData = worksheet.AppendChild(new SheetData());

                // Header row — style 2 = bold white on blue, fixed height
                var headerRow = new Row { RowIndex = 1U, Height = 20D, CustomHeight = true };
                uint colIdx = 1;
                foreach (var col in columns)
                {
                    headerRow.Append(new Cell
                    {
                        CellReference = GetCellRef(colIdx++, 1),
                        CellValue     = new CellValue(col.Key),
                        DataType      = new EnumValue<CellValues>(CellValues.String),
                        StyleIndex    = 2U   // header style
                    });
                }
                sheetData.AppendChild(headerRow);

                // Data rows
                uint rowIdx = 2;
                foreach (var row in dataRows)
                {
                    // CustomHeight=false → Excel auto-sizes height based on WrapText content
                    var dataRow = new Row { RowIndex = rowIdx, CustomHeight = false };
                    colIdx = 1;
                    foreach (var cell in row)
                    {
                        var c = new Cell
                        {
                            CellReference = GetCellRef(colIdx++, rowIdx),
                            DataType      = new EnumValue<CellValues>(cell.Type),
                            StyleIndex    = cell.Style
                        };
                        c.CellValue = new CellValue(cell.Raw ?? cell.Text);
                        dataRow.Append(c);
                    }
                    sheetData.AppendChild(dataRow);
                    rowIdx++;
                }

                worksheetPart.Worksheet = worksheet;
                worksheetPart.Worksheet.Save();
                workbookPart.Workbook.Save();
            }

            if (stream?.Length > 0)
                stream.Seek(0, SeekOrigin.Begin);

            return new FileStreamResult(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                FileDownloadName = (!string.IsNullOrEmpty(fileName) ? fileName : "Export") + ".xlsx"
            };
        }

        // A1-style cell reference helper
        private static string GetCellRef(uint col, uint row)
        {
            var colName = "";
            var c = col;
            while (c > 0)
            {
                c--;
                colName = (char)('A' + c % 26) + colName;
                c /= 26;
            }
            return colName + row;
        }


        public static object GetValue(object target, string name)
        {
            return target.GetType().GetProperty(name).GetValue(target);
        }

        public static IEnumerable<KeyValuePair<string, Type>> GetProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && IsSimpleType(p.PropertyType)).Select(p => new KeyValuePair<string, Type>(p.Name, p.PropertyType));
        }

        public static bool IsSimpleType(Type type)
        {
            var underlyingType = type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(Nullable<>) ?
                Nullable.GetUnderlyingType(type) : type;

            if(underlyingType == typeof(System.Guid) || underlyingType == typeof(System.DateTimeOffset))
                return true;

            var typeCode = Type.GetTypeCode(underlyingType);

            switch (typeCode)
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.String:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsNumeric(TypeCode typeCode)
        {
            switch (typeCode)
            {
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        private static void GenerateWorkbookStylesPartContent(WorkbookStylesPart part)
        {
            // ── Fonts ──────────────────────────────────────────────────────
            // 0: normal data  |  1: bold white (header)
            var fonts = new Fonts { Count = 2U, KnownFonts = true };

            var fontNormal = new Font();
            fontNormal.Append(new FontSize { Val = 11D });
            fontNormal.Append(new Color { Theme = 1U });
            fontNormal.Append(new FontName { Val = "Calibri" });
            fontNormal.Append(new FontFamilyNumbering { Val = 2 });
            fontNormal.Append(new FontScheme { Val = FontSchemeValues.Minor });

            var fontHeader = new Font();
            fontHeader.Append(new Bold());
            fontHeader.Append(new FontSize { Val = 11D });
            fontHeader.Append(new Color { Rgb = "FFFFFFFF" });   // white text
            fontHeader.Append(new FontName { Val = "Calibri" });
            fontHeader.Append(new FontFamilyNumbering { Val = 2 });
            fontHeader.Append(new FontScheme { Val = FontSchemeValues.Minor });

            fonts.Append(fontNormal);
            fonts.Append(fontHeader);

            // ── Fills ──────────────────────────────────────────────────────
            // 0: none (required)  |  1: gray125 (required)  |  2: blue header bg
            var fills = new Fills { Count = 3U };

            var fillNone = new Fill();
            fillNone.Append(new PatternFill { PatternType = PatternValues.None });

            var fillGray = new Fill();
            fillGray.Append(new PatternFill { PatternType = PatternValues.Gray125 });

            var fillBlue = new Fill();
            var patBlue  = new PatternFill { PatternType = PatternValues.Solid };
            patBlue.Append(new ForegroundColor { Rgb = "FF2F75B6" });  // #2F75B6
            patBlue.Append(new BackgroundColor { Indexed = 64U });
            fillBlue.Append(patBlue);

            fills.Append(fillNone);
            fills.Append(fillGray);
            fills.Append(fillBlue);

            // ── Borders ────────────────────────────────────────────────────
            // 0: none  |  1: thin all-around
            var borders = new Borders { Count = 2U };

            var borderNone = new Border();
            borderNone.Append(new LeftBorder());
            borderNone.Append(new RightBorder());
            borderNone.Append(new TopBorder());
            borderNone.Append(new BottomBorder());
            borderNone.Append(new DiagonalBorder());

            var grayColor = new Color { Rgb = "FF606060" }; // dark gray #606060

            var borderThin = new Border();
            var lb = new LeftBorder   { Style = BorderStyleValues.Thin }; lb.Append(grayColor.CloneNode(true));
            var rb = new RightBorder  { Style = BorderStyleValues.Thin }; rb.Append(grayColor.CloneNode(true));
            var tb = new TopBorder    { Style = BorderStyleValues.Thin }; tb.Append(grayColor.CloneNode(true));
            var bb = new BottomBorder { Style = BorderStyleValues.Thin }; bb.Append(grayColor.CloneNode(true));
            borderThin.Append(lb);
            borderThin.Append(rb);
            borderThin.Append(tb);
            borderThin.Append(bb);
            borderThin.Append(new DiagonalBorder());

            borders.Append(borderNone);
            borders.Append(borderThin);

            // ── Cell style formats (base formats) ──────────────────────────
            var cellStyleFormats = new CellStyleFormats { Count = 1U };
            cellStyleFormats.Append(new CellFormat
            {
                NumberFormatId = 0U, FontId = 0U, FillId = 0U, BorderId = 0U
            });

            // ── Cell formats ───────────────────────────────────────────────
            // Index 0: default data     (font 0, fill 0, border 0)
            // Index 1: date             (font 0, fill 0, border 1, numfmt 14)
            // Index 2: header           (font 1, fill 2, border 1, centered)
            // Index 3: normal + border  (font 0, fill 0, border 1)
            var cellFormats = new CellFormats { Count = 4U };

            cellFormats.Append(new CellFormat   // 0 — default
            {
                NumberFormatId = 0U, FontId = 0U, FillId = 0U,
                BorderId = 0U, FormatId = 0U
            });

            var dateFmt = new CellFormat        // 1 — date + wrap
            {
                NumberFormatId = 14U, FontId = 0U, FillId = 0U,
                BorderId = 1U, FormatId = 0U, ApplyNumberFormat = true,
                ApplyBorder = true, ApplyAlignment = true
            };
            dateFmt.Append(new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top });
            cellFormats.Append(dateFmt);

            var headerFmt = new CellFormat      // 2 — header
            {
                NumberFormatId = 0U, FontId = 1U, FillId = 2U,
                BorderId = 1U, FormatId = 0U,
                ApplyFont = true, ApplyFill = true,
                ApplyBorder = true, ApplyAlignment = true
            };
            headerFmt.Append(new Alignment
            {
                Horizontal = HorizontalAlignmentValues.Center,
                Vertical   = VerticalAlignmentValues.Center,
                WrapText   = false
            });
            cellFormats.Append(headerFmt);

            var dataFmt = new CellFormat         // 3 — data + border + wrap
            {
                NumberFormatId = 0U, FontId = 0U, FillId = 0U,
                BorderId = 1U, FormatId = 0U,
                ApplyBorder = true, ApplyAlignment = true
            };
            dataFmt.Append(new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top });
            cellFormats.Append(dataFmt);

            // ── Cell styles ────────────────────────────────────────────────
            var cellStyles = new CellStyles { Count = 1U };
            cellStyles.Append(new CellStyle
            {
                Name = "Normal", FormatId = 0U, BuiltinId = 0U
            });

            // ── Assemble stylesheet ────────────────────────────────────────
            var stylesheet = new Stylesheet();
            stylesheet.Append(fonts);
            stylesheet.Append(fills);
            stylesheet.Append(borders);
            stylesheet.Append(cellStyleFormats);
            stylesheet.Append(cellFormats);
            stylesheet.Append(cellStyles);
            stylesheet.Append(new DifferentialFormats { Count = 0U });
            stylesheet.Append(new TableStyles
            {
                Count = 0U,
                DefaultTableStyle  = "TableStyleMedium2",
                DefaultPivotStyle  = "PivotStyleLight16"
            });

            part.Stylesheet = stylesheet;
        }
    }
}
