# 1.9.0 第三轮稳定性修正

日期：2026-10-19，Asia/Taipei。仍为未公开发布的 1.9.0 工作树，不改变版本号；旧候选包及前两轮失败证据保留。

## 测量契约

NativeUi/C037 对检查窗口的第一次打开单独记录一次样本，再对复用的同一窗口连续采集五次打开，全部原始值写入报告 Detail 的 JSON。第一次打开已起到预热作用。

- 第一次样本 <1000 ms，稳定状态中位数 <250 ms、nearest-rank P95 <500 ms。五次样本的 P95 等于最大值，此处不把五次采样解释为总体分布的置信保证。
- 所有样本必须 Held=true、Completed=true（菜单仍打开且不再 Pending）、DWM 查询成功且解除遮蔽、有限且严格大于 0 的耗时。
- 未执行动画、DWM cloak 失败、缺失/取消样本或 0 ms 均失败；无动画路径另检查 Held=false、Delay=0，不能混入有效样本。
- 指标只测 Hold 到首帧准备完成。这里的第一次打开是该检查窗口首次呈现，不包含进程初始化、窗口构造和 Shell 读取，不等同于冷启动端到端延迟。
- 此次是显式调整测量契约，不是修复或声称加速了产品渲染。稳定状态中位数保留原 250 ms 目标，新增独立冷态/尾部预算；门禁不加自动重试。

## 其他修正

script-fixture 与 script-storage 的失败遗留目录共用 ScriptFixture 保留预算；两种归档名均参与显式归档清理。回归模拟失败遗留目录并检查归档校验和后续清理；本轮新归档保护仍保留。

ConfigurationWatcher 首次创建遇到 IO/权限错误后也保留对象并安排两秒重试。新增实际文件系统故障测试：用文件阻止目录创建，修复障碍后等待恢复，再写配置验证新监听器收到事件。预算边界、无效样本、统计和 watcher 回归使 xUnit 增至 289 项，脚本回归为 67 项。

## 实际验证

在独立快照仓库提交 `2e472badeca3dd64f9b14fd532bcc8fa58861e38` 上连续运行两次完整 verify。每次都会新建 self-test 进程；两次均一次通过，没有重试，也没有复用上一轮 self-test 报告。

- 每次 Release/Debug/QA 解决方案构建均为 0 警告、0 错误。
- 每次 QA/Release 测试项目均 289/289，脚本检查均 67/67，UI/Shell 均 276/276。
- whitespace / IDE0049 style、锁文件恢复、依赖审计、Cobertura 采集、正式程序集隔离及三个正式入口拒绝均通过。
- 第二次完整验证后，回执 SourceClean=true、DesktopChecks=true；实际执行 Build-Installer -ReuseVerifiedPublish 成功。
- 随后编译 QA TestBuild 并运行默认 Internal 范围的隔离安装：13/13 流程检查及安装目录 276/276 自检均一次通过。卸载完成，原有用户配置和启动项保留。

| 执行 | 首次打开 ms | 稳定中位数 ms | nearest-rank P95 ms |
| --- | ---: | ---: | ---: |
| verify 1 | 162.7287 | 11.3080 | 66.6287 |
| verify 2 | 105.1674 | 13.9454 | 46.1644 |
| 安装目录 | 63.8885 | 13.4701 | 39.2567 |

三次均记录 1 个首次与 5 个稳定样本，全部 Held/Completed/Uncloaked 为 true、耗时严格大于 0。完整原始样本与预算值保存在 artifacts/qa/stability-fix/first-frame-measurements.json。

## 来源、产物与证据

验证快照共 223 个文件，153 个实现/构建相关文件与当前工作树逐文件哈希一致。主仓库 HEAD 仍为 `2d7e3201c969f33d86d708528fe18307ce3205a1`，没有在主仓库创建提交、标签或远端发布。当前 QA 文档在验证完成后补充，源码包记录构建时的干净快照。

新候选为 artifacts/candidates/1.9.0-stability-fix/BetterCM-1.9.0-Setup-x64.exe，61,228,994 字节。

SHA256：`66B048C8A992E3B335CDA38A90550412EE03378E9D1B8DD3948A5BFA5442BDA6`。

同目录附 SHA256、构建源码 ZIP 和快照 Git bundle，旧候选包及旧失败证据保留。

证据目录 artifacts/qa/stability-fix/：

- clean-validation.json：连续两次桌面、实际载荷复用及隔离安装汇总。
- clean-source.json / source-match.json：快照提交、文件清单、当前实现哈希比较。
- stability-pass-1/ 与 stability-pass-2/：各自的 UI/Shell、脚本、隔离、回执、xUnit 和覆盖率报告。
- clean/：最终回执、完整载荷哈希、安装报告和安装目录自检。
- first-frame-measurements.json：三次执行的全部首帧样本和统计。
- source-artifacts.json：源码 ZIP/bundle 哈希。

首次样本预算及稳定状态尾部预算是本轮显式定义的契约，不以此追认旧单次 250 ms 检查为通过。历史 370.33/272.69 ms 失败及其复测记录仍保留在 review-fix 目录。此次没有给 verify 增加自动重试，也没有声称渲染代码已得到性能优化。

实际安装包为 QA TestBuild，正式 Release Setup 尚未实际安装，ExactFinalSetupInstalled=false。当前包未签名；真实签名、干净 VM、远端 CI、不同 DPI/硬件、屏幕阅读器、ARM64 与长期延迟分布验证仍属外部边界。
