using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace UpdateTool;

public partial class JsonEditView : UserControl
{
    public JsonEditView()
    {
        InitializeComponent();
    }

    private void OnTreeItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control)
        {
            var treeViewItem = control.FindAncestorOfType<TreeViewItem>();
            treeViewItem?.IsExpanded = !treeViewItem.IsExpanded;
        }
    }

}