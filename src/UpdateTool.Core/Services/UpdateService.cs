using AutoInjectGenerator;
using Microsoft.Extensions.Logging;
using UpdateTool.Core.Extensions;
using UpdateTool.Core.Models;

namespace UpdateTool.Core.Services;

[AutoInjectSelf]
public class UpdateService
{
    private readonly ILogger<UpdateService> logger;
    private readonly ProcessService processService;
    private readonly FileUpdateService fileService;
    private readonly StepRunner<UpdateRequest> runner;

    public UpdateService(ILogger<UpdateService> logger
    , ProcessService processService
    , FileUpdateService fileService
    , StepRunner<UpdateRequest> runner)
    {
        this.logger = logger;
        this.processService = processService;
        this.fileService = fileService;
        this.runner = runner;
        this.runner.SetStart(解压文件);
    }

    private async Task<StepActionDelegate<UpdateRequest>?> 解压文件(UpdateRequest context, CancellationToken cancellationToken)
    {
        logger.LogInformation("解压文件");
        context.CurrentTempPath = await context.ExtractFilesAsync();
        if (context.StopProcess)
        {
            return 停止进程;
        }
        return 替换文件;
    }

    private async Task<StepActionDelegate<UpdateRequest>?> 停止进程(UpdateRequest context, CancellationToken cancellationToken)
    {
        // 停止进程
        logger.LogInformation("停止目标进程");
        var count = await processService.StopProcessAsync(context.ProcessName, context.ProcessPort);
        logger.LogInformation("已停止{count}个进程, 等待{WaitSecondsBeforeReplace}秒后替换文件", count, context.WaitSecondsBeforeReplace);
        // 等待确保进程完全退出
        await Task.Delay(context.WaitSecondsBeforeReplace * 1000, cancellationToken);
        return 替换文件;
    }

    private async Task<StepActionDelegate<UpdateRequest>?> 替换文件(UpdateRequest context, CancellationToken cancellationToken)
    {
        logger.LogInformation("替换文件");
        await fileService.ReplaceFilesAsync(context);
        return 处理旧文件;
    }

    private async Task<StepActionDelegate<UpdateRequest>?> 处理旧文件(UpdateRequest context, CancellationToken cancellationToken)
    {
        if (context.CreateBackup)
        {
            string backupPath;
            logger.LogInformation("转移备份旧文件");
            var currentFolder = $"backup_{DateTime.Now:yyyyMMddHHmmss}";
            if (Path.IsPathRooted(context.BackupDirectory))
            {
                backupPath = Path.Combine(context.BackupDirectory, currentFolder);
            }
            else
            {
                backupPath = Path.Combine(context.ApplicationPath!, context.BackupDirectory, currentFolder);
            }
            logger.LogInformation("创建备份文件夹:{backupPath}", backupPath);
            Directory.CreateDirectory(backupPath);
            await fileService.BackupOldFiles(backupPath, context.ApplicationPath);
            logger.LogInformation("完成备份:{backupPath}", backupPath);
            context.CurrentBackupPath = backupPath;
        }
        else
        {
            logger.LogInformation("删除旧文件");
            await fileService.CleanupOldFiles(context.ApplicationPath);
        }
        return 更新配置文件;
    }

    private async Task<StepActionDelegate<UpdateRequest>?> 更新配置文件(UpdateRequest context, CancellationToken cancellationToken)
    {
        if (context.UpdateSettingFile && context.CurrentBackupPath is not null)
        {
            logger.LogInformation("更新配置文件");
            await fileService.UpdateSettingFile(context);
        }
        else if (context.CurrentBackupPath is not null)
        {
            logger.LogInformation("替换配置文件");
            var bakSetting = Path.Combine(context.CurrentBackupPath, context.AppSettingFile);
            var currentSetting = Path.Combine(context.ApplicationPath, context.AppSettingFile);
            if (!File.Exists(currentSetting))
                File.Copy(bakSetting, currentSetting, true);
        }
        return 清理临时文件;
    }

    private Task<StepActionDelegate<UpdateRequest>?> 清理临时文件(UpdateRequest context, CancellationToken cancellationToken)
    {
        logger.LogInformation("清理临时文件");
        fileService.RemoveTempFiles(context);
        return Task.FromResult<StepActionDelegate<UpdateRequest>?>(启动应用);
    }

    private Task<StepActionDelegate<UpdateRequest>?> 启动应用(UpdateRequest context, CancellationToken cancellationToken)
    {
        if (context.StartProcessAfterUpdate)
        {
            var process = processService.StartApplication(
                Path.Combine(context.ApplicationPath, $"{context.ProcessName}.exe"), "",
                context.ApplicationPath);
            if (process != null)
                logger.LogInformation("应用已启动 (PID: {Id})", process.Id);
        }
        return Task.FromResult<StepActionDelegate<UpdateRequest>?>(null);
    }

    /// <summary>
    /// 执行完整的应用更新流程
    /// </summary>
    /// <param name="request">更新请求参数</param>
    public async Task ExecuteUpdateAsync(UpdateRequest request)
    {
        try
        {
            if (request.StartProcessAfterUpdate && string.IsNullOrEmpty(request.ProcessName))
            {
                logger.LogWarning("更新后自动启动，请选择程序");
                return;
            }
            logger.LogInformation("=======开始更新=======");

            await runner.ExecuteAsync(request, CancellationToken.None);

            logger.LogInformation("=======更新完成=======");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新过程发生错误:{Message}", ex.Message);
            if (request.CurrentTempPath is not null)
            {
                logger.LogInformation("清理临时文件");
                fileService.RemoveTempFiles(request);
            }
        }
    }

    /// <summary>
    /// 回滚更新（使用备份恢复）
    /// </summary>
    public async Task RollbackAsync(UpdateRequest request, string? rollbackFolder = null)
    {
        ArgumentNullException.ThrowIfNull(rollbackFolder, "还原目录为空");
        await processService.StopProcessAsync(request.ProcessName, request.ProcessPort);
        await Task.Delay(1000);
        // 恢复文件
        await fileService.RestoreBackupFiles(request, rollbackFolder);

    }

    public void DeleteBackupFiles(string folder)
    {
        fileService.RemoveBackupFiles(folder);
    }
}