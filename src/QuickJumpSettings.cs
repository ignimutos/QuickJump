using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump;

/// <summary>
/// 扩展设置。持久化到 %LOCALAPPDATA%\QuickJump\settings.json。
/// </summary>
internal sealed class QuickJumpSettings : JsonSettingsManager
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickJump");

    /// <summary>
    /// 少于这个长度的查询不筛选, 列表按浏览视图列出全部. 默认 2 -- 单字符几乎必然命中
    /// 一大批, 让列表随每次按键整体抖动. 想单字符就筛, 切到 1.
    /// </summary>
    public ChoiceSetSetting MinQueryLength { get; } = new(
        "minQueryLength",
        "最少搜索字符数",
        "少于这个长度时不筛选, 按浏览视图列出全部; 同样适用于根搜索框的回退项 (1 = 单字符即筛选)",
        [new ChoiceSetSetting.Choice("2 个字符", "2"), new ChoiceSetSetting.Choice("1 个字符", "1")]);

    /// <summary>设置值解析成门槛 (只认 1 / 2, 其余取默认).</summary>
    public int MinQueryLengthValue => QueryGate.ResolveMinQueryLength(MinQueryLength.Value);

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
    /// MobaXterm 列表里是否显示目录行。默认显示 —— 目录多时结构清楚，点目录进入该目录的会话子页。
    /// 关掉则只列 session。
    /// </summary>
    public ToggleSetting MobaXtermShowFolders { get; } = new(
        "mobaXtermShowFolders",
        "显示目录",
        "在 MobaXterm session 列表里显示目录行（点击进入该目录的会话）",
        true);

    public QuickJumpSettings()
    {
        Directory.CreateDirectory(SettingsDir);
        FilePath = Path.Combine(SettingsDir, "settings.json");
        Settings.Add(ShowFiles);
        Settings.Add(MinQueryLength);
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
