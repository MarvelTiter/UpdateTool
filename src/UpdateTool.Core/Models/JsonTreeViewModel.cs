using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UpdateTool.Core.Models;

public partial class JsonTreeViewModel : ObservableObject
{
    [GeneratedRegex("[\r|\n|\\s]+")]
    private static partial Regex StringClean();

    [ObservableProperty]
    public partial ObservableCollection<ReadedJsonNode> RootNodes { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnableDuplicate))]
    public partial ReadedJsonNode? SelectedNode { get; set; }

    public bool EnableDuplicate => SelectedNode?.Parent?.Type == JsonNodeType.Array;

    [ObservableProperty]
    public partial string CurrentFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    [ObservableProperty]
    public partial bool IsValidJson { get; set; } = true;

    [ObservableProperty]
    public partial ObservableCollection<ReadedJsonNode> ScalarFields { get; set; } = [];

    public event EventHandler? JsonChanged;

    partial void OnSelectedNodeChanged(ReadedJsonNode? value)
    {
        value?.IsExpanded = true;
        RefreshScalarFields();
    }

    private void RefreshScalarFields()
    {
        ScalarFields.Clear();
        if (SelectedNode == null) return;
        CollectScalars(SelectedNode, ScalarFields);
    }

    private static void CollectScalars(ReadedJsonNode node, ObservableCollection<ReadedJsonNode> result)
    {
        if (node.Type is JsonNodeType.String or JsonNodeType.Number or JsonNodeType.Boolean or JsonNodeType.Null)
        {
            result.Add(node);
        }
        else
        {
            foreach (var child in node.Children)
            {
                //if (child.Type is JsonNodeType.String or JsonNodeType.Number or JsonNodeType.Boolean or JsonNodeType.Null)
                //{
                //}
                result.Add(child);
            }
        }
    }

    public async Task<bool> OpenFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            if (!TryParseJson(json))
            {
                StatusMessage = "Failed to parse JSON";
                return false;
            }

            CurrentFilePath = filePath;
            IsDirty = false;
            StatusMessage = $"Opened: {Path.GetFileName(filePath)}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening file: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> SaveFileAsync(string filePath)
    {
        try
        {
            var json = ToJsonString();
            await File.WriteAllTextAsync(filePath, json);
            CurrentFilePath = filePath;
            IsDirty = false;
            StatusMessage = $"Saved: {Path.GetFileName(filePath)}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving file: {ex.Message}";
            return false;
        }
    }

    public async Task MergeFromFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);

            using var document = JsonDocument.Parse(json, jsonOption);
            var root = document.RootElement;

            int mergedCount = 0;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    mergedCount += MergeValue(RootNodes, prop.Name, prop.Value);
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in root.EnumerateArray())
                {
                    mergedCount += MergeValue(RootNodes, $"[{index}]", item);
                    index++;
                }
            }

            IsDirty = true;
            OnJsonChanged();
            StatusMessage = $"Merged {mergedCount} value(s) from {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error merging file: {ex.Message}";
        }
    }

    private int MergeValue(ObservableCollection<ReadedJsonNode> nodes, string key, JsonElement value)
    {
        foreach (var node in nodes)
        {
            if (node.Key != key) continue;

            if (value.ValueKind == JsonValueKind.Object && node.Type == JsonNodeType.Object)
            {
                int count = 0;
                foreach (var prop in value.EnumerateObject())
                {
                    count += MergeValue(node.Children, prop.Name, prop.Value);
                }
                return count;
            }

            if (value.ValueKind == JsonValueKind.Array && node.Type == JsonNodeType.Array)
            {
                int count = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var sourceRaw = StringClean().Replace(item.GetRawText(), "");
                    bool exists = false;
                    foreach (var child in node.Children)
                    {
                        var sou = SerializeNode(child);
                        if (sou == sourceRaw)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        var newNode = CreateNode($"[{node.Children.Count}]", item);
                        newNode.Parent = node;
                        node.Children.Add(newNode);
                        count++;
                    }
                }
                return count;
            }

            node.Value = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => node.Value
            };
            node.Type = value.ValueKind switch
            {
                JsonValueKind.String => JsonNodeType.String,
                JsonValueKind.Number => JsonNodeType.Number,
                JsonValueKind.True or JsonValueKind.False => JsonNodeType.Boolean,
                JsonValueKind.Null => JsonNodeType.Null,
                _ => node.Type
            };
            return 1;
        }

        return 0;
    }

    private string SerializeNode(ReadedJsonNode node)
    {
        var domNode = CreateJsonNode(node);
        return domNode?.ToJsonString() ?? "null";
    }

    private static readonly JsonDocumentOptions jsonOption = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public bool TryParseJson(string json)
    {
        try
        {
            RootNodes.Clear();

            //json = StripJsonComments(json);

            var document = JsonDocument.Parse(json, jsonOption);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    RootNodes.Add(CreateNode(prop.Name, prop.Value));
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in root.EnumerateArray())
                {
                    RootNodes.Add(CreateNode($"[{index}]", item));
                    index++;
                }
            }

            IsValidJson = true;
            StatusMessage = "JSON parsed successfully";
            IsDirty = true;
            OnJsonChanged();
            return true;
        }
        catch (JsonException ex)
        {
            IsValidJson = false;
            StatusMessage = $"JSON parse error: {ex.Message}";
            return false;
        }
    }

    private ReadedJsonNode CreateNode(string key, JsonElement element)
    {
        var node = new ReadedJsonNode { Key = key };

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                node.Type = JsonNodeType.Object;
                node.Value = string.Empty;
                foreach (var prop in element.EnumerateObject())
                {
                    var child = CreateNode(prop.Name, prop.Value);
                    child.Parent = node;
                    node.Children.Add(child);
                }
                break;

            case JsonValueKind.Array:
                node.Type = JsonNodeType.Array;
                node.Value = string.Empty;
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var child = CreateNode($"[{index}]", item);
                    child.Parent = node;
                    node.Children.Add(child);
                    index++;
                }
                break;

            case JsonValueKind.String:
                node.Type = JsonNodeType.String;
                node.Value = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
                node.Type = JsonNodeType.Number;
                node.Value = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                node.Type = JsonNodeType.Boolean;
                node.Value = element.GetBoolean().ToString().ToLower();
                break;

            case JsonValueKind.Null:
                node.Type = JsonNodeType.Null;
                node.Value = "null";
                break;
        }

        return node;
    }

    public string ToJsonString()
    {
        var root = BuildJsonDom();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return root?.ToJsonString(options) ?? "null";
    }

    private JsonNode? BuildJsonDom()
    {
        if (RootNodes.Count == 0)
            return null;

        if (RootNodes[0].Key.StartsWith('['))
        {
            var array = new JsonArray();
            foreach (var node in RootNodes)
            {
                AppendChildJson(array, node);
            }
            return array;
        }
        else
        {
            var obj = new JsonObject();
            foreach (var node in RootNodes)
            {
                AddToJsonObject(obj, node);
            }
            return obj;
        }
    }

    private void AddToJsonObject(JsonObject obj, ReadedJsonNode node)
    {
        obj[node.Key] = CreateJsonNode(node);
    }

    private void AppendChildJson(JsonArray array, ReadedJsonNode node)
    {
        array.Add(CreateJsonNode(node));
    }

    private JsonNode? CreateJsonNode(ReadedJsonNode node)
    {
        return node.Type switch
        {
            JsonNodeType.Object => BuildJsonObject(node),
            JsonNodeType.Array => BuildJsonArray(node),
            JsonNodeType.String => node.Value,
            JsonNodeType.Number => double.TryParse(node.Value, out var num)
                ? (JsonNode?)num
                : node.Value,
            JsonNodeType.Boolean => bool.Parse(node.Value),
            JsonNodeType.Null => null,
            _ => node.Value
        };
    }

    private JsonObject BuildJsonObject(ReadedJsonNode node)
    {
        var obj = new JsonObject();
        foreach (var child in node.Children)
        {
            AddToJsonObject(obj, child);
        }
        return obj;
    }

    private JsonArray BuildJsonArray(ReadedJsonNode node)
    {
        var array = new JsonArray();
        foreach (var child in node.Children)
        {
            AppendChildJson(array, child);
        }
        return array;
    }

    [RelayCommand]
    private void AddChildNode()
    {
        if (SelectedNode == null || (SelectedNode.Type != JsonNodeType.Object && SelectedNode.Type != JsonNodeType.Array))
            return;

        var newNode = new ReadedJsonNode
        {
            Key = SelectedNode.Type == JsonNodeType.Array ? $"[{SelectedNode.Children.Count}]" : "newKey",
            Type = JsonNodeType.String,
            Value = string.Empty,
            Parent = SelectedNode,
            IsAdd = true,
        };

        SelectedNode.Children.Add(newNode);
        IsDirty = true;
        OnJsonChanged();
        StatusMessage = "Added new node";
    }

    [RelayCommand]
    private void DuplicateNode()
    {
        if (SelectedNode == null || SelectedNode.Parent is not { } parent)
            return;

        if (parent.Type != JsonNodeType.Array)
            return;

        var clone = SelectedNode.DeepClone();
        clone.Parent = parent;
        parent.Children.Add(clone);
        ReindexArray(parent);

        IsDirty = true;
        OnJsonChanged();
        StatusMessage = "Node duplicated";
    }

    private static void ReindexArray(ReadedJsonNode arrayNode)
    {
        for (int i = 0; i < arrayNode.Children.Count; i++)
        {
            arrayNode.Children[i].Key = $"[{i}]";
        }
    }

    [RelayCommand]
    private void RemoveNode(ReadedJsonNode? node)
    {
        if (node == null) return;

        if (node.Parent is { } parent)
        {
            parent.Children.Remove(node);
            if (parent.Type == JsonNodeType.Array)
            {
                ReindexArray(parent);
            }
            IsDirty = true;
            OnJsonChanged();
            StatusMessage = "Node removed";
        }
        else if (RootNodes.Contains(node))
        {
            RootNodes.Remove(node);
            IsDirty = true;
            OnJsonChanged();
            StatusMessage = "Node removed";
        }
    }

    //[RelayCommand]
    //public void ClearAll()
    //{
    //    RootNodes.Clear();
    //    IsDirty = true;
    //    OnJsonChanged();
    //    StatusMessage = "Cleared all nodes";
    //}

    [RelayCommand]
    private void UpdateSelect(ReadedJsonNode? node)
    {
        SelectedNode = node;
        ExpandWithParentNode(node);
    }

    private static void ExpandWithParentNode(ReadedJsonNode? node)
    {
        node?.IsExpanded = true;
        if (node?.Parent is not null)
        {
            node.Parent.IsExpanded = true;
            ExpandWithParentNode(node.Parent);
        }
    }

    public void MarkAsChanged()
    {
        IsDirty = true;
        OnJsonChanged();
    }

    protected virtual void OnJsonChanged()
    {
        RefreshScalarFields();
        JsonChanged?.Invoke(this, EventArgs.Empty);
    }
}
