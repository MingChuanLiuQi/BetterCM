# BetterCM 1.8.1 本地验证与保留边界

日期：2026-10-18（Asia/Taipei）。在主仓库干净提交上使用 Build-Installer.ps1 构建，源提交和安装包 SHA256 以 artifacts/qa/payload-1.8.1.json 为准；本地 annotated tag v1.8.1 标记构建源。没有远端推送或公开发行，v1.8.0 标签和历史包保留。

## 本地验证门禁

- 254 项 xUnit：包含缺失程序禁用、配置保留、介质恢复/移除、程序名拒绝等回归。
- 37 项脚本回归：覆盖五类维护目录的完整计数和最近保留、dry-run、逐文件校验归档、未签名重建清理、双架构证据及安装报告边界。成功夹具在 finally 中清理；失败夹具保留排查。
- Build-Installer.ps1 运行锁文件恢复、传递漏洞审计、warnings-as-errors 编译、自带运行时发布及 273 项 UI/Shell 自检。
- Test-Installer.ps1 使用共享 publish 载荷的独立 QA TestBuild 标识，检查安装、启动项、安装后 273 项自检、后台启动、单实例、退出和卸载，并核对原有配置未变。
- Maintenance 默认 dry-run；实际 Apply 保留每类最近 3 个目录，所有归档逐文件 SHA256 校验后才删除源构建目录。本轮使用 -SkipQaArchive 避免重复压缩 QA 原目录，历史 QA 归档及原目录均保留。

上述结果应以本轮生成报告的 Passed 和计数为准，任何失败均不能据本文推定通过。完整汇总在 artifacts/qa/recheck-summary.json，维护逐项结果在 artifacts/maintenance-report.json。

## 最终 Setup 与 QA 安装的区别

最终包位于 artifacts/releases/1.8.1/BetterCM-1.8.1-Setup-x64.exe，同字节内部副本在 artifacts/internal/1.8.1。payload 的 SourceCommit、程序集 Build、最终 Setup SHA256 必须一致。

实际安装的是 QA 标识包；ReleaseKind=Release 表示最终包存在且与 HEAD/payload 一致。报告另外明确记录 ValidationScope=QaIdentitySharedPayload、ExactFinalSetupInstalled=false、FinalSetupValidation=HashAndSourceOnly、CleanEnvironmentVerification=NotPerformed。

用户暂无一次性 Windows 11 环境，已要求先完成本地修复并保留此边界。最终 Setup 的真实 AppId、产品名称、快捷方式、启动项与卸载清理仍需在可丢弃环境执行；当前不能称为“已验证的正式发布”。已有用户虚拟机未启动或改动。

## 签名边界

无真实代码签名证书，Signed=false、SigningStatus=BlockedNoCertificate。签名回调处理测试使用明确的测试替身，不修改证书信任存储，不等同真实 Authenticode 成功。

回调分别保存 signed-uninstaller-e32.exe / e64.exe，payload 的 SignedUninstallers 按架构记录哈希；签名正式包演练时安装脚本会校验实际 unins000.exe 的签名及其哈希属于捕获证据。仍需真实证书验证 Inno 回调、最终嵌入的架构、时间戳、证书链在线吊销检查、SignedUninstaller 及其伴随文件。未签名重建会清除当前发布目录中这三种已知旧证据文件，保留其他文件。

## 其他外部边界

远端 GitHub Actions、交互桌面 runner、鼠标 E2E、屏幕阅读器 / Accessibility Insights、ARM64、混合 DPI、全部第三方 Shell 扩展组合均未据本机测试推定通过。图标独立来源与授权仍待确认。

1.8.0 的旧 RemainingDev=3 只统计 dev-*，漏掉编号目录，不能代表当时全部开发目录；本轮汇总从维护报告 Development 类及磁盘全部匹配目录计算。历史安装包和所有已有 Backups 保留，不将压缩前目录大小当作净磁盘回收量。
