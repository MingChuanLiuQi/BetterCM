# 1.9.0 建议处理记录

输入为用户提供的 DeepSeek 静态检查建议。本轮先核对源码，再以当前工作树验证，不直接沿用旧报告中的通过数字。

| 建议 | 本轮处理 |
| --- | --- |
| 备份/产物膨胀 | Backups 和 artifacts/archives 同卷原子迁出项目树；58 个备份文件、57 个旧归档文件全部保留，共 61,435,463,137 字节。其余 releases、QA、bin/obj 继续保留。不是释放磁盘空间，也不是迁出报告中的全部约 67 GB。 |
| 备份根目录、保留预算 | 默认旁置 BetterCM.Storage；支持 BackupRoot/环境变量；baseline/maintenance 纳入类别。显式 PruneArchives 按生成名称白名单控制数量和容量，不清理历史迁移目录、bundle 或源码快照。 |
| QA 混入产品 | 采用建议中的条件编译方案；Release 排除检查类与路由文件，QA 钩子条件编译；测试总是引用 QA，正式载荷和 QA 发布分开；静态和实际入口拒绝门禁均通过。 |
| 三个确定性问题 | 单遍正则替换；偏好一次建表、字典查动作；Explorer/桌面先判窗口类再决定是否读剪贴板。补回归测试。 |
| 图标授权 | 已用原创几何图标替换，保留生成脚本和 MIT 来源记录；不对旧安装包图标追认授权。 |
| 真实签名 | 本机用户证书库没有可用代码签名证书。保留 RequireSigning 强制签名/时间戳/信任链检查；成功演练仍待证书，不能伪造或用测试替身替代。 |
| App/窗口拆分 | 先提取 StartupRouter 与 App.Diagnostics，实例命名集中到 AppInstance；完整 SingleInstanceService、TrayService、MenuCoordinator、SettingsViewModel 延后，避免同时改变菜单时序和绑定模型。 |
| 热路径 | 手写配置深拷贝；原生菜单每层复用缓冲区、共享 HashSet 去重；逐个启动独立 STA 串行运行，可报告进度并取消后续启动。 |
| 生命周期 | watcher Error 重建及重试、hook 失败后下次 Apply 重试、系统主题事件局部捕获已完成。原生宿主 15 秒后降低轮询；拒绝强制终止活跃复制/属性窗口的硬超时方案。 |
| dynamic COM | 保持现有分进程隔离、超时和视图校验；强类型 COM 适配层及逐段耗时测量延后，须结合真实 Explorer 版本验证。UIA 原本已在确认窗口类之后调用。 |
| 设置主题 | 改为 Tag 驱动，新增三种主题在 XAML 选项重排后的 ReadSettings 自检。 |
| 格式/分析/覆盖率 | 全仓 C# whitespace 基线规范化并加入 dotnet format whitespace 门禁、EnforceCodeStyleInBuild；覆盖率与挂起限制已启用。没有一次性开启 Recommended 全集或机械替换静默 catch；输入 hook 的无 IO 契约和 COM 释放容错继续保留。 |
| CI 重复验证 | 同一 Windows 作业复用已验证载荷，提交/版本/检查范围/文件集合/哈希均再检查；9 项回执回归包含匹配成功及拒绝脏源码、越界、重复路径、篡改等场景。远端工作流本轮未触发。 |
| 稳定交互冒烟 | 276 项现有 UI/Shell 体系继续执行，新增主题重排回归；未额外引入 FlaUI/WinAppDriver，也未声称人工焦点、鼠标或 DWM 视觉 E2E 通过。 |
| 版本/文档 | Version.props 单一来源、MSBuild 读取、bump 脚本、当前文档入口及历史索引完成。 |
| 安装器 | 增加 SetupMutex；所有数据目录的 GUI 实例持有共同安装占用标记，保留 Local BetterCM.App 兼容检查；TestBuild 使用 QA 载荷，正式包使用 Release。 |
| 本地化、目录重组、P/Invoke 收敛 | 延后。与本轮确定性缺陷和发布边界相比，收益不足以抵消大量搬迁与 COM 改动的回归面。 |

本轮没有创建 Git 提交、标签或推送。Build-Installer 的干净源码发布门禁保留；候选安装包通过直接 Inno 编译单独保存，候选清单明确标注 UncommittedWorkingTreeCandidate。
