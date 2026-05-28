using AutoInjectGenerator;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UpdateTool.Core.Services;

[AutoInjectSelf]
public class ProcessService(ILogger<ProcessService> logger)
{
    /// <summary>
    /// 停止指定进程名称的所有进程
    /// </summary>
    /// <param name="processName">进程名称（不含扩展名）</param>
    /// <returns>停止的进程数量</returns>
    public async Task<int> StopProcessAsync(string? processName, int? port = null)
    {

        int count = 0;

        var processes = port.HasValue ? GetProcessesByPort(port.Value) : GetProcessesByName(processName);

        foreach (var process in processes)
        {
            try
            {
                process.Kill();
                count++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "停止进程发生错误:{Message}", ex.Message);
            }
        }

        // 等待进程完全退出
        if (count > 0)
        {
            await Task.Delay(1000);
        }
        return count;

        static Process[] GetProcessesByName(string? name) => Process.GetProcessesByName(name);
        static Process[] GetProcessesByPort(int port)
        {
            // netstat -ano | findstr :端口
            var netstat = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            netstat.Start();
            string output = netstat.StandardOutput.ReadToEnd();
            netstat.WaitForExit();

            string pattern = $@"TCP\s+[^\s]+:{port}\s+[^\s]+:\s+\S+\s+(\d+)";
            Match match = Regex.Match(output, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) return [];

            string pid = match.Groups[1].Value;

            // 通过PID查进程名
            var process = Process.GetProcessById(int.Parse(pid));
            return [process];
        }
    }

    /// <summary>
    /// 启动一个应用
    /// </summary>
    /// <param name="executablePath">可执行文件路径</param>
    /// <param name="arguments">启动参数</param>
    /// <param name="workingDirectory">工作目录</param>
    /// <returns>启动的进程</returns>
    public Process? StartApplication(string executablePath, string arguments = "", string? workingDirectory = null)
    {
        try
        {
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException($"找不到可执行文件: {executablePath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动进程");
            return process;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "启动进程发生错误:{Message}", ex.Message);
            return null;
        }
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
