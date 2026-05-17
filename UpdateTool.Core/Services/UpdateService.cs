using System.Text.Json;

namespace UpdateTool.Core.Services;

/// <summary>
/// 应用更新服务 - 整合所有更新功能
/// </summary>
public class UpdateService
{
    private readonly ProcessService _processService;
    private readonly FileUpdateService _fileService;
    private readonly AppSettingsService _appSettingsService;

    public event EventHandler<UpdateProgressEventArgs>? ProgressChanged;
    public event EventHandler<UpdateCompletedEventArgs>? UpdateCompleted;

    public UpdateService()
    {
        _processService = new ProcessService();
        _fileService = new FileUpdateService();
        _appSettingsService = new AppSettingsService();
    }

    /// <summary>
    /// 执行完整的应用更新流程
    /// </summary>
    /// <param name="request">更新请求参数</param>
    public async Task<UpdateResult> ExecuteUpdateAsync(UpdateRequest request)
    {
        var result = new UpdateResult { Success = true };
        var steps = new List<string>();

        try
        {
            OnProgressChanged("开始更新流程...", 0);

            // 1. 备份原文件
            if (request.CreateBackup)
            {
                OnProgressChanged("正在备份原文件...", 10);
                var backupPath = Path.Combine(request.BackupDirectory, $"backup_{DateTime.Now:yyyyMMddHHmmss}");
                await _fileService.BackupDirectoryAsync(request.TargetPath, backupPath, request.ExcludePatterns);
                result.BackupPath = backupPath;
                steps.Add($"已备份到: {backupPath}");
            }

            // 2. 停止进程
            if (request.StopProcess)
            {
                OnProgressChanged("正在停止应用进程...", 30);
                int stoppedCount;
                if (!string.IsNullOrEmpty(request.ProcessName))
                {
                    stoppedCount = await _processService.StopProcessByNameAsync(request.ProcessName);
                }
                else
                {
                    stoppedCount = 0;
                }

                steps.Add($"已停止 {stoppedCount} 个进程");

                // 等待确保进程完全退出
                await Task.Delay(request.WaitSecondsBeforeReplace * 1000);
            }

            // 3. 替换文件
            OnProgressChanged("正在替换文件...", 50);
            await _fileService.ReplaceFilesAsync(request.UpdatePath, request.TargetPath, request.ExcludePatterns);
            steps.Add("文件替换完成");

            // 4. 更新 appsettings.json
            if (request.AppSettingsUpdates?.Count > 0)
            {
                OnProgressChanged("正在更新配置文件...", 70);
                var appSettingsPath = Path.Combine(request.TargetPath, "appsettings.json");

                if (File.Exists(appSettingsPath))
                {
                    // 保留需要保留的配置项
                    if (request.PreserveAppSettings?.Count > 0)
                    {
                        await _appSettingsService.PreserveAndUpdateAsync(
                            appSettingsPath,
                            request.PreserveAppSettings,
                            request.AppSettingsUpdates);
                    }
                    else
                    {
                        await _appSettingsService.UpdateAppSettingsAsync(appSettingsPath, request.AppSettingsUpdates);
                    }
                    steps.Add("配置文件已更新");
                }
            }

            // 5. 启动应用（可选）
            if (request.StartAfterUpdate && !string.IsNullOrEmpty(request.ApplicationPath))
            {
                OnProgressChanged("正在启动应用...", 90);
                var process = _processService.StartApplication(
                    request.ApplicationPath,
                    request.ApplicationArguments ?? "",
                    Path.GetDirectoryName(request.ApplicationPath));
                result.StartedProcessId = process.Id;
                steps.Add($"应用已启动 (PID: {process.Id})");
            }

            OnProgressChanged("更新完成！", 100);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.Steps = steps;
        OnCompleted(result);

        return result;
    }

    /// <summary>
    /// 预览更新内容
    /// </summary>
    public List<FileInfo> PreviewUpdate(string updatePath, string targetPath, string[]? excludePatterns = null)
    {
        return _fileService.GetDirectoryFiles(updatePath, excludePatterns);
    }

    /// <summary>
    /// 回滚更新（使用备份恢复）
    /// </summary>
    public async Task RollbackAsync(string backupPath, string targetPath, bool restartProcess = true, string? processName = null)
    {
        if (restartProcess && !string.IsNullOrEmpty(processName))
        {
            await _processService.StopProcessByNameAsync(processName);
            await Task.Delay(1000);
        }

        // 恢复文件
        _fileService.RestoreBackupFiles(backupPath);

        // 清理备份文件
        _fileService.CleanupBackupFiles(backupPath);
    }

    /// <summary>
    /// 获取应用当前配置
    /// </summary>
    public async Task<Dictionary<string, JsonElement>> GetCurrentSettingsAsync(string appSettingsPath)
    {
        return await _appSettingsService.ReadAppSettingsAsync(appSettingsPath);
    }

    private void OnProgressChanged(string message, int percentage)
    {
        ProgressChanged?.Invoke(this, new UpdateProgressEventArgs(message, percentage));
    }

    private void OnCompleted(UpdateResult result)
    {
        UpdateCompleted?.Invoke(this, new UpdateCompletedEventArgs(result));
    }
}

/// <summary>
/// 更新请求参数
/// </summary>
public class UpdateRequest
{
    /// <summary>更新包路径</summary>
    public string UpdatePath { get; set; } = "";

    /// <summary>目标应用路径</summary>
    public string TargetPath { get; set; } = "";

    /// <summary>备份目录</summary>
    public string BackupDirectory { get; set; } = "./backups";

    /// <summary>是否创建备份</summary>
    public bool CreateBackup { get; set; } = true;

    /// <summary>是否停止进程</summary>
    public bool StopProcess { get; set; } = true;

    /// <summary>进程名称（不含扩展名）</summary>
    public string? ProcessName { get; set; }

    /// <summary>进程占用的端口</summary>
    public int ProcessPort { get; set; }

    /// <summary>替换前等待秒数</summary>
    public int WaitSecondsBeforeReplace { get; set; } = 2;

    /// <summary>排除的文件模式（如 *.pdb）</summary>
    public string[]? ExcludePatterns { get; set; }

    /// <summary>appsettings.json 的更新项</summary>
    public Dictionary<string, string?>? AppSettingsUpdates { get; set; }

    /// <summary>appsettings.json 中需要保留的项</summary>
    public Dictionary<string, string?>? PreserveAppSettings { get; set; }

    /// <summary>更新后是否启动应用</summary>
    public bool StartAfterUpdate { get; set; }

    /// <summary>应用可执行文件路径</summary>
    public string? ApplicationPath { get; set; }

    /// <summary>应用启动参数</summary>
    public string? ApplicationArguments { get; set; }
}

/// <summary>
/// 更新结果
/// </summary>
public class UpdateResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BackupPath { get; set; }
    public int? StartedProcessId { get; set; }
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// 更新进度事件参数
/// </summary>
public class UpdateProgressEventArgs : EventArgs
{
    public string Message { get; }
    public int Percentage { get; }

    public UpdateProgressEventArgs(string message, int percentage)
    {
        Message = message;
        Percentage = percentage;
    }
}

/// <summary>
/// 更新完成事件参数
/// </summary>
public class UpdateCompletedEventArgs : EventArgs
{
    public UpdateResult Result { get; }

    public UpdateCompletedEventArgs(UpdateResult result)
    {
        Result = result;
    }
}
