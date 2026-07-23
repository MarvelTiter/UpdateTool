using AutoInjectGenerator;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using UpdateTool.Core.Models;

namespace UpdateTool.Core.Services;

[AutoInjectSelf]
public class FileUpdateService(ILogger<FileUpdateService> logger)
{
    private static bool IsPathInFolder(string filePath, string folderName)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (string.IsNullOrEmpty(directory))
            return false;

        // 分割路径并检查
        var pathParts = directory.Split(Path.DirectorySeparatorChar);
        return pathParts.Contains(folderName, StringComparer.OrdinalIgnoreCase);
    }
    /// <summary>
    /// 用新文件替换目录中的文件
    /// </summary>
    public async Task ReplaceFilesAsync(UpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.CurrentTempPath);
        var updatePath = request.CurrentTempPath;
        var subs = Directory.GetDirectories(updatePath);
        if (subs.Length == 1)
        {
            updatePath = subs[0];

        }
        var files = Directory.EnumerateFiles(updatePath, "*", SearchOption.AllDirectories);

        var excludeFolders = request.ExcludeFolder.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var file in files)
        {
            // 检查是否在排除列表中
            if (excludeFolders?.Length > 0)
            {
                if (excludeFolders.Any(f => IsPathInFolder(file, f)))
                {
                    continue;
                }
            }

            var relativePath = Path.GetRelativePath(updatePath, file);
            var destFile = Path.Combine(request.ApplicationPath, relativePath);
            var destDir = Path.GetDirectoryName(destFile);

            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // 备份原文件（如果有的话）
            if (File.Exists(destFile))
            {
                File.Move(destFile, $"{destFile}.temp", overwrite: true);
            }
            await CopyFileWithRetryAsync(file, destFile);
        }
    }

    /// <summary>
    /// 复制单个文件
    /// </summary>
    private async Task CopyFileWithRetryAsync(string source, string destination, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var folder = Path.GetDirectoryName(destination);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder!);
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (IOException ex) when (i < maxRetries - 1)
            {
                logger.LogError(ex, "移动文件发生错误:{Message}", ex.Message);
                await Task.Delay(500);
            }
        }
    }


    public async Task BackupOldFiles(string backupFolder, string applicationPath)
    {
        var backupFiles = Directory.EnumerateFiles(applicationPath, "*.temp", SearchOption.AllDirectories);
        foreach (var backupFile in backupFiles)
        {
            var relativePath = Path.GetRelativePath(applicationPath, backupFile);
            var destFile = Path.Combine(backupFolder, relativePath);
            await CopyFileWithRetryAsync(backupFile, destFile.Replace(".temp", ""));
        }
    }

    public async Task CleanupOldFiles(string applicationPath)
    {
        var backupFiles = Directory.GetFiles(applicationPath, "*.temp", SearchOption.AllDirectories);
        foreach (var backupFile in backupFiles)
        {
            try
            {
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理旧文件发生错误:{Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 恢复备份文件
    /// </summary>
    /// <param name="directory">包含备份文件的目录</param>
    public void RestoreBackupFiles(string directory)
    {
        var backupFiles = Directory.GetFiles(directory, "*.bak", SearchOption.AllDirectories);

        foreach (var backupFile in backupFiles)
        {
            var originalFile = backupFile.Substring(0, backupFile.Length - 4); // 移除 .bak
            try
            {
                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, originalFile, overwrite: true);
                }
            }
            catch
            {
                // 忽略恢复失败的备份文件
            }
        }
    }

    public async Task UpdateSettingFile(UpdateRequest request)
    {
        try
        {
            var newFilePath = request.ApplicationPath;
            var originalFilePath = request.CurrentBackupPath!;
            var settingFile = request.AppSettingFile;
            var newJsons = Directory.EnumerateFiles(newFilePath, "*.json", SearchOption.AllDirectories).FirstOrDefault(f => f.EndsWith(settingFile));
            var oldJsons = Directory.EnumerateFiles(originalFilePath, "*.json", SearchOption.AllDirectories).FirstOrDefault(f => f.EndsWith(settingFile));
            if (newJsons is null || oldJsons is null)
            {
                logger.LogInformation("新配置文件或者旧配置文件不存在");
                return;
            }

            var jt = new JsonTreeViewModel();
            await jt.OpenFileAsync(newJsons);
            await jt.MergeFromFileAsync(oldJsons);
            var json = jt.ToJsonString();
            var newSettingFile = Path.Combine(newFilePath, settingFile);
            var tempFile = newSettingFile + ".tmp";
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, newSettingFile, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新配置文件发生错误:{Message}", ex.Message);
        }


    }

    public void RemoveTempFiles(UpdateRequest request)
    {
        try
        {
            if (request.CurrentTempPath is null) 
                return;
            Directory.Delete(request.CurrentTempPath,true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清理临时文件发生错误:{Message}", ex.Message);

        }
    }
}
