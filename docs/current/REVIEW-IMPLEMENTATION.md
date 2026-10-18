# DeepSeek 审查实施记录（1.8.0）

日期：2026-10-18。源码版本从 1.7.0 升至 1.8.0；初轮主仓库未提交；本轮按复查要求在主仓库提交、打本地 v1.8.0 标签并重新构建，没有远端推送或公开发行。用户选择 MIT；没有提供代码签名证书或可核对的图标来源。

| 建议 | 本次处理 | 边界 / 后续 |
|---|---|---|
| 1 发布身份与许可 | 新增 MIT LICENSE 并随程序分发；构建提供 SHA256、RFC3161 时间戳、Authenticode 信任与证书链验证；支持 SignedUninstaller | 无真实证书，签名成功路径未实测，历史和本次安装包均未签名；图标来源在 THIRD-PARTY-NOTICES 明确待确认 |
| 2 程序解析 | 编辑时只在绝对 PATH 项 / App Paths 查找并固定路径；执行再次要求现有绝对 exe/com；系统程序使用完整路径 | 不自动查找目标目录；显式配置解释器的脚本动作仍按用户指令执行 |
| 3 制品与仓库 | 19 组根目录安装包/校验文件移至忽略的 artifacts/releases；所有原始安装包按 SHA256 验证 | 已存在同版本载荷时另放 root-archive-20261018，避免覆盖；未上传任何远端制品库 |
| 4 备份体积 | Maintenance 默认 dry-run；源码白名单排除任意层 bin/obj/artifacts/Backups；完整 git bundle；压缩并逐文件校验构建目录后才移除；QA 压缩保留原目录 | 主仓库未执行 Apply；每类保留最近 N 个构建目录，所有原始历史备份均保留，不声称已回收原有 64 GB |
| 5 工程配置 | slnx、共享 props、集中包版本、NuGet.config、两份 lock file、warnings-as-errors、gitattributes | 保持现有源码目录；根项目排除规则收敛到共享 props，覆盖 WPF 临时编译项目 |
| 6 CI 与供应链 | concurrency、45 分钟超时、NuGet 缓存、Actions 固定提交、locked restore、传递漏洞审计、Dependabot、CodeQL、Inno 编译、可选交互桌面作业、PR QA 清单 | 远端 GitHub Actions 未运行；私有仓库 CodeQL 需启用服务并设置 ENABLE_CODEQL=true；没有部署交互 runner 或无人值守鼠标 E2E |
| 7 schema 迁移 | CurrentSchemaVersion=2，v1→v2 在内存迁移；不可解析的旧动作保留并禁用；未来格式和损坏配置继续拒绝保存 | 显式保存会写 schema 2，旧版本软件不支持；保存前原文不变，保存后保留 .bak |
| 8 文档组织 | 逐版 HANDOFF/QA/REVIEW 移至 docs/releases，诊断文档移至 docs/current；当前架构独立，逐版笔记保留在 docs/history | 历史记录不重写为当前事实；相关 Markdown 链接已调整 |
| 9 稳定自检 ID | 273 项 UI/Shell 自检的显式 ID，与中文名称分离；Expected-Checks 用 ID；显示映射仅提示差异；安装自检同步修改 | ID 是写入源码的常量，修改中文显示名不会重新生成 ID；新增/删除项仍须审查清单 |
| 10 编排类 | 引入 MenuPresentationRequest，生产调用改为具名字段；拆 Keyboard/NativeMenu partial；抽 ConfigurationWatcher；菜单视图依赖 IMenuHost | 本次按增量方案执行，Tray/SingleInstance/SettingsCoordinator 全面拆分留作独立迭代，避免扩大生命周期改动 |
| 11 VS Code 可用性 | 启动进程内懒缓存可用路径；识别 App Paths、安装目录和 code.cmd 对应的 Code.exe；不可用时隐藏动作 | 安装 VS Code 后需重启 BetterCM 刷新缓存；不经 cmd.exe 传递用户文件参数 |
| 12 无障碍 / 语言 | 每级菜单动作使用 MenuItemButton peer，提供 MenuItem 角色、PositionInSet、SizeOfSet、勾选/选中/禁用状态；新增 peer 测试；README 明确仅简体中文 | 未运行 Accessibility Insights / 屏幕阅读器实测；未引入 en-US 或全量 resx |
| 13 诊断 | 设置可导出 ZIP；环境、DPI、配置白名单和近期日志摘要；不复制私有路径/标题/参数/原始消息/堆栈；AppDomain 异常记录 | 原生崩溃仍依赖 Windows 错误报告，不声称托管日志能捕获所有原生崩溃 |
| 14 安装器 | x64os 与支持范围一致，版权元数据，条件启用签名卸载器；默认输出按版本归档 | 无可核对的项目网址，不虚构 AppPublisherURL；保留现有 PrepareToInstall 退出/互斥体逻辑，避免 AppMutex 在正常退出逻辑前直接阻断升级 |
| 15 小问题 | 失效 spare 连接在复用前丢弃；async void 动作/键盘入口局部处理异常；IDE/测试产物忽略规则 | 隔离安装演练额外发现 probe 报告临时文件占用，已加入有限重试并补回归测试 |

