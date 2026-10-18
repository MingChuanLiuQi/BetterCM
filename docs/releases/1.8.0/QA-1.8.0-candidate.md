# 上一轮候选包验证（历史证据）

此文保留初轮隔离候选源码的验证结果，不是本轮主仓库发布来源。当前验证见 [QA-1.8.0](QA-1.8.0.md)。

# BetterCM 1.8.0 审查加固验证

日期：2026-10-18（Asia/Taipei）。主仓库保留未提交改动；安装器通过复制源码到隔离的临时 Git 仓库、创建验证专用提交后执行原有干净源码发布门禁。主仓库没有创建提交或推送。

## 已执行

| 检查 | 结果 | 证据 |
|---|---|---|
| NuGet locked restore 与传递依赖漏洞审计 | 通过，未报告已知漏洞 | artifacts/qa/candidate-dependency-audit.json |
| Release 编译（警告视为错误） | 通过，0 警告、0 错误 | artifacts/clean-installer-build-review.log |
| xUnit | 243 通过、0 失败、0 跳过；原有 223 + 新增 20 | artifacts/qa/candidate-headless-tests.trx |
| UI / Shell 自检 | 273 通过，全部 ID 非空且唯一，与审阅清单一致 | artifacts/qa/candidate-published-tests.json |
| 发布目录不含 Tests / xUnit / testhost | 通过 | verify.ps1 门禁 |
| 无障碍 peer | STA 测试验证菜单项角色、名称、勾选与禁用状态 | Tests/MenuAccessibilityTests.cs |
| 二进制种植回归 | 目标目录同名 exe 不覆盖已固定程序，拒绝相对路径及隐式脚本解释器 | Tests/HardeningTests.cs |
| 配置迁移 / 损坏保护 | v1 在内存迁移、保存保留原文备份、未知未来版本/缺字段/空列表/损坏 JSON | Tests/HardeningTests.cs 和 ConfigRecoveryTests.cs |
| 诊断包脱敏 | 自定义标题、路径、文件名、用户名、参数及日志消息不进入 ZIP | Tests/HardeningTests.cs |
| 系统程序路径 | PowerShell、Notepad、msiexec 均为现有绝对路径且不含控制字符 | Tests/HardeningTests.cs |
| 跨进程报告临时占用 | 增加限时读取重试，文件独占锁释放后读取成功 | Tests/HardeningTests.cs |
| 历史安装包保留 | 原根目录 19 个 exe 与原始提交内 SHA256 全部匹配 | 本轮逐包核对 |
| Maintenance | 主目录仅 dry-run；临时仓库 Apply 验证 Git bundle、ZIP 文件哈希、生成物排除、未跟踪源码白名单、QA/release/旧备份保留；无 artifacts 的新目录也可预览 | artifacts/maintenance-preview.txt、maintenance-apply-fixture-review.log、maintenance-fresh-preview-review.txt |
| 未签名产物拒绝 | Assert-ArtifactSignature 拒绝 NotSigned 主程序 | 本轮实际调用 |
| PowerShell 语法 / diff 空白 | 全部脚本解析通过；git diff --check 通过 | 本轮实际调用 |

## 安装演练

最终干净候选源码构建与隔离安装、卸载均通过。隔离演练共 **13 项通过**，覆盖安装版本/运行时、逐文件 SHA256、许可文件、自启项、安装后 273 项自检、后台启动、单实例、正常退出及卸载清理；原有用户配置和 BetterCM 启动项均保持不变。报告为 `artifacts/qa/candidate-installer-smoke.json`，安装后自检为 `candidate-installed-tests.json`。

内部未签名安装包：`artifacts/internal/1.8.0/BetterCM-1.8.0-Setup-x64.exe`，同目录提供 `.exe.sha256`。

- SHA256：`92795093C3238A79534AF08E48530010416D2DD12460024A5F37B59781AEBE1E`
- 候选源码提交：`72f0e2707e0f8426a5a6928781519cfc1a8b2d40`（仅隔离验证仓库，非主仓库提交）。
- 完整候选源码历史：`artifacts/qa/candidate-source.bundle`，已通过 `git bundle verify`。
- 载荷清单：`artifacts/qa/candidate-payload-1.8.0.json`。复制到主工作区的 publish 文件逐一核对该清单，全部匹配。
- 主工作区 `artifacts/publish/win-x64` 及标准 published/installed 报告同步为最终候选产物；先前报告以 previous- 前缀保留。第一次隔离演练暴露 Shell probe 报告的瞬时文件占用；已修复并增加回归测试。另一次 QA 编译遇到 Inno EndUpdateResource(110) 外部资源占用，改用独立编译输出目录重试；没有修改杀毒软件设置。

## 未覆盖

- 没有真实证书，未测试签名成功、时间戳服务及受信任卸载器的完整流程。
- 未运行远端 GitHub Actions、部署交互 runner 或执行无人值守鼠标 E2E。
- 未用 Accessibility Insights 或真实屏幕阅读器验证 UIA 树；未增加英语 UI。
- 未验证 ARM64、混合 DPI、所有第三方 Shell 扩展组合或用户日常安装的真实升级过程。
- 没有清理主仓库历史备份或大规模删除构建产物；原始历史证据继续保留。

修改明细与逐条边界见 [审查实施记录](../../current/REVIEW-IMPLEMENTATION.md)。


## 临时构建目录清理限制

最终安装包、源码 bundle 和 QA 证据已另存。尝试删除本轮生成的 `artifacts/clean-build-fixture-36aaddf2c483447c8411002995e527c8` 时，自动审批返回 `blocked by policy`，未提供具体理由。因此目录仍保留，未改用其他删除方式绕过。
