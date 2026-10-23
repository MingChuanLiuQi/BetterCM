# BetterCM 1.10.0 验证入口

当前版本为 **1.10.0**（2026-10-23）。开发阶段 QA 配置下 362 项 xUnit、309 项 UI/Shell 检查已通过。新增 37 项单元回归与 11 项 UI/实际进程检查，覆盖预览、模板、动作交换、重复项处理、待保存草稿及真实启动参数一致性。

## 本轮验证

- 纯参数计划覆盖 single/each/batch、带空格与花括号的路径、工作目录策略、动作和选区快照、展示截断及模板独立性。
- 导入导出覆盖全部字段往返、缺失/未来格式、未知字段、保留与重复 ID、大小和数量限制、无效 UTF-8、BOM、旧文件保护及合并不修改输入。
- WPF 检查覆盖最近选区、批量和逐个模板、较低工作区下底部按钮、重复策略切换、经典模式交换和草稿撤销。受控 QA 程序实际收到的参数与工作目录逐项对照预览计划。

开发阶段证据与窗口截图保存在 `artifacts/iteration-1.10.0/`。发布从主仓库干净提交完整执行 verify，随后运行 Build-Installer 及 QA 独立身份安装流程。正式验证范围、源码树哈希、程序 Build 和交付文件哈希保存在 `artifacts/releases/1.10.0/evidence/` 与 `release-manifest.json`，公开副本随 GitHub Release 的 Verification.zip 提供。

```powershell
dotnet test .\Tests\BetterCM.Tests.csproj -c QA
.\verify.ps1
.\Build-Installer.ps1 -ReuseVerifiedPublish
# 开发中也可生成独立快照候选：
.\Build-Candidate.ps1 -InstallerSmoke
```

远端 Verify 和 CodeQL 的实际状态以对应发行提交的 GitHub Actions 为准。无交互桌面的 runner 仅执行内部范围，不充当桌面测试证据。

本轮没有真实代码签名、正式身份 Setup 的实际安装、干净 VM、混合 DPI 和屏幕阅读器实测。QA 独立身份安装通过不代表这些验证已完成。

## 历史记录

- [1.9.1 QA](docs/releases/1.9.1/QA-1.9.1.md)
- [1.9.1 迭代说明](docs/releases/1.9.1/ITERATION-1.9.1.md)
- [1.9.0 功能修正](docs/releases/1.9.0/FUNCTIONAL-FIX-1.9.0.md)

历史记录中的提交、产物、通过数量和工作区状态仅描述对应时点，不能替代本版证据。
