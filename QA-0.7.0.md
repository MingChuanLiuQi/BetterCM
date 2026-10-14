# BetterCM 0.7.0 验证记录

开发与主要验证：2026-10-12。中断后完成最终输入检查、环境恢复及交付记录：2026-10-13（Asia/Shanghai）。

## 验证对象

- 正式包：BetterCM-0.7.0-Setup-x64.exe，61,132,219 字节，文件版本 0.7.0.0。
- SHA256：7B6EECE8CCC4EF33B233A0DA6BEE2A31A88D1E4BD63113414B5CFB3C073FEA1C。
- 最终发布目录 artifacts/publish/win-x64，打包流程运行 Build-Installer.ps1 → verify.ps1。
- 本机 Windows 11 x64，系统 build 26300、实机菜单 150% DPI；未覆盖 ARM64/多硬件矩阵。

## 自动化结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Release 构建及自带运行时发布，警告视为错误 | 0 警告、0 错误 | Build-Installer.ps1 / verify.ps1 输出 |
| xUnit 逻辑与映射 | 101/101 | artifacts/qa/headless/headless-tests.trx |
| 最终发布版 WPF / Shell / 设置 | 91/91 | artifacts/qa/published-tests.json |
| 隔离安装后的自检 | 91/91 | artifacts/qa/installed-tests.json |
| IPC、宿主及捕获超时压力 | 58/58 | artifacts/qa/published-stress-0.7.0.json |
| 隔离安装与卸载 | 13/13 | artifacts/qa/installer-smoke.json |
| 最终发布版真实外部输入 / 前台切换 | 2/2 | artifacts/qa/input-0.7.0.json |

发布/安装自检实际在独立 Shell 宿主中复制和剪切临时文件，再向目标文件夹背景粘贴，验证文件内容、复制保留源文件、移动移除源文件。剪切后等待 900 ms 再粘贴，覆盖剪贴板生产宿主结束后的使用。文件名包含中文、空格及 &。测试保存已读取的剪贴板格式并在结束后恢复；不把延迟 OleGetClipboard 代理写回剪贴板。

回收站自检读取真实命名空间 IContextMenu，确认 empty 规范命令及系统 Enabled 状态存在。本机返回“清空​​回收站”、Command=30、Verb=empty、Enabled=true。命令编号只是该次会话快照，生产代码没有硬编码该编号。原始证据保存在 fixtures/transfer-*/recycle-menu.json。

新增映射测试验证禁用命令不会被界面启用、缺失规范动词时不按本地化标题猜测、第三方分组同名命令不误绑定、全项目叶子完整保留、旧配置默认值及 CompactMenu 保存。UI 检查验证小尺寸模式无搜索/更多/图标栏、所有嵌套叶子直接呈现、实际宽高及内容覆盖、还原设置恢复 CompactMenu。保留此前的焦点、材质、动画、窗口尺寸和陈旧响应保护回归。

压力测试为受控 IPC/模拟慢宿主：第二次 open 34.13 ms，50 次 open 的 p50=0.25 ms、最大 0.54 ms；卡住捕获 1802.22 ms 返回，后续捕获 210.20 ms。40 秒命令及 30 秒通信超时不会杀掉执行宿主。这些数字不是实体鼠标连击或任意第三方扩展的性能保证。

## 实际界面观察

使用 computer-use 在专用 artifacts/qa/live-070 目录操作临时文件，通过真实右键打开 0.7.0 测试实例：

1. 小尺寸模式出现剪切、复制、属性、重命名等 Shell 映射入口，300 DIP 宽、640 DIP 限高；在 150% DPI 下父/子窗口均 450×960 px，内容覆盖完整、底栏在边界内。所有第三方叶子为直接列表，超过高度用滚动浏览。
2. BetterCM 菜单 F2 进入 Explorer 原生文件名编辑状态，观察到文件名选中编辑框；随后 Escape 取消。没有提交新文件名，因此此项验证范围是进入并退出原生内联编辑。
3. BetterCM 菜单 Alt+Enter 打开真实“重命名测试.txt 属性”窗口，观察到常规/详细信息等页和正确目标路径；窗口保持存在，随后 Escape 关闭。后续版本又加入宿主前台授权与来源窗口激活；最终包经过上述自动验证。
4. 在 BetterCM 菜单 Ctrl+X，打开目标文件夹，在其背景 BetterCM 菜单 Ctrl+V。文件系统确认源路径不存在、目标路径存在。背景菜单粘贴有 Ctrl+V、Shell 来源且没有重复入口。

观察快照：artifacts/qa/live-transfer-0.7.0.json。临时文件保留在 artifacts/qa/live-070/目标文件夹。未为这些观察伪造自动化报告或声称完成更多实体输入矩阵。

## 验证边界

- 没有实际清空用户已有回收站，也没有触发可能直接删除现有内容的清空动作。确认的是原生 empty 命令读取/映射与共享执行链路的修正，实际清空及其确认对话框仍未实测。
- 重命名仅在可校验的原资源管理器视图单选中提供内联编辑；导航栏/剪贴板目标等没有可用视图时不冒用其他选区。
- 仍采用 classic IContextMenu 的带文字叶子；纯 owner-draw、部分仅现代 IExplorerCommand 的扩展、其他设备/命名空间组合不在本轮完整覆盖范围。Shift+右键保留系统入口。
- 本地无 Git 远端，本轮未执行远端 CI。

## 安装与环境恢复

隔离 QA 安装包使用独立 AppId，负载与正式发布目录逐文件 SHA256 一致；安装的 .NET/WPF/材质运行时、单实例、启动项、退出与卸载全部通过。测试安装已卸载。

最终恢复原安装 0.6.0.0 后台实例。原配置 SHA256 与测试前相同（6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6），启动项保持原值。报告 artifacts/qa/environment-restored-0.7.0.json。未自动把用户正式安装升级为 0.7.0。

## 修改前备份

Backups/BetterCM-0.6.0-before-0.7.0-20261012-162544.zip：2917 文件，1,474,318,155 字节，全部逐项 SHA256 验证。归档 SHA256：539D54E35E209739F134D1687B6D951B05EE5BC8D3449DFEE02834F71EEE073D。同目录保留 manifest.json 与 sha256 文件；旧安装包保留。
