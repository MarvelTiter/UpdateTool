using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UpdateTool.Core.Models;

public enum JsonNodeType
{
    String,
    Number,
    Boolean,
    Null,
    Object,
    Array
}

public class JsonNodeTypeViewModel
{
    public JsonNodeType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public partial class ReadedJsonNode : ObservableObject
{

    private static readonly ObservableCollection<JsonNodeTypeViewModel> jsonNodeTypes =
    [
        new() { Type = JsonNodeType.String, DisplayName = "字符串" },
        new() { Type = JsonNodeType.Number, DisplayName = "数字" },
        new() { Type = JsonNodeType.Boolean, DisplayName = "布尔值" },
        new() { Type = JsonNodeType.Null, DisplayName = "空值" },
        new() { Type = JsonNodeType.Object, DisplayName = "对象", IsEnabled = false },
        new() { Type = JsonNodeType.Array, DisplayName = "数组", IsEnabled = false }
    ];

    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    public partial JsonNodeType Type { get; set; } = JsonNodeType.String;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public bool IsAdd { get; set; }

    public bool IsScalarValue => Type is not JsonNodeType.Object && Type is not JsonNodeType.Array;

    public bool IsSelectedEnabled => !IsScalarValue || Parent is null;

    public ObservableCollection<JsonNodeTypeViewModel> JsonNodeTypes => jsonNodeTypes;

    public ReadedJsonNode? Parent { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ReadedJsonNode> Children { get; set; } = new();

    public ReadedJsonNode()
    {
    }

    public ReadedJsonNode(string key, string value, JsonNodeType type)
    {
        Key = key;
        Value = value;
        Type = type;
    }

    public ReadedJsonNode DeepClone()
    {
        var clone = new ReadedJsonNode
        {
            Key = Key,
            Value = Value,
            Type = Type,
            IsExpanded = true
        };

        foreach (var child in Children)
        {
            var childClone = child.DeepClone();
            childClone.Parent = clone;
            clone.Children.Add(childClone);
        }

        return clone;
    }
}
