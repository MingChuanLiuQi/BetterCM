# BetterCM 1.8.0 当前验证与发布来源

日期：2026-10-18（Asia/Taipei）。本轮复查修复后在主仓库提交源码，使用 `Build-Installer.ps1` 的干净工作树门禁重新构建，并创建本地 annotated tag `v1.8.0`。没有执行远端推送或公开发行。

上一轮 `candidate-source.bundle` 与隔离候选包保留为过程证据，原验证记录见 [候选包历史](QA-1.8.0-candidate.md)；它们不再代表当前发布源。当前安装包必须由主仓库提交派生，以 payload 的 SourceCommit 和程序集 Build 标识交叉核对。

## 已执行的修复回归

- xUnit：250 项通过（上一轮 243 + 本轮新增 7），包含自定义工作目录显式选择、schema 2 缺省值、迁移前清理旧默认动作和恢复备份落盘格式。
- PowerShell 脚本检查：14 项通过，覆盖拒绝未签名文件、Inno 临时卸载器回调证据、发布最终包门禁、内部报告类型、审计路径脱敏、未跟踪 dotfile 源码备份和生成物排除。
- 回调测试使用显式的测试替身，不生成受信任证书、不修改用户证书信任存储；不能当作真实 Authenticode 成功证据。

## 主仓库发布门禁与最终证据

| 门禁 | 最终证据 |
|---|---|
| 脚本回归、锁文件恢复和传递依赖审计 | artifacts/qa/script-tests.json、dependency-audit.json |
| Release 编译，warnings-as-errors；xUnit | artifacts/qa/headless/headless-tests.trx |
| UI / Shell 完整 273 项，稳定 ID 与审查清单匹配 | artifacts/qa/published-tests.json |
| 干净主仓库提交、程序集标识、完整载荷与最终安装包 SHA256 | artifacts/qa/payload-1.8.0.json |
| 隔离标识安装、安装后自检、后台启动/单实例/退出、卸载和日常环境保留 | artifacts/qa/installer-smoke.json、installed-tests.json |
| 维护实际执行，归档逐文件哈希校验与移除结果 | artifacts/maintenance-report.json |

上述报告中的 Passed、SourceCommit、Build 和安装包 SHA256 是最终结果依据；不将历史候选报告的结果或 SHA256 套用到新安装包。`Test-Installer.ps1` 默认 Release 类型要求最终包和 payload 存在且与 HEAD 一致；只有显式 `-ReleaseKind Internal` 才允许没有最终包，并在报告中记录类型。

安装包：`artifacts/releases/1.8.0/BetterCM-1.8.0-Setup-x64.exe`；本轮验证通过后同步到 `artifacts/internal/1.8.0`。完整 SHA256 和源提交由本轮自动生成报告提供。

## 签名问题的实际原因

复查指出的签名路径风险成立，但不能简单将 `uninst*.e*` 改成 `unins*.exe`：Inno 配置 SignTool 时会签名 `uninst.e32.tmp`，将其嵌入安装器后删除临时文件；安装后的 `unins000.exe` 尚不存在。现在由签名回调完成签名、信任链及时间戳检查，并在临时文件删除前另存 `signed-uninstaller.exe`，构建再验证并保留它。

依据：[Inno 6.5.4 编译器源码 SignSetupE32](https://github.com/jrsoftware/issrc/blob/is-6_5_4/Projects/Src/Compiler.SetupCompiler.pas#L7370)。本轮没有真实代码签名证书，正式签名发布仍为 **BlockedNoCertificate**；不把测试替身或未签名构建说成签名成功。

## 未覆盖与保留边界

- 无真实证书，签名成功、时间戳服务和证书链的完整演练未完成。
- 远端 GitHub Actions、交互 runner、鼠标 E2E、Accessibility Insights 和屏幕阅读器未实跑。
- ARM64、混合 DPI 及所有第三方 Shell 扩展组合仍未验证；图标独立来源待确认。
- Maintenance 保留所有已有历史备份和 QA 原目录，不删除或声称回收全部 53 GB 历史备份；本轮以实际 maintenance-report 计量归档结果。

## 1.8.1 复查补充说明

本文件保留 1.8.0 历史记录。其安装/卸载演练实际使用 QA TestBuild 标识，最终正式 Setup 仅校验哈希、载荷和来源，没有执行真实安装；不能据 ReleaseKind=Release 推定正式标识安装已验证。旧 RemainingDev=3 仅统计 dev-*，未覆盖 dev2–dev9。后续修复、完整统计和保留边界见 [QA-1.8.1](../1.8.1/QA-1.8.1.md)。根目录 artifacts/qa 中通用文件名报告会随下一版更新，1.8.0 报告副本保存于 artifacts/qa/history/1.8.0。
