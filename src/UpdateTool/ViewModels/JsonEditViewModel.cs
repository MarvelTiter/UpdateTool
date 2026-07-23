using AutoInjectGenerator;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UpdateTool.Core.Models;

namespace UpdateTool.ViewModels;

[AutoInjectSelf]
public partial class JsonEditViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial JsonTreeViewModel JsonTree { get; set; }

    [ObservableProperty]
    public partial string RawJson { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "JSON Editor";

    public JsonEditViewModel()
    {
        JsonTree = new JsonTreeViewModel();
        JsonTree.JsonChanged += (s, e) => UpdateRawJsonFromTree();
        UpdateWindowTitle();
    }

    private void UpdateRawJsonFromTree()
    {
        if (JsonTree.IsValidJson)
        {
            RawJson = JsonTree.ToJsonString();
        }
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var fileName = string.IsNullOrEmpty(JsonTree.CurrentFilePath)
            ? "Untitled"
            : System.IO.Path.GetFileName(JsonTree.CurrentFilePath);

        var dirtyMark = JsonTree.IsDirty ? "*" : string.Empty;
        WindowTitle = $"{fileName}{dirtyMark} - JSON Editor";
    }

    [RelayCommand]
    private async Task OpenFile(Window window)
    {
        var storageProvider = window.StorageProvider;
        var options = new FilePickerOpenOptions
        {
            Title = "Open JSON File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json", "*.jsonc" } },
                FilePickerFileTypes.All
            }
        };

        var result = await storageProvider.OpenFilePickerAsync(options);
        if (result.Count > 0)
        {
            var filePath = result[0].Path.LocalPath;
            await JsonTree.OpenFileAsync(filePath);
            if (JsonTree.IsValidJson)
            {
                UpdateRawJsonFromTree();
                UpdateWindowTitle();
            }
        }
    }

    [RelayCommand]
    private async Task SaveFile(Window window)
    {
        if (string.IsNullOrEmpty(JsonTree.CurrentFilePath))
        {
            await SaveAsFile(window);
            return;
        }

        UpdateRawJsonFromTree();
        await JsonTree.SaveFileAsync(JsonTree.CurrentFilePath);
        UpdateWindowTitle();
    }

    [RelayCommand]
    private async Task MergeFromFile(Window window)
    {
        var storageProvider = window.StorageProvider;
        var options = new FilePickerOpenOptions
        {
            Title = "Select JSON File to Merge From",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json", "*.jsonc" } },
                FilePickerFileTypes.All
            }
        };

        var result = await storageProvider.OpenFilePickerAsync(options);
        if (result.Count > 0)
        {
            var filePath = result[0].Path.LocalPath;
            await JsonTree.MergeFromFileAsync(filePath);
            UpdateRawJsonFromTree();
            UpdateWindowTitle();
        }
    }

    private async Task SaveAsFile(Window window)
    {
        var storageProvider = window.StorageProvider;
        var options = new FilePickerSaveOptions
        {
            Title = "Save JSON As",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json", "*.jsonc" } },
                FilePickerFileTypes.All
            }
        };

        var result = await storageProvider.SaveFilePickerAsync(options);
        if (result != null)
        {
            var filePath = result.Path.LocalPath;
            UpdateRawJsonFromTree();
            await JsonTree.SaveFileAsync(filePath);
            UpdateWindowTitle();
        }
    }
}
