# BetterCM 1.9.0 本地验证

日期：2026-10-18，Asia/Taipei。环境：本机 Windows 11 x64、交互桌面、Explorer、.NET SDK 10.0.401、Inno Setup 6。输入为用户粘贴的 DeepSeek 建议；本轮实际修改代码和运行验证。

## 实测通过

| 项目 | 结果与证据 |
| --- | --- |
| 锁文件恢复 | `dotnet restore BetterCM.slnx --locked-mode` 通过，无依赖版本变更。 |
| 依赖漏洞审计 | NuGet 官方 advisory 源、包含传递依赖，未报告漏洞；`artifacts/qa/dependency-audit.json`。 |
| 格式与编译 | dotnet format whitespace --verify-no-changes 通过；Release 与 QA 编译均 0 警告、0 错误。全仓首次建立 C# whitespace 基线，较多文件仅规范化格式。 |
| xUnit | 274/274，新增 20 个用例实例；`artifacts/qa/headless/headless-tests.trx`。两分钟挂起限制启用。新增 STA 启动即取消/Dispose 的线程退出回归，避免批量取消遗留后台线程。 |
| UI/Shell | QA 自包含发布 276/276，失败 0；`artifacts/qa/published-tests.json`。新增三种主题选项重排后按 Tag 读取检查。 |
| PowerShell 回归 | 58/58；`artifacts/qa/script-tests.json`。包括外置存储、迁移、归档数量/容量、非法根目录/非有限容量，以及发布回执接受/拒绝场景。 |
| 覆盖率 | Microsoft Code Coverage 已输出 Cobertura XML 至 `artifacts/qa/headless/<run-id>/`。最终统计：2047/6029 行（33.95%）、1391/5742 分支（24.23%）；覆盖面含 QA 代码和未执行 UI/COM 路径，不把该比例解释为完整产品保证，也未设置百分比门槛。 |
| 正式二进制隔离 | PE 元数据检查没有 QA 检查/夹具类型，字符串检查没有测试开关、QA 环境变量或假宿主协议；同一检查器对 QA 程序集正确拒绝，报告命中 BackdropUiChecks。 |
| 正式入口拒绝 | 自包含 Release 实际运行 `--self-test`、`--verb-probe`、`--shell-test-host` 均退出码 2；即使提供 QA 环境变量，也未写目标报告。`artifacts/qa/production-isolation.json`。 |
| 脚本语法/补丁 | 所有根目录、Installer、Assets PowerShell 脚本经解析无语法错误；git diff --check 通过。 |
| 图标 | 原创脚本生成 7 个尺寸的 ICO；256px 预览人工视觉检查通过，图案及边缘完整。`artifacts/qa/icon-1.9.0.png`。 |
| 历史存储迁移 | 同卷 Directory.Move；迁出后 58 个旧备份 + 57 个旧归档，文件数与总字节数均匹配迁出前统计。`artifacts/qa/storage-migration.json`。 |
| 候选安装器 | Inno Setup 编译成功；`artifacts/candidates/1.9.0/BetterCM-1.9.0-Setup-x64.exe`，附 SHA256。载荷来自正式 Release，不含 QA 自检。`artifacts/qa/candidate-1.9.0.json` 记录完整载荷哈希与候选身份。 |

## 产物身份

这是当前**未提交工作树的未签名候选**。没有创建 Git 提交、v1.9.0 标签、远端推送或公开发布。程序集沿用 SDK 的“版本 + 父提交”信息版本字段；因为工作树有修改，该父提交不能单独代表本轮源码。候选清单明确记录 `SourceState=UncommittedWorkingTreeCandidate`。

`Build-Installer.ps1` 要求干净提交的正式发布门禁保留。本轮直接使用 Inno 编译到 candidates 目录以验证脚本和载荷；没有冒充已通过主仓库正式发布门禁。CI 的复用回执记录 SourceClean=false 时会拒绝复用；相关成功/失败路径通过独立脚本夹具验证。

正式二进制和 QA 二进制分别位于 `artifacts/publish/win-x64` 与 `artifacts/diagnostics/win-x64`；QA 自检结果只证明 QA 构建上的对应 UI/Shell 行为。正式构建另经编译、隔离扫描及实际诊断入口拒绝验证，未声称用 QA 二进制测试等同于正式安装包完整 E2E。

## 存储变化

- `Backups` → `../BetterCM.Storage/legacy-backups`：55,728,937,441 字节。
- `artifacts/archives` → `../BetterCM.Storage/legacy-archives`：5,706,525,696 字节。
- 合计 61,435,463,137 字节，原目录已不在项目树。全部历史文件保留，同卷移动不释放磁盘空间。
- 其他 artifacts 发布包、QA、bin/obj 未迁移，不能声称解决了原报告中的全部约 67 GB。
- 新归档容量策略默认不删除；只有显式 PruneArchives 才清理脚本识别的生成归档，旧迁移目录不参加。

## 验证边界

没有可用真实代码签名证书，尚未验证签名、RFC3161 时间戳及信任链的成功路径；本轮没有购证或注册签名服务。新图标来源问题已通过替换解决，历史图标不作追认。

本轮没有安装当前候选 Setup，也未运行干净 Windows 11 环境、ARM64、混合 DPI、人工鼠标输入、屏幕阅读器或远端 CI。既有完整 UI/Shell 自动检查不替代这些验证。

watcher Error 重建、hook 安装失败后重试及系统主题读取异常隔离已实现；没有在真实系统中主动制造文件监听缓冲溢出、原生 hook 安装失败或注册表读取失败，不把正常路径检查当作这些故障注入已通过。

Shell 宿主保留活跃原生操作，不加入强制终止超时。完整 App/窗口重构、强类型 COM 适配、国际化、服务目录重排与额外交互自动化框架延后，详见 [建议处理记录](REVIEW-1.9.0.md)。
