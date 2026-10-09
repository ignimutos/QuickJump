# QuickJump

[![Build](https://github.com/ignimutos/QuickJump/actions/workflows/build.yml/badge.svg)](https://github.com/ignimutos/QuickJump/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> 切换语言：[English](./README_en.md)

PowerToys Command Palette 扩展 —— 快速跳转到 VSCode 最近项目与 MobaXterm 会话。
一个扩展、两个模块（VSCode / MobaXterm），各有独立的顶层命令。

## 功能

- 自动显示 VSCode 最近打开的文件夹与工作区
- 列表按最后打开时间倒序
- 支持 WSL 远程项目（自动还原成 `vscode-remote://wsl+` 打开）
- 回车一键在 VSCode 中打开
- 设置项「显示文件」可切换是否把单独打开的文件也列出来（默认关）；打开后列表分成
  **项目 (N)** / **文件 (N)** 两组（标题带条数），与 VSCode 自己的最近列表一致 ——
  这两段之间没有可比较的时间戳（VSCode 只存顺序不存时间），所以不揉成一条时间线。
  CmdPal 的列表页只能单列滚动，做不到左右分栏；文件太多时直接打字筛选，比滚更快
- 另一个顶层命令 **MobaXterm**：搜索本机 MobaXterm 的 session（含目录层级），
  回车用 `MobaXterm.exe -bookmark` 打开并自动登入该 session

## 安装

1. 到 [Releases](https://github.com/ignimutos/QuickJump/releases) 下载对应架构的 `.msix`（`x64` 或 `ARM64`）
2. 开启开发者模式：设置 → 系统 → 开发者选项 → 开发人员模式
3. **用管理员身份**打开 PowerShell，进到 `.msix` 所在目录
4. 安装：

```powershell
Add-AppxPackage -Path .\QuickJump-x64.msix -AllowUnsigned
```

5. 打开 Command Palette，运行 `Reload Command Palette Extension`

> 两个前提，缺一不可：
> - **开发者模式**：未签名包要求系统允许旁加载。
> - **管理员权限**：包里含可执行文件，未签名包只能装给「所有用户」，这需要提权。
>   不开管理员会报 `0x80073D2C` 或权限错误。
>
> 覆盖安装旧版本时加 `-ForceUpdateFromAnyVersion`，否则版本号相同/更低会被挡下。

## 使用

1. `Win + Alt + Space` 打开 Command Palette（默认快捷键，可在 Command Palette 设置里改）
2. **滚到根列表最末尾** —— 扩展的顶层命令排在系统命令之后；若开了紧凑模式，先按 `↓` 或 `Tab` 展开列表
3. 选择 **VSCode** 回车进入列表页
4. 选中项目回车，在 VSCode 中打开

更快的一条路：在根搜索框直接敲项目名（至少 2 个字符）。扩展会以回退项的形式出现在结果里 ——

- **只有一条命中**：回车**直接打开**该项目，不经过列表页
- **多条命中**：回车进入列表页，且搜索框已填好你敲的词，继续改即可

> 回退项只在搜索框有输入时出现（宿主规定：`Title` 为空的项不进根列表，而回退项平时必须
> 是空的才不会白占一行），所以不能「空着搜索框就看到项目」。

### MobaXterm

顶层命令 **MobaXterm** 列出本机 `MobaXterm.ini` 里的全部 session：

- **每行**：标题是 session 名，副标题带目录、`user@host:port`，图标按协议区分
  （SSH / WSL / 其它）。回车用 `MobaXterm.exe -bookmark "User sessions\<目录>\<名字>"`
  打开，MobaXterm 会据此自动登入。
- **目录行**（默认显示）：每个出现过的目录占一行，图标是文件夹；**点它进入该目录的会话子页**，
  只列该目录下的会话（不启动任何东西）。子页里用命令面板自带的 🔙 返回上一级；若宿主设置
  `外观 → 搜索为空时用 Backspace 返回` 打开，清空搜索框后按 Backspace 也能返回。
  可在设置里关掉目录行，只列 session。
- **搜索**：按名字 / 目录 / host 子串筛选。打目录名（如 `remote`）即筛出该目录下的全部会话，
  无论目录行开不开都有效。

> 和 VSCode 一样，这是**顶层命令**。想让它更快，可在 Command Palette 设置里给它
> 绑一个别名（Aliases）或热键 —— 例如像 VSCode 那样绑成单个字符。别名/热键是
> 宿主的设置项，扩展自己不能声明快捷键。

## 设置

在 Command Palette 的扩展设置里可配：

- **显示文件**（默认关）—— VSCode 的最近记录里绝大多数是单独打开的文件，默认过滤掉，
  只留文件夹和工作区。
- **打开后置于前台**（默认开）—— 打开 VSCode / MobaXterm 后把窗口切到前台。由后台进程
  启动的程序默认开在背后，关掉此项可让它们留在后台。
- **MobaXterm.ini 路径** / **MobaXterm.exe 路径**（默认空）—— 留空则自动探测（便携版 /
  Scoop / 文档目录 / PATH）；探测不到时在此显式指定。
- **显示目录**（默认开）—— MobaXterm 列表里是否显示目录行。关掉则只列 session。
  目录行本身随时可搜（打目录名即筛出该目录下的会话），此开关只控制它是否单独占一行。

设置存在 `%LOCALAPPDATA%\QuickJump\settings.json`。

## 系统要求

- Windows 11（10.0.19041.0+）
- Command Palette 0.12+（已是独立 Store 应用，不再随 PowerToys 分发）
- VSCode 已安装且 `code` 命令可用

## VSCode 数据位置

按以下优先级自动查找，逐级回退：

1. **共享存储**（VSCode 1.75+ 默认）
   - `%USERPROFILE%\.vscode-shared\sharedStorage\state.vscdb`
2. **SQLite 数据库**
   - `%APPDATA%\Code\User\globalStorage\*\state.vscdb`
3. **传统 JSON 格式**（兼容旧版本）
   - `%APPDATA%\Code\User\globalStorage\storage.json`

## MobaXterm 数据位置

session 全部是 `MobaXterm.ini` 里 `[Bookmarks]` / `[Bookmarks_N]` 段的明文，按以下优先级
查找（第一条存在的即用；设置里的路径覆盖优先于全部）：

1. **exe 附近**（便携版）—— `<exe 目录>\MobaXterm.ini` 或上一级
2. **Scoop** —— `<scoop root>\persist\mobaxterm\MobaXterm.ini`
3. **文档目录**（安装版）—— `%USERPROFILE%\Documents\MobaXterm\MobaXterm.ini`

`MobaXterm.exe` 的查找顺序：设置覆盖 → PATH → Scoop `apps\mobaxterm\current` →
运行中的 MobaXterm 进程。

> session 的目录层级来自 `SubRep`，`-bookmark` 路径拼成 `User sessions\<目录>\<名字>`。
> 密码存在单独的 `[Passwords]` / `[Sesspass]` 里（加密），本扩展不读取也不搬运。

## 开发

需要 .NET 10 SDK。项目目标框架为 `net10.0-windows10.0.26100.0`。

**推荐用一键脚本**（停止进程 → 打包 → 安装 → 重启 Command Palette）：

```powershell
.\dev.cmd                    # Debug + x64，完整流程
.\dev.cmd -Configuration Release
.\dev.cmd -Platform ARM64
.\dev.cmd -NoRestart          # 不重启 Command Palette
.\dev.cmd -Stop               # 只停掉扩展和 Command Palette
.\dev.cmd -Uninstall          # 卸载扩展
```

> 如果在 WSL 的 UNC 路径下直接跑 `.\dev.ps1` 报"未进行数字签名"，用 `dev.cmd` 入口 ——
> 它对单条命令使用 `-ExecutionPolicy Bypass`，不改系统执行策略。
> 也可以在 **Windows 本地磁盘**（而非 `\\wsl.localhost\...`）上跑 `.\dev.ps1`。

手动构建：

```powershell
dotnet restore -p:Platform=x64
dotnet build -c Debug -p:Platform=x64
```

产物：`AppPackages\QuickJump_1.0.0.0_x64_Debug_Test\QuickJump_1.0.0.0_x64_Debug.msix`

如果 `dotnet build` 没有生成 `.msix`，改用官方 recipe 手动 publish：

```powershell
dotnet publish -c Debug -p:Platform=x64 `
  -p:WindowsPackageType=MSIX `
  -p:AppxPackageDir="$PWD\AppPackages\" `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxBundle=Never
```

安装并测试：

```powershell
Add-AppxPackage -Path .\AppPackages\QuickJump_1.0.0.0_x64_Debug_Test\QuickJump_1.0.0.0_x64_Debug.msix -AllowUnsigned
```

> 未签名安装需要 `Package.appxmanifest` 的 `Publisher` 里带上 Windows 保留的
> `OID.2.25.311729368913984317654407730594956997722=1`（见
> [MS 文档](https://learn.microsoft.com/en-us/windows/msix/package/unsigned-package)），
> 仓库里已经配好了。真正允许未签名部署的是 `-AllowUnsigned` 参数本身。

改完代码后重新安装，并在 Command Palette 里运行一次 `Reload Command Palette Extension` —— 否则面板不会重新加载扩展。

卸载：

```powershell
Get-AppxPackage -Name "QuickJump" | Remove-AppxPackage
```

## 发布

版本号的唯一来源是 `QuickJump.csproj` 的 `<Version>`。改版本号并 push 到 `main`，
CI 自动打 `v<版本>` tag 并发 Release；版本没变时只构建，不重复发版：

```xml
<!-- QuickJump.csproj -->
<Version>1.1.0</Version>
```

`.github/workflows/build.yml` 会：

1. 从 csproj（或手工推的 tag）解析版本，覆写 `Package.appxmanifest` 的 `Version`（`1.2.3` → `1.2.3.0`）
2. 在 x64 与 arm64 两个原生 runner 上各跑测试并构建 `x64` / `ARM64` 两个 `.msix`
3. 该版本没对应 tag 时，建 Release 一并创建 `v1.2.3` 并上传 `QuickJump-x64.msix` / `QuickJump-ARM64.msix`

tag 由 Release 步骤创建（发布时间也是这一步），所以某次构建失败不会留下 tag 挡住下次重试；GITHUB_TOKEN 建的 tag 不会再触发 workflow，不会递归。

手工 `git tag v1.2.3 && git push origin v1.2.3` 的旧路径仍可用；此时版本以 tag 为准，tag 号须与 `QuickJump.csproj` 的 `<Version>` 一致。

### 发布到 Microsoft Store

`.github/workflows/store.yml` 在 Release 发布后调用
[MSStore CLI](https://github.com/microsoft/msstore-cli) 提交。Store 会**用自己的证书
重新签名**，所以不需要自签证书。

工作流做的事：下载 Release 里的 `.msix` → 用 `makeappx` 合成 `.msixbundle`
→ `msstore publish`（上传 → 提交 → 轮询 → 发布）。

> Store 一次只接受一个包文件，所以要合成 bundle；否则第二次 `publish` 会把第一次
> 创建的草稿提交删掉。

**这个工作流是可选的**：下面的 secret 没配齐时，job 第一步就检测出来并跳过，
显示为绿色成功，不会失败、也不会影响 Release 分发。想彻底去掉就直接删掉
`.github/workflows/store.yml`。

**前置步骤（一次性，全部手工）：**

1. 注册 [Partner Center](https://partner.microsoft.com/dashboard) 开发者账号（个人约 $19 一次性）
2. 在 Partner Center 预留应用名，把拿到的 `Package/Identity/Name`、`Publisher`、`PublisherDisplayName`
   填进 `Package.appxmanifest` —— 必须与 Partner Center **逐字符一致**（含大小写）。
   此时同步删掉 `Publisher` 里的未签名 OID
3. 建 Azure AD 租户并关联 Partner Center，注册一个 Azure AD 应用、授予 **Manager** 角色
4. 在 Partner Center 手工提交一次（年龄分级问卷等）。API 无法创建**第一个**提交，
   只能用带列表信息的那次已有提交续写
5. 加两个 GitHub secret：
   - `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` / `SELLER_ID`（Partner Center 账号设置里的 Seller ID）
   - `PRODUCT_ID`（Partner Center 里的应用 ID，即 `AppId`）

之后每次发 Release 自动提交。也可在 Actions 页面手动 `workflow_dispatch` 触发。

### 代码签名

走 Store 不需要自签（Store 重新签名）。仅当将来要绕过 Store 自行分发签名的 MSIX 才需要，
届时同步改两处：

1. `Package.appxmanifest` 的 `Publisher` 改成证书的 CN，删掉 `OID.2.25.311729368913984317654407730594956997722=1`
2. `QuickJump.csproj` 的 `<AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>` 改为 `true`，
   并给 `PackageCertificateKeyFile` / `PackageCertificateThumbprint`

## 项目结构

```
.
├── QuickJump.csproj              # 项目文件
├── Directory.Packages.props         # 集中式包版本管理
├── global.json                      # SDK 版本固定
├── Package.appxmanifest             # MSIX 清单（COM 服务器 + Command Palette 扩展注册）
├── app.manifest                     # 应用清单（DPI 感知）
├── src/
│   ├── Program.cs                   # 入口点，COM 服务器宿主
│   ├── QuickJumpExtension.cs     # IExtension 实现（COM 激活入口）
│   ├── QuickJumpCommandsProvider.cs # 命令提供者（继承 Toolkit 的 CommandProvider）—— 各模块入口都在这
│   ├── QuickJumpSettings.cs      # 扩展设置（JsonSettingsManager）
│   ├── WindowActivation.cs          # 把外部程序窗口切到前台（最小化+还原）
│   ├── VSCode/                      # VSCode 模块的全部代码
│   │   ├── VSCodeInstall.cs         # VSCode 装在哪：位置探测（标准/便携/Scoop）
│   │   ├── VSCodeHistory.cs   # 读取最近记录：枚举数据源、去重排序、缓存
│   │   ├── VSCodeHistorySource.cs   # 单个数据源 + JSON 解析规则（ParseHistoryKey）
│   │   ├── VSCodeUri.cs             # VSCode URI → 打开目标（只在这里解一次码）
│   │   ├── VSCodeFallbackItem.cs  # 根搜索内联命中项
│   │   ├── OpenInVSCodeCommand.cs   # 在 VSCode 中打开（只负责启动）
│   │   ├── VSCodeListPage.cs  # 最近项目列表页
│   │   ├── VSCodeItem.cs            # 一条最近记录
│   │   ├── ItemKind.cs              # 类型：显示名/是否算项目/并列兜底顺序
│   │   ├── ItemGroups.cs            # 列表页分组切割（项目 / 文件）
│   │   ├── VSCodeOpenTarget.cs      # 打开目标：本地 / WSL，含命令行与显示路径
│   │   ├── MaterialIconTheme.cs     # 按文件名查彩色图标（关联表内嵌）
│   │   └── VSCodeIcons.cs           # 模块应用图标路径常量
│   └── MobaXterm/                   # MobaXterm 模块的全部代码
│       ├── MobaXtermInstall.cs      # MobaXterm 装在哪：ini/exe 位置探测
│       ├── MobaXtermSessions.cs     # 读取 session：缓存 + 快照 + 后台刷新
│       ├── MobaXtermIni.cs          # MobaXterm.ini 的 [Bookmarks*] 解析（纯函数）
│       ├── MobaXtermSession.cs      # 一个 MobaXterm session（含 -bookmark 路径）
│       ├── OpenInMobaXtermCommand.cs  # 用 MobaXterm 打开 session（只负责启动）
│       ├── MobaXtermSessionListPage.cs  # MobaXterm session 列表页
│       ├── MobaXtermFolderPage.cs   # 单个目录的会话子页（目录行点进去）
│       ├── MobaXtermSessionRow.cs   # session 行的构造（两页共用）
│       └── MobaXtermIcons.cs        # 按协议选行图标
├── tests/QuickJump.Tests/        # 纯逻辑测试（xUnit），不需要 VSCode
└── Assets/                          # MSIX 图标资源
    ├── MaterialIcons/               # 按文件名关联的彩色图标（MIT，见其 NOTICE.md）
    ├── MobaIcons/                   # MobaXterm 会话类型图标（Tabler，MIT，见其 NOTICE.md）
    └── VSCodeIcons/                 # VSCode 模块应用图标（AI 生成，见其 NOTICE.md）
```

### 测试

```powershell
dotnet test tests\QuickJump.Tests\QuickJump.Tests.csproj -p:Platform=x64
```

覆盖 URI 解析、JSON 解析、过滤、排序与分组 —— 这些都不碰文件系统，所以不需要装 VSCode 或
安装扩展。文件系统相关的部分（位置探测、读库）仍只能手动验证。

CI（`.github/workflows/build.yml`）会在两个平台上各跑一遍：`x64` 用 `windows-latest`，
`ARM64` 用 `windows-11-arm`。ARM64 **必须**跑在 arm64 宿主上 —— 测试程序集按
`Platform` 编成 `win-arm64`，x64 runner 上装不出对应的 dotnet 测试宿主。

### 实现要点

- 提供者**必须继承** `Microsoft.CommandPalette.Extensions.Toolkit.CommandProvider`，不要手写 `ICommandProvider` 接口 —— 基类负责实现全部胶水成员，子类只需 `override TopLevelCommands()`。
- `QuickJumpExtension.cs` 的 `[Guid]` 必须与 `Package.appxmanifest` 里的 COM `Class Id` 一致。
- 这是 WinRT/COM 进程外扩展，**不能用 PowerToys Run 的 `plugin.json` 方式加载**。
- URI 解码**只用 `Uri.UnescapeDataString`，不要用 `HttpUtility.UrlDecode`** —— 后者是表单编码语义，会把路径里的 `+` 当空格吃掉。
- PATH 上的 `code` 命令枚举**只有 `VSCodeInstall.CodeExecutableNames` 一份**，启动与便携版探测共用，避免两处清单漂移。
- WSL 的 distro 名在生成命令行时转小写，而路径段保持原样；两者混写会打不开（见 `VSCodeOpenTarget` 注释）。
- 列表页**必须继承 `DynamicListPage`**，不能是 `ListPage`：普通 ListPage 由宿主做前缀模糊匹配，
  页面拿不到输入框内容。`SearchText` 是**唯一的查询来源** —— 宿主只在页面刷新时读一次它
  （`ListViewModel` 里 `SearchText = model.SearchText`），之后每次输入走
  `UpdateSearchText`。另存一份关键词并优先用它，会让「进页面后清空搜索框，列表却还是旧结果」。
- 分组**不要自己插 `Separator`**：宿主按 `IListItem.Section` 认分组，且要求 `Command` 为空
  才算 section header。Toolkit 的 `Section` 构造时就自动插好了，直接用即可。
- MobaXterm 数据源**不套 `IVSCodeHistorySource`**：那套接口的 `SourceRead` 承载 `VSCodeItem`
  （含 MRU Order、去重、WSL 目标），而 session 是配置文件里的树，语义不同。`MobaXtermSessions`
  只复用了刷新/缓存的**骨架**，没复用它的类型。
- MobaXterm 的 session 路径**只依赖 `SubRep` 的字符串内容**还原层级，`[Bookmarks_N]` 的编号
  不参与深度计算（不假设编号连续）。`__PTVIRG__` 是值里 `;` 的转义。
- 启动 MobaXterm **直接 `Process.Start` exe，不套 `cmd.exe`**：`OpenInVSCodeCommand` 包一层
  cmd 是因为 PATH 上的 `code` 是 `.cmd` 批处理；`MobaXterm.exe` 是真正的 GUI 程序，套 cmd
  反而让 `%` 被展开、并多一层窗口风险。
- 打开 session **只用 `-bookmark`，不要加 `-newtab`**。实测：`-bookmark` 单独用时，
  MobaXterm 已在运行则复用它、在窗口里开新标签，未运行则自行启动。而 `-newtab`
  的语义是「在新标签里执行后跟的命令」（文档 `-newtab ["<Command>"]`），把
  `-bookmark ...` 拼在它后面会被当成一条 shell 命令去执行 —— 表现为终端里跑一堆
  `set -o` 然后 `/bin/bash: -c: option requires an argument`、会话即关。
- 参数里的路径**必须整体带引号**（`-bookmark "User sessions\ld\gateway"`）：名字含空格，
  不加引号会被按空格切开，报 `no bookmark folder "User"`。
- **置前要用「最小化 + 还原」，不能只 `SetForegroundWindow`**。扩展是进程外 COM 服务器，
  启动时不在前台，Windows 的前台锁定会让 `SetForegroundWindow` 直接失败（实测返回 false），
  `SwitchToThisWindow` 同样无效。先 `ShowWindow(SW_MINIMIZE)` + `ShowWindow(SW_RESTORE)`
  让窗口重新获得置前资格，再 `SetForegroundWindow` 才成立 —— 见 `WindowActivation.cs`。
  启动前先取窗口快照，只认「新出现的窗口」，避免聚焦到无关窗口。

## 许可证

[MIT](LICENSE)
