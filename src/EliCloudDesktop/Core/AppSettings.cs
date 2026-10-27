using System.Text.Json;
using System.Text.Json.Serialization;

namespace EliCloudDesktop.Core;

/// <summary>
/// 本机记住的东西。落盘位置：<c>%LOCALAPPDATA%\EliCloud\settings.json</c>。
/// </summary>
/// <remarks>
/// 只有两样，而且都不敏感：上次登录用的用户名、界面主题。
/// <b>刻意不保存</b>密码、access token 与 refresh token ——
/// SDK 的密码直连签发的 refresh token 没有会话绑定，标准 <c>/logout</c> 找不到它，
/// 平台也已删掉私有的 <c>/v1/logout</c>，也就是说签出去就撤不回来（SDK README §5.2）。
/// 把它写到磁盘上等于换一种方式长期留存凭据。
/// </remarks>
internal sealed record AppSettings(string? LastUsername, AppTheme Theme)
{
    private const string FolderName = "EliCloud";
    private const string FileName = "settings.json";

    /// <summary>默认：没有上次的用户名，主题跟随 Windows。</summary>
    public static AppSettings Default => new(null, AppTheme.System);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FolderName,
        FileName);

    /// <summary>读取本机配置；文件不存在或损坏时回落到默认值。</summary>
    public static AppSettings Load()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path)) return Default;

            var loaded = JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJsonContext.Default.AppSettings);
            return loaded ?? Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Default;
        }
    }

    /// <summary>写回本机配置；失败时静默（配置写不进去不该让应用崩）。</summary>
    public void Save()
    {
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 忽略：设置写不进去不影响本次会话。
        }
    }
}

/// <summary>源生成 JSON 上下文 —— Release 下会裁剪反射序列化器，必须走源生成。</summary>
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext
{
}
