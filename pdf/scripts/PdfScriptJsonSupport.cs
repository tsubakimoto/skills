using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfScripts;

internal static class JsonUtilities
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static T LoadFromFile<T>(string path)
    {
        using var stream = File.OpenRead(path);
        var value = JsonSerializer.Deserialize<T>(stream, Options);
        return value ?? throw new InvalidDataException($"Failed to deserialize JSON from '{path}'.");
    }

    public static void SaveToFile<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, Options);
    }
}

internal sealed class FieldsDocument
{
    [JsonPropertyName("pages")]
    public List<PageInfo> Pages { get; set; } = [];

    [JsonPropertyName("form_fields")]
    public List<FormFieldDefinition> FormFields { get; set; } = [];
}

internal sealed class PageInfo
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("image_width")]
    public double? ImageWidth { get; set; }

    [JsonPropertyName("image_height")]
    public double? ImageHeight { get; set; }

    [JsonPropertyName("pdf_width")]
    public double? PdfWidth { get; set; }

    [JsonPropertyName("pdf_height")]
    public double? PdfHeight { get; set; }
}

internal sealed class FormFieldDefinition
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("field_label")]
    public string? FieldLabel { get; set; }

    [JsonPropertyName("label_bounding_box")]
    public double[] LabelBoundingBox { get; set; } = [];

    [JsonPropertyName("entry_bounding_box")]
    public double[] EntryBoundingBox { get; set; } = [];

    [JsonPropertyName("entry_text")]
    public EntryTextDefinition? EntryText { get; set; }
}

internal sealed class EntryTextDefinition
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("font")]
    public string? Font { get; set; }

    [JsonPropertyName("font_size")]
    public double? FontSize { get; set; }

    [JsonPropertyName("font_color")]
    public string? FontColor { get; set; }
}

internal sealed class FieldValueInput
{
    [JsonPropertyName("field_id")]
    public string FieldId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

internal sealed class FormFieldInfo
{
    [JsonPropertyName("field_id")]
    public string FieldId { get; set; } = string.Empty;

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("rect")]
    public double[]? Rect { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("checked_value")]
    public string? CheckedValue { get; set; }

    [JsonPropertyName("unchecked_value")]
    public string? UncheckedValue { get; set; }

    [JsonPropertyName("radio_options")]
    public List<RadioOption>? RadioOptions { get; set; }

    [JsonPropertyName("choice_options")]
    public List<ChoiceOption>? ChoiceOptions { get; set; }
}

internal sealed class RadioOption
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("rect")]
    public double[] Rect { get; set; } = [];
}

internal sealed class ChoiceOption
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal sealed class FormStructureDocument
{
    [JsonPropertyName("pages")]
    public List<StructurePageInfo> Pages { get; set; } = [];

    [JsonPropertyName("labels")]
    public List<StructureLabel> Labels { get; set; } = [];

    [JsonPropertyName("lines")]
    public List<StructureLine> Lines { get; set; } = [];

    [JsonPropertyName("checkboxes")]
    public List<StructureCheckbox> Checkboxes { get; set; } = [];

    [JsonPropertyName("row_boundaries")]
    public List<RowBoundary> RowBoundaries { get; set; } = [];
}

internal sealed class StructurePageInfo
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}

internal sealed class StructureLabel
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("x0")]
    public double X0 { get; set; }

    [JsonPropertyName("top")]
    public double Top { get; set; }

    [JsonPropertyName("x1")]
    public double X1 { get; set; }

    [JsonPropertyName("bottom")]
    public double Bottom { get; set; }
}

internal sealed class StructureLine
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("x0")]
    public double X0 { get; set; }

    [JsonPropertyName("x1")]
    public double X1 { get; set; }
}

internal sealed class StructureCheckbox
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("x0")]
    public double X0 { get; set; }

    [JsonPropertyName("top")]
    public double Top { get; set; }

    [JsonPropertyName("x1")]
    public double X1 { get; set; }

    [JsonPropertyName("bottom")]
    public double Bottom { get; set; }

    [JsonPropertyName("center_x")]
    public double CenterX { get; set; }

    [JsonPropertyName("center_y")]
    public double CenterY { get; set; }
}

internal sealed class RowBoundary
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("row_top")]
    public double RowTop { get; set; }

    [JsonPropertyName("row_bottom")]
    public double RowBottom { get; set; }

    [JsonPropertyName("row_height")]
    public double RowHeight { get; set; }
}