## 验证与制品

具体结果见 [QA-1.8.0](../releases/1.8.0/QA-1.8.0.md)。本地内部安装包由隔离的干净候选源码副本构建，源副本提交只用于验证，不属于主仓库提交。候选源 bundle、载荷清单和测试证据保存在 artifacts/qa。

## 操作示例

```powershell
./verify.ps1
./verify.ps1 -SkipDesktopChecks
./Maintenance.ps1
./Maintenance.ps1 -Apply -KeepRecent 3
./Build-Installer.ps1 -RequireSigning -CertificateThumbprint <thumbprint> -SignToolPath <absolute-signtool.exe>
```

依赖调整后用 `dotnet restore BetterCM.slnx --force-evaluate` 更新锁文件，审查 lock diff 再提交；CI 使用 locked mode，不自动接受漂移。

## 上游依据

- [Inno 架构标识说明](https://jrsoftware.org/ishelp/topic_archidentifiers.htm)：x64os 只匹配 x64 Windows。
- [Inno SignTool](https://jrsoftware.org/ishelp/topic_setup_signtool.htm) 与 [SignedUninstaller](https://jrsoftware.org/ishelp/topic_setup_signeduninstaller.htm)：同一签名入口覆盖 Setup 与卸载器。
- [Windows App Paths 注册](https://learn.microsoft.com/en-us/windows/win32/shell/app-registration)：程序注册位置与解析机制。
- [NuGet PackageReference 和锁文件](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)：locked restore 的约束。


## 1.8.0 复查修复（本轮）

- 签名证据改由 Inno 回调保存，覆盖临时 uninst.e32.tmp / e64，而非等待安装后的 unins000.exe；14 项脚本回归验证回调文件处理、发布门禁和报告脱敏等行为。真实签名成功路径明确为 BlockedNoCertificate。
- 主仓库提交和本地 annotated tag 替代隔离候选 bundle 作为本轮构建源；Build-Installer 仍拒绝脏树。payload、安装报告和内部包由本轮主仓库产物重新生成。
- README 的当前版本引用统一为 1.8.0，CI 描述涵盖锁文件、漏洞审计、安装器编译与可选桌面作业；删除空的 Unreleased 段。
- 自定义动作默认 WorkingDirectory 为程序目录；UseTargetWorkingDirectory 默认 false，编辑窗口提供显式开关；{dir} 参数仍按目标展开。已有 schema 2 缺字段时也默认 false。
- 旧 bundled 默认动作在解析程序名之前清理，不删除用户配置中的动作；恢复备份序列化迁移后的 Current，保留 .bak 原文及损坏副本。
- Maintenance 纳入未跟踪 .gitattributes/.gitignore/.editorconfig；记录实际归档/移除体积；可显式加入旧 clean-build-fixture；已有历史备份继续保留。
- dependency-audit 的项目路径改为相对路径；Release 安装门禁强制最终包和对应 payload/HEAD 一致，Internal 报告必须显式选择。

此处以前的表格记录初轮实施边界；本轮结果以 [当前 QA](../releases/1.8.0/QA-1.8.0.md) 和新生成的报告为准。
