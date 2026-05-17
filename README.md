# 应用更新工具 (UpdateTool)

一个用于自托管 .NET 应用的更新工具，支持停止进程、替换文件、更新配置文件。

## 功能特性

- **进程管理**: 通过进程名称或端口号停止目标应用
- **文件替换**: 自动备份并替换目标目录中的文件
- **配置更新**: 支持更新 appsettings.json 中的配置项
- **回滚支持**: 保留备份，支持回滚操作
- **预览功能**: 更新前预览即将替换的文件列表

## 使用方法

### 1. 启动应用

```bash
cd UpdateTool.Web
dotnet run
```

### 2. Web UI 操作

访问 `http://localhost:5000`，填写以下信息：

1. **更新包路径**: 包含新版本文件的目录
2. **目标应用路径**: 要更新的应用所在目录
3. **进程设置**: 选择通过进程名称或端口停止应用
4. **文件排除**: 设置要排除的文件类型（*.pdb, *.xml 等）
5. **配置更新**: 添加需要更新的 appsettings.json 配置项
6. **启动选项**: 更新完成后是否自动启动应用

### 3. API 使用示例

```csharp
using UpdateTool.Core.Services;

var service = new UpdateService();

service.ProgressChanged += (s, e) => 
{
    Console.WriteLine($"进度: {e.Percentage}% - {e.Message}");
};

var request = new UpdateRequest
{
    UpdatePath = @"D:\Updates\v1.0.0",
    TargetPath = @"D:\MyApp",
    ProcessName = "MyApp",
    StopProcess = true,
    CreateBackup = true,
    AppSettingsUpdates = new Dictionary<string, string?>
    {
        { "ConnectionStrings:Default", "\"Server=myserver;Database=mydb;\"" }
    }
};

var result = await service.ExecuteUpdateAsync(request);

if (result.Success)
{
    Console.WriteLine("更新成功！");
}
```

## 项目结构

```
UpdateTool/
├── UpdateTool.Core/           # 核心业务逻辑
│   └── Services/
│       ├── ProcessService.cs      # 进程管理
│       ├── FileUpdateService.cs   # 文件替换
│       ├── AppSettingsService.cs  # 配置更新
│       └── UpdateService.cs       # 整合服务
└── UpdateTool.Web/            # Blazor Web UI
    └── Components/Pages/
        └── Home.razor        # 更新界面
```

## 配置项更新说明

支持嵌套键的 JSON 配置更新：

| 键格式 | 说明 |
|--------|------|
| `Key` | 根级键 |
| `Section:Key` | 嵌套键 |
| `Section:SubSection:Key` | 深层嵌套 |

保留配置的示例：

```csharp
var request = new UpdateRequest
{
    // 要更新的值
    AppSettingsUpdates = new Dictionary<string, string?>
    {
        { "ConnectionStrings:Default", "\"Server=newserver;...\"" }
    },
    // 要保留的值（不会被新包覆盖）
    PreserveAppSettings = new Dictionary<string, string?>
    {
        { "Logging:LogLevel:Default", "" }
    }
};
```

## 许可

MIT License