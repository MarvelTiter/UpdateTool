using AutoInjectGenerator;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UpdateTool.Core;
using UpdateTool.Core.Models;
using UpdateTool.Core.Services;

namespace UpdateTool.ViewModels;

[AutoInjectSelf]
internal partial class UpdateViewModel(ILogger<UpdateViewModel> logger
        , UpdateRequest setting
        , UpdateService updateService
    , StepRunner<UpdateRequest> runner) : ViewModelBase
{
    [ObservableProperty]
    public partial UpdateRequest Setting { get; set; } = setting;

    [ObservableProperty]
    public partial ObservableCollection<BackupInfo> Backups { get; set; } = [];

    [RelayCommand]
    private async Task SelectFileAsync(Window window)
    {
        if (window is null) return;
        var storageProvider = window.StorageProvider;
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
            var file = files[0];
            Setting.UpdatePackage = file.TryGetLocalPath()!;
            var ext = Path.GetExtension(file.Name).ToLower();
            Setting.FileName = Path.GetFileNameWithoutExtension(file.Name);
            Setting.FileExt = ext;
        }
    }

    [RelayCommand]
    private async Task SelectProcessAsync(Window window)
    {
        var storageProvider = window.StorageProvider;
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
            var file = files[0];
            Setting.ProcessName = Path.GetFileNameWithoutExtension(file.Name);
            var parent = await file.GetParentAsync();
            Setting.ApplicationPath = parent?.TryGetLocalPath();
            Refresh();
        }
    }

    [RelayCommand]
    private async Task SelectAppFolderPath(Window window)
    {
        var storageProvider = window.StorageProvider;
        var folders = await storageProvider.OpenFolderPickerAsync(new()
        {
            AllowMultiple = false,
            Title = "选择目录"
        });
        var folder = folders.FirstOrDefault()?.TryGetLocalPath();
        Setting.ApplicationPath = folder;
        Refresh();
    }

    [RelayCommand]
    private async Task SelectBackupFolderPath(Window window)
    {
        var storageProvider = window.StorageProvider;
        var folders = await storageProvider.OpenFolderPickerAsync(new()
        {
            AllowMultiple = false,
            Title = "选择目录"
        });
        var folder = folders.FirstOrDefault()?.TryGetLocalPath();
        if (folder is not null)
        {
            Setting.BackupDirectory = folder;
            Refresh();
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
        }
    }

    [ObservableProperty]
    public partial bool DebugMode { get; set; }

    partial void OnDebugModeChanged(bool value)
    {
        runner.SetStepWaitEnabled(value);
    }

    [RelayCommand]
    private void RunNext()
    {
        runner.Continue();
    }

    [RelayCommand]
    private async Task Rollback(BackupInfo backup)
    {
        await updateService.RollbackAsync(Setting, backup.Folder);
        updateService.DeleteBackupFiles(backup.Folder);
        Refresh();
    }

    [RelayCommand]
    private void DeleteBackup(BackupInfo backup)
    {
        updateService.DeleteBackupFiles(backup.Folder);
        Refresh();
    }

    private void Refresh()
    {
        if (!Path.IsPathRooted(Setting.BackupDirectory) && string.IsNullOrEmpty(Setting.ApplicationPath))
        {
            return;
        }
        var backup = Path.IsPathRooted(Setting.BackupDirectory) ? Setting.BackupDirectory : Path.Combine(Setting.ApplicationPath, Setting.BackupDirectory);
        var folders = Directory.EnumerateDirectories(backup).Select(f =>
        {
            var ct = Directory.GetCreationTime(f);
            var full = Path.GetFullPath(f);
            return new BackupInfo(full, ct);
        }).OrderByDescending(b => b.CreateTime);
        Backups = new ObservableCollection<BackupInfo>(folders);
    }
}
