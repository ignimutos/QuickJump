using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VSCodeRecent;

/// <summary>
/// 扩展设置。持久化到 %LOCALAPPDATA%\VSCodeRecent\settings.json。
/// </summary>
internal sealed class VSCodeRecentSettings : JsonSettingsManager
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VSCodeRecent");

    /// <summary>默认不显示文件 —— 最近记录里绝大多数是单独打开的文件，会淹没项目。</summary>
    public ToggleSetting ShowFiles { get; } = new(
        "showFiles",
        "显示文件",
        "在列表中显示最近单独打开的文件",
        false);

    /// <summary>
    /// 打开 VSCode / MobaXterm 后是否把窗口切到前台。默认开 —— 由后台进程启动的程序
    /// 默认开在背后，用户往往以为「没反应」。
    /// </summary>
    public ToggleSetting ActivateOnOpen { get; } = new(
        "activateOnOpen",
        "打开后置于前台",
        "打开 VSCode / MobaXterm 后把窗口切到前台；关闭则留在后台",
        true);

    /// <summary>MobaXterm.ini 的显式覆盖路径。留空则自动探测（便携版 / Scoop / 文档目录）。</summary>
    public TextSetting MobaXtermIniPath { get; } = new(
        "mobaXtermIniPath",
        "MobaXterm.ini 路径",
        "留空则自动探测（便携版 / Scoop / 文档目录）",
        string.Empty);

    /// <summary>MobaXterm.exe 的显式覆盖路径。留空则从 PATH 与运行中的进程探测。</summary>
    public TextSetting MobaXtermExePath { get; } = new(
        "mobaXtermExePath",
        "MobaXterm.exe 路径",
        "留空则自动探测",
        string.Empty);

    /// <summary>
    /// MobaXterm 列表里是否显示目录行。默认显示 —— 目录多时结构清楚，点目录还能筛出该目录下的会话。
    /// 关掉则只列 session。
    /// </summary>
    public ToggleSetting MobaXtermShowFolders { get; } = new(
        "mobaXtermShowFolders",
        "显示目录",
        "在 MobaXterm session 列表里显示目录行（点击可筛选该目录）",
        true);

    public VSCodeRecentSettings()
    {
        Directory.CreateDirectory(SettingsDir);
        FilePath = Path.Combine(SettingsDir, "settings.json");
        Settings.Add(ShowFiles);
        Settings.Add(MobaXtermIniPath);
        Settings.Add(MobaXtermExePath);
        Settings.Add(MobaXtermShowFolders);
        Settings.Add(ActivateOnOpen);

        // JsonSettingsManager 本身不监听 SettingsChanged（只有 LoadSettings/SaveSettings
        // 两个公开方法），不自己接这一步的话，点 Save 只改内存，重启就丢。
        Settings.SettingsChanged += (_, _) => SaveSettings();

        LoadSettings();
    }
}
