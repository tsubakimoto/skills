using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;
using System.Drawing;

namespace PdfScripts;

internal static class PdfFormUtilities
{
    public static bool HasFillableFields(string pdfPath)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        return document.AcroForm is { Fields.Count: > 0 };
    }

    public static List<FormFieldInfo> ExtractFieldInfo(string pdfPath, Action<string>? log = null)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        return ExtractFieldInfo(document, log);
    }

    public static void WriteFieldInfo(string pdfPath, string jsonOutputPath)
    {
        var fieldInfo = ExtractFieldInfo(pdfPath, Console.WriteLine);
        JsonUtilities.SaveToFile(jsonOutputPath, fieldInfo);
        Console.WriteLine($"Wrote {fieldInfo.Count} fields to {jsonOutputPath}");
    }

    public static void FillFields(string inputPdfPath, string fieldValuesPath, string outputPdfPath)
    {
        var requestedFields = JsonUtilities.LoadFromFile<List<FieldValueInput>>(fieldValuesPath);

        using var document = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Modify);
        var fieldInfo = ExtractFieldInfo(document, Console.WriteLine);
        var fieldsById = fieldInfo.ToDictionary(x => x.FieldId, StringComparer.Ordinal);
        var fieldLookup = BuildFieldLookup(document.AcroForm);

        var hasError = false;

        foreach (var field in requestedFields)
        {
            if (!fieldsById.TryGetValue(field.FieldId, out var existingField))
            {
                hasError = true;
                Console.WriteLine($"ERROR: `{field.FieldId}` is not a valid field ID");
                continue;
            }

            if (field.Page != existingField.Page)
            {
                hasError = true;
                Console.WriteLine($"ERROR: Incorrect page number for `{field.FieldId}` (got {field.Page}, expected {existingField.Page})");
            }

            if (field.Value is not null)
            {
                var error = ValidationErrorForFieldValue(existingField, field.Value);
                if (error is not null)
                {
                    hasError = true;
                    Console.WriteLine(error);
                }
            }
        }

        if (hasError)
        {
            Environment.Exit(1);
        }

        if (document.AcroForm is null)
        {
            throw new InvalidOperationException("The PDF does not contain fillable form fields.");
        }

        foreach (var field in requestedFields.Where(x => x.Value is not null))
        {
            if (!fieldLookup.TryGetValue(field.FieldId, out var existing))
            {
                continue;
            }

            SetFieldValue(existing.Field, field.Value!, fieldsById[field.FieldId]);
        }

        document.AcroForm.Elements.SetBoolean("/NeedAppearances", true);
        document.Save(outputPdfPath);
    }

    public static void FillPdfWithOverlayText(string inputPdfPath, string fieldsJsonPath, string outputPdfPath)
    {
        var fieldsData = JsonUtilities.LoadFromFile<FieldsDocument>(fieldsJsonPath);

        using var document = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Modify);
        var textEntryCount = 0;

        foreach (var field in fieldsData.FormFields)
        {
            if (field.EntryText?.Text is not { Length: > 0 } text)
            {
                continue;
            }

            var pageInfo = fieldsData.Pages.FirstOrDefault(x => x.PageNumber == field.PageNumber)
                ?? throw new InvalidDataException($"No page metadata found for page {field.PageNumber}.");

            var page = document.Pages[field.PageNumber - 1];
            var rect = ToPdfSharpRect(field.EntryBoundingBox, pageInfo, page);
            if (rect is null)
            {
                continue;
            }

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var font = CreateFont(field.EntryText);
            var brush = new XSolidBrush(ParseColor(field.EntryText.FontColor));
            gfx.DrawString(text, font, brush, rect.Value, XStringFormats.TopLeft);
            textEntryCount++;
        }

        document.Save(outputPdfPath);
        Console.WriteLine($"Successfully filled PDF form and saved to {outputPdfPath}");
        Console.WriteLine($"Added {textEntryCount} text annotations");
    }

    public static FormStructureDocument ExtractFormStructure(string pdfPath)
    {
        var structure = new FormStructureDocument();

        using var pdfPigDocument = PdfPigDocument.Open(pdfPath);
        using var docReader = Docnet.Core.DocLib.Instance.GetDocReader(pdfPath, new Docnet.Core.Models.PageDimensions(2.0));

        foreach (var page in pdfPigDocument.GetPages())
        {
            structure.Pages.Add(new StructurePageInfo
            {
                PageNumber = page.Number,
                Width = page.Width,
                Height = page.Height
            });

            foreach (var word in page.GetWords())
            {
                var box = word.BoundingBox;
                structure.Labels.Add(new StructureLabel
                {
                    Page = page.Number,
                    Text = word.Text,
                    X0 = Round1(box.Left),
                    Top = Round1(page.Height - box.Top),
                    X1 = Round1(box.Right),
                    Bottom = Round1(page.Height - box.Bottom)
                });
            }

            using var pageReader = docReader.GetPageReader(page.Number - 1);
            using var image = LoadDocnetPageImage(pageReader);

            structure.Lines.AddRange(PdfRenderingUtilities.DetectHorizontalLines(image, page.Number, page.Width, page.Height));
            structure.Checkboxes.AddRange(PdfRenderingUtilities.DetectCheckboxes(image, page.Number, page.Width, page.Height));
        }

        foreach (var group in structure.Lines.GroupBy(x => x.Page))
        {
            var yCoords = group
                .Select(x => x.Y)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            for (var i = 0; i < yCoords.Count - 1; i++)
            {
                structure.RowBoundaries.Add(new RowBoundary
                {
                    Page = group.Key,
                    RowTop = yCoords[i],
                    RowBottom = yCoords[i + 1],
                    RowHeight = Round1(yCoords[i + 1] - yCoords[i])
                });
            }
        }

        return structure;
    }

    public static string? ValidationErrorForFieldValue(FormFieldInfo fieldInfo, string fieldValue)
    {
        if (fieldInfo.Type == "checkbox")
        {
            if (fieldValue != fieldInfo.CheckedValue && fieldValue != fieldInfo.UncheckedValue)
            {
                return $"ERROR: Invalid value \"{fieldValue}\" for checkbox field \"{fieldInfo.FieldId}\". The checked value is \"{fieldInfo.CheckedValue}\" and the unchecked value is \"{fieldInfo.UncheckedValue}\"";
            }
        }
        else if (fieldInfo.Type == "radio_group")
        {
            var optionValues = fieldInfo.RadioOptions?.Select(x => x.Value).ToList() ?? [];
            if (!optionValues.Contains(fieldValue, StringComparer.Ordinal))
            {
                return $"ERROR: Invalid value \"{fieldValue}\" for radio group field \"{fieldInfo.FieldId}\". Valid values are: [{string.Join(", ", optionValues)}]";
            }
        }
        else if (fieldInfo.Type == "choice")
        {
            var optionValues = fieldInfo.ChoiceOptions?.Select(x => x.Value).ToList() ?? [];
            if (!optionValues.Contains(fieldValue, StringComparer.Ordinal))
            {
                return $"ERROR: Invalid value \"{fieldValue}\" for choice field \"{fieldInfo.FieldId}\". Valid values are: [{string.Join(", ", optionValues)}]";
            }
        }

        return null;
    }

    private static List<FormFieldInfo> ExtractFieldInfo(PdfSharp.Pdf.PdfDocument document, Action<string>? log)
    {
        if (document.AcroForm is null)
        {
            return [];
        }

        var fieldInfoById = new Dictionary<string, FormFieldInfo>(StringComparer.Ordinal);
        var possibleRadioNames = new HashSet<string>(StringComparer.Ordinal);
        var radioFieldsById = new Dictionary<string, FormFieldInfo>(StringComparer.Ordinal);

        foreach (PdfAcroField field in document.AcroForm.Fields)
        {
            CollectLeafFields(field, fieldInfoById, possibleRadioNames);
        }

        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            var annotations = page.Elements.GetArray("/Annots");
            if (annotations is null)
            {
                continue;
            }

            for (var annotationIndex = 0; annotationIndex < annotations.Elements.Count; annotationIndex++)
            {
                var annotation = ResolveDictionary(annotations.Elements[annotationIndex]);
                if (annotation is null)
                {
                    continue;
                }

                var fieldId = GetFullAnnotationFieldId(annotation);
                if (string.IsNullOrEmpty(fieldId))
                {
                    continue;
                }

                var rect = annotation.Elements.GetRectangle("/Rect");
                var rectValues = rect.IsZero ? null : ToRectArray(rect);

                if (rectValues is not null && fieldInfoById.TryGetValue(fieldId, out var existingField))
                {
                    existingField.Page = pageIndex + 1;
                    existingField.Rect = rectValues;
                }
                else if (possibleRadioNames.Contains(fieldId))
                {
                    var onValues = GetAnnotationOnValues(annotation);
                    if (onValues.Count != 1 || rectValues is null)
                    {
                        continue;
                    }

                    if (!radioFieldsById.TryGetValue(fieldId, out var radioGroup))
                    {
                        radioGroup = new FormFieldInfo
                        {
                            FieldId = fieldId,
                            Type = "radio_group",
                            Page = pageIndex + 1,
                            RadioOptions = []
                        };
                        radioFieldsById[fieldId] = radioGroup;
                    }

                    radioGroup.RadioOptions ??= [];
                    radioGroup.RadioOptions.Add(new RadioOption
                    {
                        Value = onValues[0],
                        Rect = rectValues
                    });
                }
            }
        }

        var fieldsWithLocation = new List<FormFieldInfo>();
        foreach (var fieldInfo in fieldInfoById.Values)
        {
            if (fieldInfo.Page > 0)
            {
                fieldsWithLocation.Add(fieldInfo);
            }
            else
            {
                log?.Invoke($"Unable to determine location for field id: {fieldInfo.FieldId}, ignoring");
            }
        }

        var sortedFields = fieldsWithLocation
            .Concat(radioFieldsById.Values)
            .OrderBy(x => x.Page)
            .ThenByDescending(x => GetSortRect(x)?[1] ?? 0)
            .ThenBy(x => GetSortRect(x)?[0] ?? 0)
            .ToList();

        return sortedFields;
    }

    private static void CollectLeafFields(PdfAcroField field, IDictionary<string, FormFieldInfo> fieldInfoById, ISet<string> possibleRadioNames)
    {
        var fullFieldId = GetFullAnnotationFieldId(field);
        if (string.IsNullOrEmpty(fullFieldId))
        {
            return;
        }

        var hasKids = field.Fields.Count > 0;
        var fieldType = GetInheritedName(field, "/FT");

        if (hasKids && string.Equals(fieldType, "/Btn", StringComparison.Ordinal))
        {
            possibleRadioNames.Add(fullFieldId);
            return;
        }

        if (hasKids)
        {
            foreach (PdfAcroField child in field.Fields)
            {
                CollectLeafFields(child, fieldInfoById, possibleRadioNames);
            }

            return;
        }

        fieldInfoById[fullFieldId] = MakeFieldInfo(field, fullFieldId);
    }

    private static FormFieldInfo MakeFieldInfo(PdfAcroField field, string fieldId)
    {
        var typeName = GetInheritedName(field, "/FT");
        var fieldInfo = new FormFieldInfo
        {
            FieldId = fieldId
        };

        switch (typeName)
        {
            case "/Tx":
                fieldInfo.Type = "text";
                break;
            case "/Btn":
                fieldInfo.Type = "checkbox";
                if (field is PdfCheckBoxField checkbox)
                {
                    fieldInfo.CheckedValue = NormalizePdfName(checkbox.CheckedName);
                    fieldInfo.UncheckedValue = NormalizePdfName(checkbox.UncheckedName);
                }
                else
                {
                    var states = field.GetAppearanceNames().Select(NormalizePdfName).ToList();
                    if (states.Count == 2)
                    {
                        if (states.Contains("/Off", StringComparer.Ordinal))
                        {
                            fieldInfo.CheckedValue = states.First(x => x != "/Off");
                            fieldInfo.UncheckedValue = "/Off";
                        }
                        else
                        {
                            Console.WriteLine($"Unexpected state values for checkbox `${fieldId}`. Its checked and unchecked values may not be correct; if you're trying to check it, visually verify the results.");
                            fieldInfo.CheckedValue = states[0];
                            fieldInfo.UncheckedValue = states[1];
                        }
                    }
                }
                break;
            case "/Ch":
                fieldInfo.Type = "choice";
                fieldInfo.ChoiceOptions = GetChoiceOptions(field);
                break;
            default:
                fieldInfo.Type = $"unknown ({typeName})";
                break;
        }

        return fieldInfo;
    }

    private static List<ChoiceOption> GetChoiceOptions(PdfAcroField field)
    {
        var options = new List<ChoiceOption>();
        var optValue = GetInheritedValue(field, "/Opt");
        var optArray = ResolveArray(optValue);
        if (optArray is null)
        {
            return options;
        }

        for (var i = 0; i < optArray.Elements.Count; i++)
        {
            var item = optArray.Elements[i];
            var arrayItem = ResolveArray(item);
            if (arrayItem is not null && arrayItem.Elements.Count >= 2)
            {
                options.Add(new ChoiceOption
                {
                    Value = GetStringOrName(arrayItem.Elements[0]) ?? string.Empty,
                    Text = GetStringOrName(arrayItem.Elements[1]) ?? string.Empty
                });
            }
            else
            {
                var value = GetStringOrName(item) ?? string.Empty;
                options.Add(new ChoiceOption
                {
                    Value = value,
                    Text = value
                });
            }
        }

        return options;
    }

    private static Dictionary<string, ResolvedField> BuildFieldLookup(PdfAcroForm? form)
    {
        var lookup = new Dictionary<string, ResolvedField>(StringComparer.Ordinal);
        if (form is null)
        {
            return lookup;
        }

        foreach (PdfAcroField field in form.Fields)
        {
            AddFields(field, lookup);
        }

        return lookup;
    }

    private static void AddFields(PdfAcroField field, IDictionary<string, ResolvedField> lookup)
    {
        var fullFieldId = GetFullAnnotationFieldId(field);
        if (string.IsNullOrEmpty(fullFieldId))
        {
            return;
        }

        var fieldType = GetInheritedName(field, "/FT");
        if (field.Fields.Count > 0 && string.Equals(fieldType, "/Btn", StringComparison.Ordinal))
        {
            lookup[fullFieldId] = new ResolvedField(field);
            return;
        }

        if (field.Fields.Count > 0)
        {
            foreach (PdfAcroField child in field.Fields)
            {
                AddFields(child, lookup);
            }

            return;
        }

        lookup[fullFieldId] = new ResolvedField(field);
    }

    private static void SetFieldValue(PdfAcroField field, string value, FormFieldInfo fieldInfo)
    {
        switch (fieldInfo.Type)
        {
            case "text":
                if (field is PdfTextField textField)
                {
                    textField.Text = value;
                }
                else
                {
                    field.Elements.SetString("/V", value);
                }
                break;
            case "checkbox":
                if (field is PdfCheckBoxField checkBox)
                {
                    checkBox.Checked = string.Equals(value, fieldInfo.CheckedValue, StringComparison.Ordinal);
                }

                SetButtonAppearance(field, value, fieldInfo.UncheckedValue ?? "/Off");
                break;
            case "choice":
                field.Elements.SetString("/V", value);
                field.Elements.SetString("/DV", value);
                break;
            case "radio_group":
                field.Elements.SetName("/V", value);
                SetButtonAppearance(field, value, "/Off");
                break;
            default:
                field.Elements.SetString("/V", value);
                break;
        }
    }

    private static void SetButtonAppearance(PdfAcroField field, string selectedValue, string offValue)
    {
        if (field.Fields.Count == 0)
        {
            field.Elements.SetName("/V", selectedValue);
            field.Elements.SetName("/AS", selectedValue);
            return;
        }

        foreach (PdfAcroField child in field.Fields)
        {
            var onValues = GetAnnotationOnValues(child);
            if (onValues.Contains(selectedValue, StringComparer.Ordinal))
            {
                child.Elements.SetName("/AS", selectedValue);
            }
            else
            {
                child.Elements.SetName("/AS", offValue);
            }
        }
    }

    private static XRect? ToPdfSharpRect(double[] rect, PageInfo pageInfo, PdfPage page)
    {
        if (rect.Length != 4)
        {
            return null;
        }

        if (pageInfo.PdfWidth.HasValue && pageInfo.PdfHeight.HasValue)
        {
            return new XRect(
                rect[0],
                rect[1],
                Math.Max(0, rect[2] - rect[0]),
                Math.Max(0, rect[3] - rect[1]));
        }

        if (!pageInfo.ImageWidth.HasValue || !pageInfo.ImageHeight.HasValue)
        {
            return null;
        }

        var xScale = page.Width.Point / pageInfo.ImageWidth.Value;
        var yScale = page.Height.Point / pageInfo.ImageHeight.Value;
        return new XRect(
            rect[0] * xScale,
            rect[1] * yScale,
            Math.Max(0, (rect[2] - rect[0]) * xScale),
            Math.Max(0, (rect[3] - rect[1]) * yScale));
    }

    private static XFont CreateFont(EntryTextDefinition entryText)
    {
        var fontName = string.IsNullOrWhiteSpace(entryText.Font) ? "Arial" : entryText.Font;
        var fontSize = entryText.FontSize ?? 14;

        try
        {
            return new XFont(fontName, fontSize, XFontStyleEx.Regular);
        }
        catch (InvalidOperationException)
        {
            return new XFont("Arial", fontSize, XFontStyleEx.Regular);
        }
    }

    private static XColor ParseColor(string? hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return XColors.Black;
        }

        var normalized = hexColor.Trim().TrimStart('#');
        if (normalized.Length == 6 &&
            int.TryParse(normalized[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            int.TryParse(normalized[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            int.TryParse(normalized[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return XColor.FromArgb(r, g, b);
        }

        return XColors.Black;
    }

    private static List<string> GetAnnotationOnValues(PdfDictionary annotation)
    {
        var result = new List<string>();
        var appearance = annotation.Elements.GetDictionary("/AP");
        var normal = appearance?.Elements.GetDictionary("/N");
        if (normal is null)
        {
            return result;
        }

        foreach (var key in normal.Elements.KeyNames)
        {
            var value = NormalizePdfName(key.ToString());
            if (!string.Equals(value, "/Off", StringComparison.Ordinal))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static string? GetFullAnnotationFieldId(PdfDictionary annotation)
    {
        var components = new List<string>();
        PdfDictionary? current = annotation;
        while (current is not null)
        {
            var fieldName = current.Elements.GetString("/T");
            if (!string.IsNullOrEmpty(fieldName))
            {
                components.Add(fieldName);
            }

            current = ResolveDictionary(current.Elements.GetValue("/Parent"));
        }

        if (components.Count == 0)
        {
            return null;
        }

        components.Reverse();
        return string.Join(".", components);
    }

    private static string? GetInheritedName(PdfDictionary dictionary, string key)
    {
        var current = dictionary;
        while (current is not null)
        {
            var name = current.Elements.GetName(key);
            if (!string.IsNullOrEmpty(name))
            {
                return NormalizePdfName(name);
            }

            current = ResolveDictionary(current.Elements.GetValue("/Parent"));
        }

        return null;
    }

    private static PdfItem? GetInheritedValue(PdfDictionary dictionary, string key)
    {
        var current = dictionary;
        while (current is not null)
        {
            if (current.Elements.TryGetValue(key, out var value))
            {
                return value;
            }

            current = ResolveDictionary(current.Elements.GetValue("/Parent"));
        }

        return null;
    }

    private static PdfDictionary? ResolveDictionary(PdfItem? item)
    {
        item = ResolveItem(item);
        return item as PdfDictionary;
    }

    private static PdfArray? ResolveArray(PdfItem? item)
    {
        item = ResolveItem(item);
        return item as PdfArray;
    }

    private static PdfItem? ResolveItem(PdfItem? item)
        => item switch
        {
            PdfReference reference => reference.Value,
            _ => item
        };

    private static string? GetStringOrName(PdfItem? item)
    {
        item = ResolveItem(item);
        if (item is null)
        {
            return null;
        }

        var raw = item.ToString();
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        if (raw.StartsWith('(') && raw.EndsWith(')'))
        {
            return raw[1..^1];
        }

        return raw;
    }

    private static string NormalizePdfName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        return value.StartsWith("/", StringComparison.Ordinal) ? value : "/" + value;
    }

    private static double[] ToRectArray(PdfRectangle rect)
        => [rect.X1, rect.Y1, rect.X2, rect.Y2];

    private static double[]? GetSortRect(FormFieldInfo field)
        => field.RadioOptions?.FirstOrDefault()?.Rect ?? field.Rect;

    private static Bitmap LoadDocnetPageImage(Docnet.Core.Readers.IPageReader pageReader)
    {
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        var bytes = pageReader.GetImage();
        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, Math.Min(bytes.Length, Math.Abs(data.Stride) * height));
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private sealed record ResolvedField(PdfAcroField Field);
}
