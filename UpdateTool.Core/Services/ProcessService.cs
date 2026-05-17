namespace UpdateTool.Core.Services;

/// <summary>
/// 进程管理服务 - 停止和启动 .NET 应用进程
/// </summary>
public class ProcessService
{
    /// <summary>
    /// 停止指定进程名称的所有进程
    /// </summary>
    /// <param name="processName">进程名称（不含扩展名）</param>
    /// <returns>停止的进程数量</returns>
    public async Task<int> StopProcessByNameAsync(string? processName)
    {
        var count = 0;
        var processes = System.Diagnostics.Process.GetProcessesByName(processName);

        foreach (var process in processes)
        {
            try
            {
                process.Kill();
                count++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法停止进程 {processName}: {ex.Message}", ex);
            }
        }

        // 等待进程完全退出
        if (count > 0)
        {
            await Task.Delay(1000); // 等待1秒让进程完全退出

            // 验证进程是否已停止
            var remaining = System.Diagnostics.Process.GetProcessesByName(processName);
            if (remaining.Length > 0)
            {
                throw new InvalidOperationException($"进程 {processName} 未能完全停止，仍有 {remaining.Length} 个实例在运行");
            }
        }

        return count;
    }

    /// <summary>
    /// 启动一个应用
    /// </summary>
    /// <param name="executablePath">可执行文件路径</param>
    /// <param name="arguments">启动参数</param>
    /// <param name="workingDirectory">工作目录</param>
    /// <returns>启动的进程</returns>
    public System.Diagnostics.Process StartApplication(string executablePath, string arguments = "", string? workingDirectory = null)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"找不到可执行文件: {executablePath}");
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? "",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = System.Diagnostics.Process.Start(startInfo);

        if (process == null)
        {
            throw new InvalidOperationException("无法启动进程");
        }

        return process;
    }

    /// <summary>
    /// 检查进程是否在运行
    /// </summary>
    /// <param name="processName">进程名称</param>
    /// <returns>是否在运行</returns>
    public bool IsProcessRunning(string processName)
    {
        return System.Diagnostics.Process.GetProcessesByName(processName).Length > 0;
    }
}

/// <summary>
/// 进程信息
/// </summary>
public class ProcessInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime StartTime { get; set; }
}
