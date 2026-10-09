using System.Diagnostics;
using VSCodeRecent.VSCode;

namespace VSCodeRecent.MobaXterm;

/// <summary>一个探测到的 MobaXterm 相关位置及其存在情况。</summary>
internal sealed record MobaXtermProbe(string Kind, string Path, bool Exists);

/// <summary>
/// 「MobaXterm 装在哪、<c>MobaXterm.ini</c> 在哪」—— 全仓库只有这一个模块知道。
///
/// <para>配置位置随安装模式变化：便携版把 ini 放在 exe 附近，Scoop 把 ini 放在
/// <c>persist</c> 而 exe 在 <c>apps\...\current</c>，安装版在「文档」目录。
/// 所以必须探测 + 允许覆盖，不能写死。</para>
///
/// <para>单例只缓存文件系统探测结果，不认识设置 —— 覆盖路径由调用方传入
/// （<see cref="IniPaths"/> / <see cref="Executable"/> 的 <c>overridePath</c>）。</para>
/// </summary>
internal sealed class MobaXtermInstall
{
    /// <summary>ini 路径的显式覆盖环境变量（与设置里的覆盖项等价）。</summary>
    public const string IniEnvVar = "VSCODERECENT_MOBAXTERM_INI";

    /// <summary>exe 路径的显式覆盖环境变量。</summary>
    public const string ExeEnvVar = "VSCODERECENT_MOBAXTERM_EXE";

    /// <summary>PATH 上可能的 MobaXterm 可执行文件。顺序即优先级。</summary>
    private static readonly string[] ExecutableNames = ["MobaXterm.exe", "mobaxterm.exe"];

    private static MobaXtermInstall? _shared;
    private static readonly object SharedGate = new();

    public static MobaXtermInstall Shared
    {
        get
        {
            lock (SharedGate)
            {
                return _shared ??= new MobaXtermInstall();
            }
        }
    }

    /// <summary>
    /// 候选 ini 路径，按优先级：设置/环境变量覆盖 → exe 附近（便携）→
    /// Scoop persist → 用户文档。返回全部候选（含不存在的），供诊断展示。
    /// </summary>
    public IEnumerable<string> IniPaths(string? overridePath = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in IniCandidates(overridePath))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>第一个存在的 ini 路径，找不到返回 null。</summary>
    public string? IniPath(string? overridePath = null) =>
        IniPaths(overridePath).FirstOrDefault(File.Exists);

    /// <summary>PATH / 运行进程里解析出的 MobaXterm.exe，找不到返回 null。</summary>
    public string? Executable(string? overridePath = null)
    {
        if (FullIfExists(overridePath) is { } overridden)
        {
            return overridden;
        }

        var fromEnv = FullIfExists(Environment.GetEnvironmentVariable(ExeEnvVar));
        if (fromEnv is not null)
        {
            return fromEnv;
        }

        return OnPath() ?? FromRunningProcess();
    }

    /// <summary>结构化探测结果，读不到 session 时用来排查。</summary>
    public IEnumerable<MobaXtermProbe> Probe(string? iniOverride = null, string? exeOverride = null)
    {
        foreach (var path in IniPaths(iniOverride))
        {
            yield return new MobaXtermProbe("MobaXterm.ini", path, File.Exists(path));
        }

        var exe = Executable(exeOverride);
        yield return new MobaXtermProbe("MobaXterm.exe", exe ?? "(未找到)", exe is not null);
    }

    private IEnumerable<string> IniCandidates(string? overridePath)
    {
        if (FullIfExists(overridePath) is { } overridden)
        {
            yield return overridden;
        }

        if (FullIfExists(Environment.GetEnvironmentVariable(IniEnvVar)) is { } fromEnv)
        {
            yield return fromEnv;
        }

        // 便携版：ini 通常在 exe 同级或上一级
        foreach (var exe in ExecutableCandidates())
        {
            if (Path.GetDirectoryName(exe) is not { Length: > 0 } dir)
            {
                continue;
            }

            yield return Path.Combine(dir, "MobaXterm.ini");
            yield return Path.GetFullPath(Path.Combine(dir, "..", "MobaXterm.ini"));
        }

        // Scoop：exe 在 apps\...\current，ini 在 persist\mobaxterm
        foreach (var scoopRoot in PathEnvironment.ScoopRoots())
        {
            yield return Path.Combine(scoopRoot, "persist", "mobaxterm", "MobaXterm.ini");
        }

        // 安装版：ini 在用户「文档」目录
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (documents.Length > 0)
        {
            yield return Path.Combine(documents, "MobaXterm", "MobaXterm.ini");
            yield return Path.Combine(documents, "MobaXterm.ini");
        }
    }

    /// <summary>PATH 上的 exe 候选（含 Scoop shim 反推出的真实路径）。</summary>
    private IEnumerable<string> ExecutableCandidates()
    {
        if (OnPath() is { } onPath)
        {
            yield return onPath;
        }

        foreach (var root in PathEnvironment.ScoopRoots())
        {
            yield return Path.Combine(root, "apps", "mobaxterm", "current", "MobaXterm.exe");
        }
    }

    private static string? OnPath()
    {
        foreach (var dir in PathEnvironment.Directories())
        {
            foreach (var name in ExecutableNames)
            {
                var exe = Path.Combine(dir, name);
                if (File.Exists(exe))
                {
                    return exe;
                }
            }
        }

        return null;
    }

    /// <summary>正在运行的 MobaXterm 进程的可执行路径 —— 便携版没进 PATH 时的兜底。</summary>
    private static string? FromRunningProcess()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("MobaXterm");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MobaXterm FromRunningProcess error: {ex.Message}");
            return null;
        }

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { Length: > 0 } path)
                    {
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    // 权限 / 位数不匹配，跳过
                    Debug.WriteLine($"MobaXterm MainModule error: {ex.Message}");
                }
            }
        }

        return null;
    }

    /// <summary>非空且规范化成功才返回该路径，否则 null。</summary>
    private static string? FullIfExists(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : VSCodeInstall.TryGetFullPath(path);
}
