using AutoInjectGenerator;
using Microsoft.Extensions.Logging;
using UpdateTool.Core.Extensions;
using UpdateTool.Core.Models;

namespace UpdateTool.Core.Services;

[AutoInjectSelf]
public class UpdateService(ILogger<UpdateService> logger, ProcessService processService, FileUpdateService fileService)
{
    /// <summary>
    /// 执行完整的应用更新流程
    /// </summary>
    /// <param name="request">更新请求参数</param>
    public async Task ExecuteUpdateAsync(UpdateRequest request)
    {
        string? tempPath = null;
        try
        {
            if (request.StartProcessAfterUpdate && string.IsNullOrEmpty(request.ProcessName))
            {
                logger.LogWarning("更新后自动启动，请选择程序");
                return;
            }
            logger.LogInformation("=======开始更新=======");
            var excludeFolders = request.ExcludeFolder.Split(',', StringSplitOptions.RemoveEmptyEntries);
            // 备份原文件与解压新文件
            logger.LogInformation("解压文件");
            tempPath = await request.ExtractFilesAsync();
            if (request.StopProcess)
            {
                // 停止进程
                logger.LogInformation("停止目标进程");
                var count = await processService.StopProcessAsync(request.ProcessName, request.ProcessPort);
                logger.LogInformation("已停止{count}个进程, 等待{WaitSecondsBeforeReplace}秒后替换文件", count, request.WaitSecondsBeforeReplace);
                // 等待确保进程完全退出
                await Task.Delay(request.WaitSecondsBeforeReplace * 1000);
            }
            logger.LogInformation("替换文件");
            await fileService.ReplaceFilesAsync(tempPath, request, excludeFolders);

            // 处理旧文件
            string? backupPath = null;
            if (request.CreateBackup)
            {
                logger.LogInformation("转移备份旧文件");
                var currentFolder = $"backup_{DateTime.Now:yyyyMMddHHmmss}";
                if (Path.IsPathRooted(request.BackupDirectory))
                {
                    backupPath = Path.Combine(request.BackupDirectory, currentFolder);
                }
                else
                {
                    backupPath = Path.Combine(request.ApplicationPath!, request.BackupDirectory, currentFolder);
                }
                logger.LogInformation("创建备份文件夹:{backupPath}", backupPath);
                Directory.CreateDirectory(backupPath);
                await fileService.BackupOldFiles(backupPath, request.ApplicationPath);
            }
            else
            {
                logger.LogInformation("删除旧文件");
                await fileService.CleanupOldFiles(request.ApplicationPath);
            }

            // 更新 appsettings.json
            if (request.UpdateSettingFile && backupPath is not null)
            {
                logger.LogInformation("更新配置文件");
                await fileService.UpdateSettingFile(request.ApplicationPath, backupPath, request.AppSettingFile);
            }
            else if (backupPath is not null)
            {
                var bakSetting = Path.Combine(backupPath, request.AppSettingFile);
                var currentSetting = Path.Combine(request.ApplicationPath, request.AppSettingFile);
                File.Move(bakSetting, currentSetting, true);
            }
            // 清理临时文件
            logger.LogInformation("清理临时文件");
            fileService.RemoveTempFiles(tempPath);

            // 启动应用
            if (request.StartProcessAfterUpdate)
            {
                var process = processService.StartApplication(
                    Path.Combine(request.ApplicationPath, $"{request.ProcessName}.exe"), "",
                    request.ApplicationPath);
                if (process != null)
                    logger.LogInformation("应用已启动 (PID: {Id})", process.Id);
            }
            logger.LogInformation("=======更新完成=======");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新过程发生错误:{Message}", ex.Message);
            if (tempPath is not null)
            {
                logger.LogInformation("清理临时文件");
                fileService.RemoveTempFiles(tempPath);
            }
        }

    }

    /// <summary>
    /// 回滚更新（使用备份恢复）
    /// </summary>
    public async Task RollbackAsync(string backupPath, string targetPath, bool restartProcess = true, string? processName = null)
    {
        if (restartProcess && !string.IsNullOrEmpty(processName))
        {
            await processService.StopProcessAsync(processName);
            await Task.Delay(1000);
        }

        // 恢复文件
        fileService.RestoreBackupFiles(backupPath);

    }
}