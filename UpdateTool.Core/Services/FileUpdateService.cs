namespace UpdateTool.Core.Services;

/// <summary>
/// 文件更新服务 - 文件替换和备份
/// </summary>
public class FileUpdateService
{
    /// <summary>
    /// 备份目录中的所有文件
    /// </summary>
    /// <param name="sourcePath">源目录</param>
    /// <param name="backupPath">备份目录</param>
    /// <param name="excludePatterns">排除的文件模式（如 *.pdb, *.xml）</param>
    public async Task BackupDirectoryAsync(string sourcePath, string backupPath, string[]? excludePatterns = null)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"源目录不存在: {sourcePath}");
        }

        Directory.CreateDirectory(backupPath);

        var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            // 检查是否在排除列表中
            if (excludePatterns != null)
            {
                var fileName = Path.GetFileName(file);
                if (excludePatterns.Any(p => fileName.EndsWith(p.Replace("*", ""), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            var relativePath = Path.GetRelativePath(sourcePath, file);
            var destFile = Path.Combine(backupPath, relativePath);
            var destDir = Path.GetDirectoryName(destFile);
            
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            await CopyFileWithRetryAsync(file, destFile);
        }
    }

    /// <summary>
    /// 用新文件替换目录中的文件
    /// </summary>
    /// <param name="updatePath">更新包目录</param>
    /// <param name="targetPath">目标目录</param>
    /// <param name="excludePatterns">排除的文件模式</param>
    public async Task ReplaceFilesAsync(string updatePath, string targetPath, string[]? excludePatterns = null)
    {
        if (!Directory.Exists(updatePath))
        {
            throw new DirectoryNotFoundException($"更新包目录不存在: {updatePath}");
        }

        if (!Directory.Exists(targetPath))
        {
            throw new DirectoryNotFoundException($"目标目录不存在: {targetPath}");
        }

        var files = Directory.GetFiles(updatePath, "*", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            // 检查是否在排除列表中
            if (excludePatterns != null)
            {
                var fileName = Path.GetFileName(file);
                if (excludePatterns.Any(p => fileName.EndsWith(p.Replace("*", ""), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            var relativePath = Path.GetRelativePath(updatePath, file);
            var destFile = Path.Combine(targetPath, relativePath);
            var destDir = Path.GetDirectoryName(destFile);
            
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // 备份原文件（如果有的话）
            if (File.Exists(destFile))
            {
                var backupPath = destFile + ".bak";
                File.Copy(destFile, backupPath, overwrite: true);
            }

            await CopyFileWithRetryAsync(file, destFile);
        }
    }

    /// <summary>
    /// 复制单个文件
    /// </summary>
    public async Task CopyFileWithRetryAsync(string source, string destination, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(500);
            }
        }
    }

    /// <summary>
    /// 删除备份文件
    /// </summary>
    /// <param name="directory">要清理的目录</param>
    public void CleanupBackupFiles(string directory)
    {
        var backupFiles = Directory.GetFiles(directory, "*.bak", SearchOption.AllDirectories);
        
        foreach (var file in backupFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // 忽略删除失败的备份文件
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

    /// <summary>
    /// 获取目录的文件列表（用于预览更新内容）
    /// </summary>
    public List<FileInfo> GetDirectoryFiles(string path, string[]? excludePatterns = null)
    {
        if (!Directory.Exists(path))
        {
            return new List<FileInfo>();
        }

        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        var result = new List<FileInfo>();
        
        foreach (var file in files)
        {
            if (excludePatterns != null)
            {
                var fileName = Path.GetFileName(file);
                if (excludePatterns.Any(p => fileName.EndsWith(p.Replace("*", ""), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            var info = new FileInfo
            {
                RelativePath = Path.GetRelativePath(path, file),
                FullPath = file,
                Size = new System.IO.FileInfo(file).Length
            };
            result.Add(info);
        }
        
        return result;
    }

    /// <summary>
    /// 清理目标目录中不在更新包中的文件（可选功能）
    /// </summary>
    public async Task CleanOrphanedFilesAsync(string updatePath, string targetPath, string[]? excludePatterns = null)
    {
        if (!Directory.Exists(updatePath) || !Directory.Exists(targetPath))
        {
            return;
        }

        var updateFiles = Directory.GetFiles(updatePath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(updatePath, f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetFiles = Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories);
        
        foreach (var file in targetFiles)
        {
            var relativePath = Path.GetRelativePath(targetPath, file);
            
            if (!updateFiles.Contains(relativePath))
            {
                // 检查是否是排除的文件
                if (excludePatterns != null)
                {
                    var fileName = Path.GetFileName(file);
                    if (excludePatterns.Any(p => fileName.EndsWith(p.Replace("*", ""), StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }
                
                // 不删除核心配置文件
                var fileNameLower = Path.GetFileName(file).ToLower();
                if (fileNameLower == "appsettings.json" || 
                    fileNameLower == "appsettings.development.json")
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // 忽略删除失败的文件
                }
            }
        }
    }
}

/// <summary>
/// 文件信息
/// </summary>
public class FileInfo
{
    public string RelativePath { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long Size { get; set; }
}
