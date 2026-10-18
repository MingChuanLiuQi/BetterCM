# BetterCM 当前验证记录

当前版本为 **1.8.0**。本轮复查后的主仓库构建与证据入口见 [QA-1.8.0](docs/releases/1.8.0/QA-1.8.0.md)，修改说明见 [审查实施记录](docs/current/REVIEW-IMPLEMENTATION.md)。[初轮隔离候选包](docs/releases/1.8.0/QA-1.8.0-candidate.md) 仅作历史过程证据。

`Build-Installer.ps1` 要求干净主仓库提交、锁文件、漏洞审计、脚本/逻辑测试、发布目录检查及 273 项 UI/Shell 自检通过；最终包在 artifacts/releases/1.8.0，载荷清单记录其 SHA256 与 SourceCommit。本地 annotated tag v1.8.0 标记源码，没有远端推送。

`Test-Installer.ps1` 默认 Release 门禁，最终包或对应 payload 缺失不能通过；纯内部演练须显式传入 -ReleaseKind Internal。维护默认 dry-run，Apply 只在归档及逐文件校验后移除旧构建目录，保留历史备份和 QA 原目录。

真实代码签名证书尚未提供，正式签名发布明确阻塞；远端 CI、真实屏幕阅读器、鼠标 E2E、混合 DPI 与 ARM64 不据本机自检通过推定为已验证。
