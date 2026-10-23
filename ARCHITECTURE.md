# BetterCM 当前架构

当前源码为 1.9.0。版本由 Version.props 集中定义；本轮为未提交候选工作树，历史发行记录不代表当前版本已发布。

```mermaid
flowchart LR
  Input[InputService / 热键与右键] --> App[App / 生命周期与编排]
  App --> Context[ExplorerContextService / 独立 context host]
  App --> Shell[ShellBridge / 独立 shell host]
  Shell --> Native[ShellMenuSession / COM 与会话]
  App --> Menu[ContextMenuWindow / 菜单呈现]
  Menu --> Executor[ActionExecutor / 动作执行]
  App --> Settings[MainWindow / 设置]
  Settings --> Persistence[SettingsPersistence / 事务与回滚]
  Persistence --> Store[ConfigStore / 原子写入]
  Watcher[ConfigurationWatcher / 文件变动合并] --> App
  Settings --> Diagnostics[DiagnosticExporter / 脱敏诊断包]
```

## 关键契约

- 主进程不加载第三方 Shell 扩展；context host 与 shell host 由有限生命周期的管道管理。Shell 会话、命令 ID 和 Explorer 视图一一绑定，旧上下文不得执行新目标。
- `_current`、`_spare` 和进行中的执行连接分别持有所有权；失败预热的关闭连接不能交给下一次菜单请求。超时不强杀正在处理系统操作的宿主。
- `MenuPresentationRequest` 承载呈现参数，生产入口使用具名字段；旧位置参数仅供内部夹具兼容。菜单窗口分出 Keyboard、NativeMenu、Submenus partial，各级菜单共用根生命周期。
- 程序名仅在设置时从绝对 PATH 项或 App Paths 解析。执行只接受现有 `.exe` / `.com` 的绝对路径，菜单构建时通过 CustomActionPresentation 检查自定义程序是否存在，缺失时禁用并显示原因，保留配置供介质恢复后重用。自定义动作默认使用程序目录作为 WorkingDirectory，只有显式启用 UseTargetWorkingDirectory 时才使用目标目录。内置系统程序使用系统目录；VS Code 可用性在进程中缓存。脚本应显式配置解释器及参数。
- 配置 schema 当前为 2。`ConfigMigrations` 按版本逐级迁移：v1 可解析程序名转成绝对路径，无法解析的动作保留并禁用；迁移仅在内存执行，显式保存后才更新磁盘并保留 `.bak`。未来版本和损坏配置拒绝保存，原文及损坏副本保留。
- 配置写入使用同目录临时文件、磁盘刷新和原子替换；保存前检测外部编辑。设置与自启项保持已有回滚语义。`ConfigurationWatcher` 合并高频文件事件并在 UI 线程重载；Error 事件记录异常、重建 watcher，失败后以两秒间隔重试。配置菜单快照采用手写深拷贝，保持集合比较器并隔离嵌套列表。
- UI 动作事件局部记录异常；未知全局 UI 异常仍暂停右键接管。AppDomain 事件补充记录未处理托管异常；原生崩溃仍依赖 Windows 错误报告，不能宣称已捕获所有原生故障。
- 诊断包使用字段白名单，不复制原配置、异常消息或堆栈；菜单 UIA peer 提供菜单项角色、位置、总数及选中/勾选/禁用状态说明，仍需屏幕阅读器实测。
- NuGet 使用集中版本与锁文件，CI 以 locked mode 恢复并审计传递依赖。UI/Shell 门禁比较稳定 ID，显示名映射只提供辅助提示。
- 安装器发布仍要求干净提交和程序集提交标识匹配；签名为可选能力，`-RequireSigning` 强制签名并验证信任链和时间戳。跳过桌面验证的产物仅放入 `artifacts/internal`。

历史逐版设计说明见 [architecture history](docs/history/architecture.md)。工程操作与未完成的外部验证见 [审查实施记录](docs/current/REVIEW-IMPLEMENTATION.md)。

## 生产与 QA 边界

- 默认 Release 不编译 `Services/*Checks.cs`、`SelfTests.cs`、`App.Diagnostics.cs`。Debug/QA 配置定义 QA 常量，测试项目显式引用 QA 配置，输出分目录。
- `StartupRouter` 限定生产参数；QA 参数路由集中在 App.Diagnostics partial。键盘记录、Shell 回调跟踪、性能钩子和 QA 自启项覆盖均以 QA 条件编译；正式发布后扫描程序集类型和 QA 字符串，并实际检查诊断入口返回码。
- `ActionCatalog.Ordered` 一次索引配置，保持首个重复 ID 优先和稳定排序；执行器按动作 ID 字典查找。参数占位符只遍历模板，插入的路径不参与第二次展开。
- Explorer/桌面请求在读取剪贴板前判别窗口类；只有非 Explorer 回退分支读取剪贴板。上下文 COM 捕获继续由独立进程和 1800 ms 预算保护。
- 自定义逐个启动使用独立 STA、并发度 1、进度与启动前取消检查，已启动进程不因取消而终止。
- 实例名称规则集中在 AppInstance；所有数据目录实例共持有安装占用标记，安装器检测自定义数据目录实例并要求正常退出。此标记不取代原有单实例锁。
- Shell 原生操作宿主仍等待活跃 UI/引用结束，15 秒后降低到每秒检查。没有采用强制最长存活时间，避免终止长时间文件操作。
- CI 安装阶段可复用验证回执；回执必须对应干净提交、版本、检查范围、唯一且完整的文件集合与所有哈希。

本轮建议的完成和延后范围见 [1.9.0 迭代记录](docs/releases/1.9.0/REVIEW-1.9.0.md)。

## 1.9.0 复查后的构建和维护策略

解决方案显式声明 Debug/Release/QA。Release 只构建产品；Debug/QA 构建测试。测试项目直接构建时仍固定引用 QA 产物。CodeQL 使用产品项目，verify 同时检查三个解决方案配置和 QA/Release 测试项目，避免项目级通过掩盖解决方案失败。

归档清理仅使用运行开始时的归档清单，在新归档创建之前执行；本次新归档不受清理预算影响，超额留待后续运行评估。BackupRoot 必须位于工作区外，不能包含工作区。历史 release/QA 资产继续保留，不自动按容量删除。

## 第三轮门禁与 watcher

NativeUi/C037 分离首次与稳定状态测量：首次一次、稳定五次，验证真正 Hold、有效正耗时、完成呈现与解除遮蔽，再计算中位数和 nearest-rank P95。预算分别为首次 <1000 ms、稳定中位数 <250 ms、P95 <500 ms；五次的 P95 即最大值。QA 数学辅助和测试仍排除在 Release 程序集中，生产只增加当前呈现是否真正 Hold 的观测标记，无动画重开明确返回 0 ms 并标记未 Hold。

ConfigurationWatcher 在构造期间创建失败时也安排两秒重试，实例持续存在；运行期恢复及退出竞态处理保持。ScriptFixture 分类同时匹配 fixture/storage 的失败目录，共享保留预算和归档清理规则。最新证据见第三轮稳定性修正记录。
