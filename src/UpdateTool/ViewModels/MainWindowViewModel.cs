using AutoInjectGenerator;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UpdateTool.Core.Models;
using UpdateTool.Core.Services;

namespace UpdateTool.ViewModels
{
    [AutoInjectSelf]
    internal partial class MainWindowViewModel(ILogger<MainWindowViewModel> logger
        , UpdateRequest setting
        , UpdateService updateService) : ViewModelBase
    {
        [ObservableProperty]
        public partial UpdateRequest Setting { get; set; } = setting;

        [RelayCommand]
        private async Task SelectFileAsync(string type)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
            {
                var storageProvider = desktop.MainWindow.StorageProvider;
                if (type == "Package")
                {
                    var options = new FilePickerOpenOptions
                    {
                        Title = "选择升级文件(.dll/.7z/.zip/.rar)",
                        AllowMultiple = false,
                        FileTypeFilter = [new FilePickerFileType("升级文件")
                        {
                            Patterns = ["*.dll","*.7z","*.zip","*.rar"],
                        }],
                    };
                    var files = await storageProvider.OpenFilePickerAsync(options);
                    if (files.Count > 0)
                    {
                        var file = files.First();
                        Setting.UpdatePackage = file.TryGetLocalPath()!;
                        var ext = Path.GetExtension(file.Name).ToLower();
                        Setting.FileName = Path.GetFileNameWithoutExtension(file.Name);
                        Setting.FileExt = ext;
                    }
                }
                else if (type == "ProcessName")
                {
                    var options = new FilePickerOpenOptions
                    {
                        Title = "选择程序文件(.exe)",
                        AllowMultiple = false,
                        FileTypeFilter = [new FilePickerFileType("程序入口")
                        {
                            Patterns = ["*.exe"],
                        }],
                    };
                    var files = await storageProvider.OpenFilePickerAsync(options);
                    if (files.Count > 0)
                    {
                        var file = files.First();
                        Setting.ProcessName = Path.GetFileNameWithoutExtension(file.Name);
                        var parent = await file.GetParentAsync();
                        Setting.ApplicationPath = parent?.TryGetLocalPath();
                    }
                }
            }
        }

        [RelayCommand]
        private async Task SelectFolderPath(string type)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
            {
                var storageProvider = desktop.MainWindow.StorageProvider;
                var folders = await storageProvider.OpenFolderPickerAsync(new()
                {
                    AllowMultiple = false,
                    Title = "选择目录"
                });
                var folder = folders.FirstOrDefault()?.TryGetLocalPath();
                if (type == nameof(UpdateRequest.ApplicationPath))
                {
                    Setting.ApplicationPath = folder;
                }

                else if (type == nameof(UpdateRequest.BackupDirectory) && folder is not null)
                {
                    Setting.BackupDirectory = folder;
                }
            }
        }

        [RelayCommand]
        private async Task ExecuteUpdateAsync()
        {
            try
            {
                ArgumentNullException.ThrowIfNull(Setting.ApplicationPath);
                await updateService.ExecuteUpdateAsync(Setting);
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "{Message}", ex.Message);
                await updateService.RollbackAsync(Setting.BackupDirectory, Setting.ApplicationPath!);
            }
        }
    }
}
