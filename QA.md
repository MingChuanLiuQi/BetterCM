# BetterCM 当前验证入口

当前为 **1.9.0 功能与体验迭代修正**（2026-10-23）。本轮 `verify.ps1 -SkipDesktopChecks` 已完整通过：Release/Debug/QA 构建均无警告，QA/Release 各 309 项 xUnit、67 项脚本回归、格式与 IDE0049、依赖审计及正式二进制 QA 隔离检查全部通过；在沙盒桌面下运行 `--self-test` 时，包含新增 `ReviewFix/PreserveDraftOnReopen` 与 `ReviewFix/CustomOrderAndBrowse` 在内的 17 项 `ReviewFix` 检查及共 285 项 UI/Shell 检查通过（依赖交互式 `WinSta0\Default` Explorer `Progman` 桌面窗口的检查需在真实交互桌面会话下运行 `.\verify.ps1`）。

本轮修复与范围见 [功能修正记录](docs/releases/1.9.0/FUNCTIONAL-FIX-1.9.0.md)。报告位于 `artifacts/review-fix/`，可运行产物位于 `artifacts/review-fix/publish/`；工作树仍保留原有未提交修改。补充打包时，在逐文件匹配的独立干净源码快照中重新完整验证并编译正式身份的未签名 Setup，位于 `artifacts/candidates/1.9.0-functional-fix/`。随后 QA 独立安装身份的 13 项安装/启动/卸载检查和安装目录 291 项自检通过。这不代表最终正式身份 Setup 已在干净 VM 安装；下述旧安装结果仍仅是历史证据。

## 上一轮稳定性验证（历史记录）

当前为 **1.9.0 第三轮稳定性修正**（2026-10-19）。最新证据、测量契约和限制见 [稳定性修正记录](docs/releases/1.9.0/STABILITY-FIX-1.9.0.md)。前两轮 [初始 QA](docs/releases/1.9.0/QA-1.9.0.md) 与 [复查记录](docs/releases/1.9.0/REVIEW-FIX-1.9.0.md) 均保留为历史证据。

在独立干净快照上连续两次完整 verify 均一次通过，没有重试。每次含 Release/Debug/QA 解决方案构建、QA/Release 各 289 项 xUnit、67 项脚本回归、276 项 UI/Shell 自检、格式与 IDE0049 style、依赖审计、覆盖率和正式二进制隔离。真实 Build-Installer -ReuseVerifiedPublish 通过；随后 QA TestBuild 的 13 项安装流程检查和安装目录内 276 项自检也一次通过。

首帧门禁改为独立首次样本 <1000 ms、五次稳定采样中位数 <250 ms / nearest-rank P95 <500 ms。全部样本须真实 Hold、完成呈现、DWM 解除遮蔽、有限正耗时；动画关闭或 Hold 失败不能以 0 ms 通过。第一次打开是检查窗口的首次呈现，指标从 Hold 开始，不是进程启动或 Shell 读取的端到端计时。

| 执行 | 首次打开 ms | 稳定中位数 ms | 稳定 P95 ms |
| --- | ---: | ---: | ---: |
| 完整 verify 1 | 162.73 | 11.31 | 66.63 |
| 完整 verify 2 | 105.17 | 13.95 | 46.16 |
| 安装目录自检 | 63.89 | 13.47 | 39.26 |

验证提交 `2e472badeca3dd64f9b14fd532bcc8fa58861e38` 属于独立快照仓库；153 个实现/构建相关文件与当前工作树哈希一致。主仓库 HEAD 未改变，工作树仍未提交，没有版本标签、推送或公开发布。文档在验证后补充。

候选包位于 `artifacts/candidates/1.9.0-stability-fix/`，证据位于 `artifacts/qa/stability-fix/`，包括两次独立报告、全部原始首帧样本、干净回执、载荷哈希和安装报告。此前的失败报告未覆盖或删除。

这次稳定了检查方式，没有修改菜单渲染实现或声称缩短产品延迟；两次通过不代表长期或全部硬件上的时延保证。真实代码签名、最终 Release Setup 实际安装、非隔离分支的干净 VM、远端 CI/CodeQL、人工鼠标、混合 DPI、屏幕阅读器及 ARM64 验证仍未完成。
