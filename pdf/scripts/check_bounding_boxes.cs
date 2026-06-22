#:property PublishAot=false
#:property NoWarn=CA2266
#:include PdfScriptJsonSupport.cs

using PdfScripts;

if (args.Length != 1)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\check_bounding_boxes.cs -- [fields.json]");
    Environment.Exit(1);
}

var fields = JsonUtilities.LoadFromFile<FieldsDocument>(args[0]);
var messages = new List<string> { $"Read {fields.FormFields.Count} fields" };

var rectsAndFields = new List<(double[] Rect, string RectType, FormFieldDefinition Field)>();
foreach (var field in fields.FormFields)
{
    rectsAndFields.Add((field.LabelBoundingBox, "label", field));
    rectsAndFields.Add((field.EntryBoundingBox, "entry", field));
}

var hasError = false;
for (var i = 0; i < rectsAndFields.Count; i++)
{
    var current = rectsAndFields[i];
    for (var j = i + 1; j < rectsAndFields.Count; j++)
    {
        var other = rectsAndFields[j];
        if (current.Field.PageNumber == other.Field.PageNumber && RectsIntersect(current.Rect, other.Rect))
        {
            hasError = true;
            if (ReferenceEquals(current.Field, other.Field))
            {
                messages.Add($"FAILURE: intersection between label and entry bounding boxes for `{current.Field.Description}` ({FormatRect(current.Rect)}, {FormatRect(other.Rect)})");
            }
            else
            {
                messages.Add($"FAILURE: intersection between {current.RectType} bounding box for `{current.Field.Description}` ({FormatRect(current.Rect)}) and {other.RectType} bounding box for `{other.Field.Description}` ({FormatRect(other.Rect)})");
            }

            if (messages.Count >= 20)
            {
                messages.Add("Aborting further checks; fix bounding boxes and try again");
                WriteMessages(messages);
                Environment.Exit(0);
            }
        }
    }

    if (current.RectType == "entry" && current.Field.EntryText is not null)
    {
        var fontSize = current.Field.EntryText.FontSize ?? 14;
        var entryHeight = current.Rect.Length == 4 ? current.Rect[3] - current.Rect[1] : 0;
        if (entryHeight < fontSize)
        {
            hasError = true;
            messages.Add($"FAILURE: entry bounding box height ({entryHeight}) for `{current.Field.Description}` is too short for the text content (font size: {fontSize}). Increase the box height or decrease the font size.");
            if (messages.Count >= 20)
            {
                messages.Add("Aborting further checks; fix bounding boxes and try again");
                WriteMessages(messages);
                Environment.Exit(0);
            }
        }
    }
}

if (!hasError)
{
    messages.Add("SUCCESS: All bounding boxes are valid");
}

WriteMessages(messages);

static bool RectsIntersect(double[] r1, double[] r2)
{
    if (r1.Length != 4 || r2.Length != 4)
    {
        return false;
    }

    var disjointHorizontal = r1[0] >= r2[2] || r1[2] <= r2[0];
    var disjointVertical = r1[1] >= r2[3] || r1[3] <= r2[1];
    return !(disjointHorizontal || disjointVertical);
}

static string FormatRect(double[] rect)
    => $"[{string.Join(", ", rect)}]";

static void WriteMessages(IEnumerable<string> messages)
{
    foreach (var message in messages)
    {
        Console.WriteLine(message);
    }
}
