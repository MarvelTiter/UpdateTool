using System.Text.Json;

namespace UpdateTool.Core.Services;

/// <summary>
/// AppSettings 配置服务 - 读取和更新 appsettings.json
/// </summary>
public class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 读取 appsettings.json 文件
    /// </summary>
    /// <param name="path">appsettings.json 路径</param>
    /// <returns>配置字典</returns>
    public async Task<Dictionary<string, JsonElement>> ReadAppSettingsAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"配置文件不存在: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        var doc = JsonDocument.Parse(json);
        
        return FlattenJson(doc.RootElement, "");
    }

    /// <summary>
    /// 读取 appsettings.json 并反序列化为强类型对象
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="path">appsettings.json 路径</param>
    /// <returns>配置对象</returns>
    public async Task<T?> ReadAppSettingsAsync<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"配置文件不存在: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>
    /// 更新 appsettings.json 中的特定值
    /// </summary>
    /// <param name="path">appsettings.json 路径</param>
    /// <param name="updates">要更新的键值对（支持嵌套键，如 "ConnectionStrings:Default"）</param>
    public async Task UpdateAppSettingsAsync(string path, Dictionary<string, string?> updates)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"配置文件不存在: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);
        
        // 使用 Utf8JsonReader 和 Utf8JsonWriter 来保留格式
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteJson(writer, doc.RootElement, updates);
        }
        
        var result = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        await File.WriteAllTextAsync(path, result);
    }

    /// <summary>
    /// 合并两个 appsettings.json 文件
    /// </summary>
    /// <param name="basePath">基础配置文件</param>
    /// <param name="overridePath">覆盖配置文件</param>
    /// <param name="outputPath">输出路径</param>
    public async Task MergeAppSettingsAsync(string basePath, string overridePath, string outputPath)
    {
        if (!File.Exists(basePath))
        {
            throw new FileNotFoundException($"基础配置文件不存在: {basePath}");
        }

        if (!File.Exists(overridePath))
        {
            throw new FileNotFoundException($"覆盖配置文件不存在: {overridePath}");
        }

        var baseJson = await File.ReadAllTextAsync(basePath);
        var baseDoc = JsonDocument.Parse(baseJson);
        
        var overrideJson = await File.ReadAllTextAsync(overridePath);
        var overrideDoc = JsonDocument.Parse(overrideJson);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            MergeJson(writer, baseDoc.RootElement, overrideDoc.RootElement);
        }
        
        var result = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        await File.WriteAllTextAsync(outputPath, result);
    }

    /// <summary>
    /// 保留基础配置，只更新指定的键
    /// </summary>
    public async Task PreserveAndUpdateAsync(string path, Dictionary<string, string?> preserveKeys, Dictionary<string, string?> updateValues)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"配置文件不存在: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);
        
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteJsonWithPreserve(writer, doc.RootElement, preserveKeys, updateValues);
        }
        
        var result = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        await File.WriteAllTextAsync(path, result);
    }

    /// <summary>
    /// 获取特定的配置值
    /// </summary>
    public async Task<string?> GetValueAsync(string path, string key)
    {
        var settings = await ReadAppSettingsAsync(path);
        return settings.TryGetValue(key, out var value) ? value.ToString() : null;
    }

    /// <summary>
    /// 设置特定的配置值
    /// </summary>
    public async Task SetValueAsync(string path, string key, string? value)
    {
        await UpdateAppSettingsAsync(path, new Dictionary<string, string?> { { key, value } });
    }

    /// <summary>
    /// 将 JSON 展平为键值对字典
    /// </summary>
    private Dictionary<string, JsonElement> FlattenJson(JsonElement element, string prefix)
    {
        var result = new Dictionary<string, JsonElement>();
        
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
                
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var nested in FlattenJson(property.Value, key))
                    {
                        result[nested.Key] = nested.Value;
                    }
                }
                else
                {
                    result[key] = property.Value;
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// 写入 JSON 并应用更新
    /// </summary>
    private void WriteJson(Utf8JsonWriter writer, JsonElement element, Dictionary<string, string?> updates)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            element.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        
        foreach (var property in element.EnumerateObject())
        {
            var key = property.Name;
            
            // 检查是否有对应的更新值
            var updateKey = updates.Keys.FirstOrDefault(k => k.EndsWith($":{key}") || k == key);
            
            writer.WritePropertyName(key);
            
            if (updateKey != null && updates[updateKey] != null)
            {
                // 直接写入新值
                writer.WriteRawValue(updates[updateKey]!);
            }
            else if (property.Value.ValueKind == JsonValueKind.Object)
            {
                // 递归处理嵌套对象
                WriteJson(writer, property.Value, updates);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }
        
        writer.WriteEndObject();
    }

    /// <summary>
    /// 合并两个 JSON 对象
    /// </summary>
    private void MergeJson(Utf8JsonWriter writer, JsonElement baseElement, JsonElement overrideElement)
    {
        if (baseElement.ValueKind != JsonValueKind.Object || overrideElement.ValueKind != JsonValueKind.Object)
        {
            overrideElement.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        
        // 先写入基础配置
        foreach (var property in baseElement.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            
            if (overrideElement.TryGetProperty(property.Name, out var overrideValue))
            {
                if (property.Value.ValueKind == JsonValueKind.Object && overrideValue.ValueKind == JsonValueKind.Object)
                {
                    // 递归合并
                    MergeJson(writer, property.Value, overrideValue);
                }
                else
                {
                    // 用覆盖值替换
                    overrideValue.WriteTo(writer);
                }
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }
        
        // 添加覆盖配置中独有的属性
        foreach (var property in overrideElement.EnumerateObject())
        {
            if (!baseElement.TryGetProperty(property.Name, out _))
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
        }
        
        writer.WriteEndObject();
    }

    /// <summary>
    /// 保留特定键的同时更新其他值
    /// </summary>
    private void WriteJsonWithPreserve(Utf8JsonWriter writer, JsonElement element, 
        Dictionary<string, string?> preserveKeys, Dictionary<string, string?> updateValues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            element.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        
        foreach (var property in element.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            
            // 检查是否应该保留
            var preserveKey = preserveKeys.Keys.FirstOrDefault(k => k.EndsWith($":{property.Name}") || k == property.Name);
            
            if (preserveKey != null)
            {
                // 保留原始值
                property.Value.WriteTo(writer);
            }
            else if (property.Value.ValueKind == JsonValueKind.Object)
            {
                WriteJsonWithPreserve(writer, property.Value, preserveKeys, updateValues);
            }
            else
            {
                // 检查是否有对应的更新值
                var updateKey = updateValues.Keys.FirstOrDefault(k => k.EndsWith($":{property.Name}") || k == property.Name);
                if (updateKey != null && updateValues[updateKey] != null)
                {
                    writer.WriteRawValue(updateValues[updateKey]!);
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }
        }
        
        writer.WriteEndObject();
    }
}
