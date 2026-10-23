# 1.9.0 工作树复查修正

本记录接续首次 1.9.0 QA。已复现原解决方案 Release 构建缺失 RegressionCheck，并修复；本轮不改变产品版本号。

- 解决方案声明 Debug/Release/QA；Release 仅构建产品，Debug/QA 包含依赖 QA 的测试。CodeQL 明确编译产品；verify 增加三配置解决方案构建和 QA/Release 测试项目检查。
- 归档清理只使用运行开始时已有文件的计划，在创建新归档和删除源目录前执行。新归档不受本轮数量/容量清理影响，报告记录保护列表与 PreRunArchivesOnly 范围。新增 0 数量及超过容量预算的真实文件恢复回归。
- BackupRoot 拒绝工作区内部及其父目录；脚本测试使用旁置存储并按安全路径检查清理成功夹具。
- watcher 重建失败明确设置两秒重试，并容忍 Dispatcher 退出竞态。InputService 的 hook 句柄/线程 ID 使用 Volatile 读写。
- Clone 测试反射填充所有可写字段为非默认值、比较序列化结果，并递归断言可变对象不共享；未来新字段类型没有夹具时直接失败。
- 隔离安装默认 Internal，显式 Release 仍保留最终包检查；隔离模式检查卸载清除测试启动项，非隔离模式检查清理恢复后的原值及类型，不再无条件要求原启动项为空。
- style 门禁明确检查 IDE0049 预定义类型风格并保留 whitespace 检查；不把它描述为全部代码风格规则。

## 实际验证结果

在项目树外建立独立快照仓库，提交为 `86c1d732f57f85b371045690bd19dcc537482f55`。快照包含 219 个源码文件；150 个实现、配置及构建文件与当前工作树逐文件哈希一致（文档在验证后补充）。主仓库 HEAD 没有改变，没有在主仓库提交、打标签或推送。

| 检查 | 结果 |
| --- | --- |
| Release/Debug/QA 解决方案构建 | 全部通过，0 警告、0 错误。Release 按声明仅构建产品。 |
| 直接测试项目 QA / Release | 各 275/275 通过，均有两分钟挂起限制。 |
| PowerShell 回归 | 65/65 通过；包括两个真实归档恢复用例及工作区内部存储根拒绝。 |
| 格式与风格 | whitespace 及明确启用的 IDE0049 style 检查通过。 |
| 依赖恢复和审计 | locked mode 及传递依赖漏洞审计通过。 |
| 正式程序隔离 | 程序集检查及三个测试入口的退出码/未写报告检查通过。 |
| QA 发布桌面自检 | 复测 276/276 通过；首轮有一项时延失败，详见下文。 |
| 真实载荷复用与安装器编译 | 完整 verify 后的回执 SourceClean=true、DesktopChecks=true，实际运行 Build-Installer -ReuseVerifiedPublish 成功。 |
| QA TestBuild 隔离安装 | 默认 ReleaseKind 实际为 Internal；13/13 流程检查通过，覆盖载荷 SHA256、自启项、后台运行、单实例、退出、卸载和原用户数据保留。 |
| 安装目录自检 | 复测 276/276 通过；首次仍有同一首帧时延超限。 |
| 覆盖率 | Cobertura 已生成，逻辑测试行覆盖率 2081/6067（34.30%），统计包含 QA 与未执行 UI 路径；没有百分比门槛。 |

主要证据在 `artifacts/qa/review-fix/`：

- `clean-validation.json`：真实复用和隔离安装的汇总。
- `clean-source.json`、`source-match.json`：快照提交、文件清单和当前实现一致性。
- `clean/verified-publish.json`、`clean/payload-1.9.0.json`：干净回执和完整载荷哈希。
- `clean/headless/`、`clean/script-tests.json`：275 项两配置测试、覆盖率和 65 项脚本回归。
- `clean/published-tests.json`、`clean/installed-tests.json`、`clean/installer-smoke.json`：发布/安装后的自检及安装流程。

## 未忽略的时序失败

首次完整 verify 的 NativeUi/C037 报告 ready=370.33 ms，超过原有 250 ms 条件；同一干净源码再次完整执行后 ready=183.78 ms、276 项全部通过。首次隔离安装后的同项 ready=272.69 ms，清理后以相同安装器和门槛再执行，ready=107.45 ms、13 项安装检查及 276 项自检通过。

保留 `published-tests-first-attempt.json`、`verify-first-attempt.log`、`installed-tests-first-attempt.json`、`installer-smoke-first-attempt.json` 和 `isolated-install-first-attempt.log`。没有放宽门槛、修改相关菜单首帧实现或给 verify 加自动重试。这些复测证明存在环境/时序波动，不能证明冷启动时延问题已解决。

## 候选包与身份

新的候选安装包为 `artifacts/candidates/1.9.0-review-fix/BetterCM-1.9.0-Setup-x64.exe`，61,198,038 字节。

SHA256：`B7539E73248EF8CA8B25F301D120DAD636AFE8B72CB825114CF2B4F6BBAA327B`。

同目录保留 SHA256 文件、该干净提交的源码 ZIP 和 Git bundle。快照 bundle 记录独立验证仓库的提交，不取代原主仓库完整历史备份。候选包的程序集提交标识对应快照提交；主工作树本身仍未提交，不能把该结果当作主仓库当前 HEAD 已通过发布门禁。

实际安装使用独立 GUID 的 QA TestBuild 及 QA 载荷；正式 Release Setup 只完成编译、载荷/来源/哈希门禁，没有实际安装，ExactFinalSetupInstalled=false。非隔离分支的启动项断言已修正，但没有在干净 VM 中执行。

存储中的旧发布包和 QA 证据暂不自动清理；本轮只收紧存储根位置和修复删除安全性，避免未经逐版证据核对就删除历史发布资产。真实签名、干净 VM 安装、远端 CodeQL 服务及人工鼠标/DPI 等边界继续保留。
