# BetterCM 1.5.0 交接

本轮编译源码 `ca5d9084959b1917dc32491f9b8f07fc1745ad5e`，发布包为根目录 `BetterCM-1.5.0-Setup-x64.exe`，哈希及验证见 QA-1.5.0.md。修改前源码是 v1.4.0 tag（`dc08a296c71f3cab60f33a97d9d37fcc3c30fbe9`），旧安装包在 artifacts/releases/1.4.0，历史备份保留。现在有本地 Git；不要再把 bin/obj/artifacts/Backups 递归装入新的全量备份，不要未经确认删除旧历史。

## 本轮核心

MenuBackdrop 为每个 HWND 在 SetTarget 前启用宿主背景画刷，Acrylic 使用 Thin；ThemeService 在禁用材质时清理画刷，保留系统回退和固定输入活性。BackdropUiChecks 对已缓存的三层窗口跑 48 项材质与布局检查。短菜单高度纳入当前 DPI 的系统 SM_CYMINTRACK，避免复用 Show 时 WPF 加高但内容未覆盖。

NativeSelectionCommand 按根层级规范动词识别开始/任务栏固定及取消固定。ShellMenuSession 保持普通命令原生执行；这四种受保护入口经 ShellViewBinding 验证选区、激活视图并向真实焦点 HWND 发 WM_CONTEXTMENU，由用户在真实 Windows 菜单完成确认。界面显示“系统确认”并提供详情。仍需多选择一次；勿声称一键固定。失败实验显示 IContextMenu / FolderItemVerb 返回拒绝访问，IFolderView2 返回成功也不能证明固定发生；不要恢复这些无效果路径或伪装进程身份。该交接工作由 Explorer 持有，ShellHost 不以 invoked 保留本进程线程引用；普通原生异步任务的生命周期保护继续存在。

OptionalProbe 只有同目标、同类型在飞任务才能共享；不同文件仍需自己的读取，完成后不缓存大小。取消调用方不取消共享底层任务。TryStartNoWait 的名字表示不排队等待前序任务，非“读取立刻完成”。ConfigJson 统一序列化、Clone 和集合规范化的冲突快照，动作列表顺序不可排序，回滚必须保留原始文本。

AppInfo.Build 保存完整程序集版本；payload-1.5.0.json 记录 SourceCommit 和 503 文件清单。Build-Installer.ps1 成功后只删除当前 GUID 暂存目录，失败输出保留；路径验证必须保持。REVIEW-1.5.0.md 逐项说明建议取舍；ARCHITECTURE.md 包含所有权/生效/作废契约。

## 验证与日常环境

199 xUnit、发布及隔离安装各 239 UI/Shell、67 压力、13 安装卸载、2 外部输入、3 侧边输入、3 彩色背景复核均通过。真实测试副本的固定→取消固定菜单动词变更已记录；原用户固定项未调整。详见 QA-1.5.0.md 与 artifacts/qa 中报告。

日常安装仍保留原 1.4.0 并恢复后台运行，用户配置与 BetterCM 自启值校验一致；没有替用户覆盖日常安装。新安装包已放根目录，直接原目录升级即可。所有测试目录使用 BETTERCM_DATA_DIR 隔离，真实输入必须通过 computer-use 的 sky 工具。--cascade-input-check 可用 BETTERCM_QA_MATERIAL=acrylic 复核实际三级材质；彩色测试背景源码只在 artifacts/qa/visual-background，不参与产品编译或安装包。

后续可单独规划真实更新发布渠道、签名证书、更多进程外自动化、诊断导出及完整无障碍协议；不要虚构下载地址或把大量维护重构混入一个用户缺陷修复版本。
