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
                    return items.Select($"new ({query["$select"].ToString()})");
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
            var stream = new MemoryStream();

            using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet();

                var workbookStylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                GenerateWorkbookStylesPartContent(workbookStylesPart);

                var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                var sheet = new Sheet() { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Sheet1" };
                sheets.Append(sheet);
                workbookPart.Workbook.Save();

                // Materialize data and calculate column widths in one pass
                var dataRows = new List<object>();
                var colWidths = columns.Select(c => (double)c.Key.Length).ToArray();
                foreach (var item in query)
                {
                    dataRows.Add(item);
                    for (int i = 0; i < columns.Count; i++)
                    {
                        var val = GetValue(item, columns[i].Key);
                        var len = val != null ? $"{val}".Trim().Length : 0;
                        if (len > colWidths[i]) colWidths[i] = len;
                    }
                }

                // Freeze top row
                var sheetViews = worksheetPart.Worksheet.AppendChild(new SheetViews());
                var sheetView = sheetViews.AppendChild(new SheetView() { TabSelected = true, WorkbookViewId = (UInt32Value)0U });
                sheetView.AppendChild(new Pane() { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen });
                sheetView.AppendChild(new Selection() { Pane = PaneValues.BottomLeft });

                // Auto column widths (capped at 60)
                var columnsElement = worksheetPart.Worksheet.AppendChild(new Columns());
                for (int i = 0; i < columns.Count; i++)
                {
                    var width = Math.Min(colWidths[i] * 1.2 + 3, 80);
                    columnsElement.AppendChild(new Column()
                    {
                        Min = (UInt32Value)((uint)(i + 1)),
                        Max = (UInt32Value)((uint)(i + 1)),
                        Width = width,
                        CustomWidth = true
                    });
                }

                var sheetData = worksheetPart.Worksheet.AppendChild(new SheetData());

                // Header row — style index 2 (bold white on blue)
                var headerRow = new Row();
                foreach (var column in columns)
                {
                    headerRow.Append(new Cell()
                    {
                        CellValue = new CellValue(column.Key),
                        DataType = new EnumValue<CellValues>(CellValues.String),
                        StyleIndex = (UInt32Value)2U
                    });
                }
                sheetData.AppendChild(headerRow);

                // Data rows — style index 3 (border), 4 (date + border)
                foreach (var item in dataRows)
                {
                    var row = new Row();

                    foreach (var column in columns)
                    {
                        var value = GetValue(item, column.Key);
                        var stringValue = $"{value}".Trim();

                        var cell = new Cell();

                        var underlyingType = column.Value.IsGenericType &&
                            column.Value.GetGenericTypeDefinition() == typeof(Nullable<>) ?
                            Nullable.GetUnderlyingType(column.Value) : column.Value;

                        var typeCode = Type.GetTypeCode(underlyingType);

                        if (typeCode == TypeCode.DateTime)
                        {
                            if (!string.IsNullOrWhiteSpace(stringValue))
                            {
                                cell.CellValue = new CellValue() { Text = ((DateTime)value).ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture) };
                                cell.DataType = new EnumValue<CellValues>(CellValues.Number);
                                cell.StyleIndex = (UInt32Value)4U;
                            }
                            else
                            {
                                cell.StyleIndex = (UInt32Value)3U;
                            }
                        }
                        else if (typeCode == TypeCode.Boolean)
                        {
                            cell.CellValue = new CellValue(stringValue.ToLowerInvariant());
                            cell.DataType = new EnumValue<CellValues>(CellValues.Boolean);
                            cell.StyleIndex = (UInt32Value)3U;
                        }
                        else if (IsNumeric(typeCode))
                        {
                            if (value != null)
                            {
                                stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                            }
                            cell.CellValue = new CellValue(stringValue);
                            cell.DataType = new EnumValue<CellValues>(CellValues.Number);
                            cell.StyleIndex = (UInt32Value)3U;
                        }
                        else
                        {
                            cell.CellValue = new CellValue(stringValue);
                            cell.DataType = new EnumValue<CellValues>(CellValues.String);
                            cell.StyleIndex = (UInt32Value)3U;
                        }

                        row.Append(cell);
                    }

                    sheetData.AppendChild(row);
                }

                workbookPart.Workbook.Save();
            }

            if (stream?.Length > 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            var result = new FileStreamResult(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            result.FileDownloadName = (!string.IsNullOrEmpty(fileName) ? fileName : "Export") + ".xlsx";

            return result;
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

        private static void GenerateWorkbookStylesPartContent(WorkbookStylesPart workbookStylesPart1)
        {
            var stylesheet = new Stylesheet();

            // Font 0: Normal
            var font0 = new Font();
            font0.Append(new FontSize() { Val = 11D });
            font0.Append(new Color() { Theme = (UInt32Value)1U });
            font0.Append(new FontName() { Val = "Calibri" });
            font0.Append(new FontFamilyNumbering() { Val = 2 });
            font0.Append(new FontScheme() { Val = FontSchemeValues.Minor });

            // Font 1: Bold + White (header)
            var font1 = new Font();
            font1.Append(new Bold());
            font1.Append(new FontSize() { Val = 11D });
            font1.Append(new Color() { Rgb = "FFFFFFFF" });
            font1.Append(new FontName() { Val = "Calibri" });
            font1.Append(new FontFamilyNumbering() { Val = 2 });
            font1.Append(new FontScheme() { Val = FontSchemeValues.Minor });

            var fonts = new Fonts() { Count = (UInt32Value)2U, KnownFonts = true };
            fonts.Append(font0);
            fonts.Append(font1);

            // Fill 0: None (required), Fill 1: Gray125 (required), Fill 2: Blue header
            var fill0 = new Fill();
            fill0.Append(new PatternFill() { PatternType = PatternValues.None });

            var fill1 = new Fill();
            fill1.Append(new PatternFill() { PatternType = PatternValues.Gray125 });

            var fill2 = new Fill();
            var headerFill = new PatternFill() { PatternType = PatternValues.Solid };
            headerFill.Append(new ForegroundColor() { Rgb = "FF2E75B6" });
            headerFill.Append(new BackgroundColor() { Indexed = (UInt32Value)64U });
            fill2.Append(headerFill);

            var fills = new Fills() { Count = (UInt32Value)3U };
            fills.Append(fill0);
            fills.Append(fill1);
            fills.Append(fill2);

            // Border 0: None, Border 1: Thin all sides
            var border0 = new Border();
            border0.Append(new LeftBorder());
            border0.Append(new RightBorder());
            border0.Append(new TopBorder());
            border0.Append(new BottomBorder());
            border0.Append(new DiagonalBorder());

            var border1 = new Border();
            border1.Append(new LeftBorder() { Style = BorderStyleValues.Thin });
            border1.Append(new RightBorder() { Style = BorderStyleValues.Thin });
            border1.Append(new TopBorder() { Style = BorderStyleValues.Thin });
            border1.Append(new BottomBorder() { Style = BorderStyleValues.Thin });
            border1.Append(new DiagonalBorder());

            var borders = new Borders() { Count = (UInt32Value)2U };
            borders.Append(border0);
            borders.Append(border1);

            var cellStyleFormats = new CellStyleFormats() { Count = (UInt32Value)1U };
            cellStyleFormats.Append(new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U });

            // CellFormat indices:
            // 0 = default
            // 1 = date, no border (backward compat)
            // 2 = header: bold white font, blue fill, border
            // 3 = data cell: border
            // 4 = date data cell: date format + border
            var cellFormats = new CellFormats() { Count = (UInt32Value)5U };
            cellFormats.Append(new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U });
            cellFormats.Append(new CellFormat() { NumberFormatId = (UInt32Value)14U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyNumberFormat = true });

            var headerFormat = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)1U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)1U, FormatId = (UInt32Value)0U, ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true };
            headerFormat.Append(new Alignment() { WrapText = true });
            cellFormats.Append(headerFormat);

            var dataFormat = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)1U, FormatId = (UInt32Value)0U, ApplyBorder = true, ApplyAlignment = true };
            dataFormat.Append(new Alignment() { WrapText = true });
            cellFormats.Append(dataFormat);

            var dateFormat = new CellFormat() { NumberFormatId = (UInt32Value)14U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)1U, FormatId = (UInt32Value)0U, ApplyNumberFormat = true, ApplyBorder = true, ApplyAlignment = true };
            dateFormat.Append(new Alignment() { WrapText = true });
            cellFormats.Append(dateFormat);

            var cellStyles = new CellStyles() { Count = (UInt32Value)1U };
            cellStyles.Append(new CellStyle() { Name = "Normal", FormatId = (UInt32Value)0U, BuiltinId = (UInt32Value)0U });

            stylesheet.Append(fonts);
            stylesheet.Append(fills);
            stylesheet.Append(borders);
            stylesheet.Append(cellStyleFormats);
            stylesheet.Append(cellFormats);
            stylesheet.Append(cellStyles);
            stylesheet.Append(new DifferentialFormats() { Count = (UInt32Value)0U });
            stylesheet.Append(new TableStyles() { Count = (UInt32Value)0U, DefaultTableStyle = "TableStyleMedium2", DefaultPivotStyle = "PivotStyleLight16" });

            workbookStylesPart1.Stylesheet = stylesheet;
        }
    }
}
