using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickJump.MobaXterm;

/// <summary>
/// 目录行被选中时，把筛选框填成该目录名 —— 页面据「查询词恰为目录名」只列该目录的
/// session。留在本页（<see cref="CommandResult.KeepOpen"/>），不启动任何东西。
/// </summary>
internal sealed partial class FilterToFolderCommand : InvokableCommand
{
    private readonly MobaXtermSessionListPage _page;
    private readonly string _folder;

    public FilterToFolderCommand(MobaXtermSessionListPage page, string folder)
    {
        _page = page;
        _folder = folder;
    }

    public override ICommandResult Invoke()
    {
        _page.SetFixedQuery(_folder);
        return CommandResult.KeepOpen();
    }
}
