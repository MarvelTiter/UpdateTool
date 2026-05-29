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
    public async Task ReplaceFilesAsync(string updatePath
        , UpdateRequest request
        , string[]? excludeFolder = null)
    {
        var subs = Directory.GetDirectories(updatePath);
        if (subs.Length == 1 && subs[0].EndsWith(request.FileName))
        {
            updatePath = Path.Combine(updatePath, request.FileName);
        }
        var files = Directory.EnumerateFiles(updatePath, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            // 检查是否在排除列表中
            if (excludeFolder?.Length > 0)
            {
                if (excludeFolder.Any(f => IsPathInFolder(file, f)))
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
            if (file.EndsWith(request.AppSettingFile))
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

    public async Task UpdateSettingFile(string newFilePath, string originalFilePath, string settingFile)
    {
        try
        {
            var newJsons = Directory.EnumerateFiles(newFilePath, "*.json", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.EndsWith(settingFile));
            var oldJsons = Directory.EnumerateFiles(originalFilePath, "*.json", SearchOption.AllDirectories).First(f => f.EndsWith(settingFile));
            if (newJsons is null)
                return;
            var jsonOption = new JsonDocumentOptions()
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };
            var newBytes = await File.ReadAllBytesAsync(newJsons);
            using var newStream = new MemoryStream(newBytes);
            var oldBytes = await File.ReadAllBytesAsync(oldJsons);
            using var oldStream = new MemoryStream(oldBytes);
            //using var newStream = File.Open(newJsons, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete);
            //using var oldStream = File.Open(oldJsons, FileMode.Open, FileAccess.ReadWrite);
            var newDoc = JsonNode.Parse(newStream, documentOptions: jsonOption);
            var oldDoc = JsonDocument.Parse(oldStream, jsonOption);
            if (newDoc is null)
            {
                logger.LogInformation("未提供新的配置文件，跳过");
                return;
            }
            //oldDoc.RootElement.
            var updates = new Dictionary<string, JsonElement>();
            CollectValues(oldDoc.RootElement, "", updates);
            UpdateValues(newDoc.Root, updates, logger);
            var json = newDoc.ToJsonString(new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            var newSettingFile = Path.Combine(newFilePath, settingFile);
            var tempFile = newSettingFile + ".tmp";
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, newSettingFile, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新配置文件发生错误:{Message}", ex.Message);
        }

        static void CollectValues(JsonElement element, string path, Dictionary<string, JsonElement> updates)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var childPath = string.IsNullOrEmpty(path)
                            ? property.Name
                            : $"{path}/{property.Name}";

                        if (IsPrimitiveOrArray(property.Value))
                        {
                            updates[childPath] = property.Value;
                        }
                        else
                        {
                            CollectValues(property.Value, childPath, updates);
                        }
                    }
                    break;
            }
        }

        static void UpdateValues(JsonNode root, Dictionary<string, JsonElement> updates, ILogger logger)
        {
            foreach (var kvp in updates)
            {
                var path = kvp.Key;           // 如 "Logging.LogLevel.Default"
                var newValue = kvp.Value;     // 新值（来自旧配置）

                // 按 "." 分割路径
                var segments = path.Split('/');

                // 从 root 开始逐层导航
                if (!GetTargetNode(root, segments, logger, out var target) || !IsTypeMatch(target, newValue))
                {
                    logger.LogWarning("  [跳过] 类型不匹配: {path}", path);
                    continue;
                }
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
                target?.ReplaceWith<JsonElement>(newValue);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
            }

            static bool GetTargetNode(JsonNode root, string[] segments, ILogger logger, out JsonNode? target)
            {
                JsonNode? currentNode = root;
                for (var i = 0; i < segments.Length - 1; i++)
                {
                    var propertyName = segments[i];
                    currentNode = currentNode[propertyName];
                    if (currentNode is null)
                    {
                        logger.LogWarning("  [跳过] 路径中段为 null: {path}", string.Join('.', segments[0..(i + 1)]));
                        break;
                    }
                }
                var final = segments[^1];
                if (currentNode is JsonObject o && o.TryGetPropertyValue(final, out target))
                {
                    return true;
                }
                target = null;
                return false;
            }

            static bool IsTypeMatch(JsonNode? node, JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => node is JsonValue && node.GetValueKind() == JsonValueKind.String,
                    JsonValueKind.Number => node is JsonValue && node.GetValueKind() == JsonValueKind.Number,
                    JsonValueKind.True or JsonValueKind.False => node is JsonValue && (node.GetValueKind() == JsonValueKind.True || node.GetValueKind() == JsonValueKind.False),
                    JsonValueKind.Null => node is null,
                    JsonValueKind.Array => node is JsonArray,
                    _ => false
                };
            }
        }

        static bool IsPrimitiveOrArray(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => true,
                JsonValueKind.Number => true,
                JsonValueKind.True => true,
                JsonValueKind.False => true,
                JsonValueKind.Null => true,
                JsonValueKind.Array => true,
                _ => false
            };
        }
    }

    public void RemoveTempFiles(string tempPath)
    {
        try
        {
            Directory.Delete(tempPath, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清理临时文件发生错误:{Message}", ex.Message);

        }
    }
}
