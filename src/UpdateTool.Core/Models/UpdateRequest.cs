using AutoInjectGenerator;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UpdateTool.Core.Models;

[AutoInjectSelf]
public partial class UpdateRequest : ObservableObject
{
    public static UpdateRequest LoadFromJson(IServiceProvider _)
    {
        if (File.Exists("config.json"))
        {
            var json = File.ReadAllText("config.json");
            return System.Text.Json.JsonSerializer.Deserialize(json, JsonContext.Default.UpdateRequest) ?? new();
        }
        else
        {
            return new UpdateRequest();
        }
    }
    [ObservableProperty]
    /// <summary>更新包路径</summary>
    public partial string UpdatePackage { get; set; } = "";
    

    [ObservableProperty]
    /// <summary>目标应用路径</summary>
    public partial string TargetPath { get; set; } = "";

    [ObservableProperty]
    /// <summary>备份目录</summary>
    public partial string BackupDirectory { get; set; } = "backups";

    [ObservableProperty]
    /// <summary>是否创建备份</summary>
    public partial bool CreateBackup { get; set; } = true;

    [ObservableProperty]
    /// <summary>是否停止进程</summary>
    public partial bool StopProcess { get; set; } = true;

    [ObservableProperty]
    /// <summary>进程名称（不含扩展名）</summary>
    public partial string? ProcessName { get; set; }

    [ObservableProperty]
    /// <summary>进程占用的端口</summary>
    public partial int? ProcessPort { get; set; }

    [ObservableProperty]
    /// <summary>替换前等待秒数</summary>
    public partial int WaitSecondsBeforeReplace { get; set; } = 2;

    [ObservableProperty, NotNull]
    /// <summary>应用可执行文件路径</summary>
    public partial string? ApplicationPath { get; set; }

    [ObservableProperty]
    public partial string AppSettingFile { get; set; } = "appsettings.json";

    [ObservableProperty]
    public partial bool UpdateSettingFile { get; set; } = true;

    [ObservableProperty]
    public partial string ExcludeFolder { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool StartProcessAfterUpdate { get; set; } = true;

    public string FileName { get; set; } = string.Empty;
    public string FileExt { get; set; } = string.Empty;
    public bool IsPackage => FileExt != ".dll";

}
