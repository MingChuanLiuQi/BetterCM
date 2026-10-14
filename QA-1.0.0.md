# BetterCM 1.0.0 验证记录

2026-10-13，Asia/Shanghai。本机 Windows 11 x64 build 26300，150% DPI。

## 交付及备份

正式安装包在根目录：BetterCM-1.0.0-Setup-x64.exe，61,169,360 字节，文件版本 1.0.0.0。SHA256：`5BD7A91756CE8714A9A5859104A31CBD071799C9CC1A81E523728778F0CB2BCB`，附 .exe.sha256。包含 .NET/WPF 和 Windows App SDK 材质组件。中间构建 3159510F… 已被最终包替代，不能用于交付校验。

修改前完整备份 Backups/BetterCM-0.9.0-before-1.0.0-20261013-135415.zip，9870 文件，3,538,436,185 字节。所有归档条目逐一比较大小和 SHA256，附 manifest.json 和 sha256。压缩包 SHA256：`78B59A15225B620E8FE05BCC10F82B70E690D20693FC6F8C740A72313820F42A`。历史包和备份保留。

## 冻结代码验证

| 检查 | 结果 | artifacts/qa 证据 |
| --- | --- | --- |
| 构建、发布，警告视为错误 | 0 警告、0 错误 | build-installer-1.0.0.log |
| 无界面 xUnit | 140/140 | headless/headless-tests.trx |
| 最终发布 WPF/Shell/设置 | 123/123 | published-tests.json |
| 最终隔离安装 WPF/Shell/设置 | 123/123 | installed-tests.json |
| 压力、IPC、捕获、日志并发 | 59/59 | published-stress-1.0.0.json |
| 隔离安装及卸载 | 13/13 | installer-smoke.json |
| 外部点击及前台切换 | 2/2 | input-1.0.0.json |
| 多级侧边动作及准备期间取消 | 3/3 | cascade-input-1.0.0.json |

正式包通过 Build-Installer.ps1 默认完整门禁。最后父 HWND 输入法修改后重新构建、发布和打包，再编译独立 AppId QA 包，安装后所有发布文件逐一 SHA256 比较通过，123 项自检通过。installer-smoke.json 的 FinalSetupSHA256 是正式包，SetupSHA256 是独立 QA 包，不混用。

真实普通背景刷新 invoke 往返 6.83ms，清理后宿主总共 737.09ms 退出，新增阈值分别 1.5s / 2.5s。0.9.0 的响应本来在 finally 清理之前，不能将本轮结果误写成消除所有命令响应的固定 3 秒阻塞；修正的是无关动作在新建服务清理阶段的额外空等。

新增回归：深层搜索路径及直接命令、空组过滤、全路径稳定 ID、父禁用传播、保留原父组启用状态、打印加速键/规范动词回退、默认 Enter、设置构造态、配置/启动项回滚、图标 LRU/过期/路径隔离、高对比度色板往返、父 HWND 无 IME 关联。跨进程日志夹具三个宿主并发追加 36 条，全部保留。复制/移动只使用自检新建夹具，恢复自检读取的剪贴板快照。

首次外部输入检查与安装端桌面自检阶段重叠，外部点击断言失败、前台检查通过；原报告保留为 input-1.0.0-overlap-attempt.json。安装检查结束后独立重跑，两项通过；多级侧边检查随后独立执行通过。以后不要并行运行多个会改变前台窗口的桌面测试。

## 实机操作

使用 computer-use，在独立 BETTERCM_DATA_DIR 测试。前四项在最后父 HWND 输入法修正前的发布构建验证，最后三项用 dev-ime 构建验证；该构建 BetterCM.dll 与最终发布哈希一致：`94F4A50A0C7EA6DB4E5E0C32602FF627308F5C09FC763D3901D3B1C9AC180F81`。

1. 搜索 SHA-256，直接出现 NanaZip · SHA-256，无组箭头及二次展开。证据 search-direct-1.0.0.png。
2. 测试文件保持选中时，右键固定下载导航项，正确显示下载、资源管理器导航栏、物理路径和原生取消固定；未改变用户固定状态。证据 navigation-with-selection-1.0.0.png/.json。
3. 在文件菜单的动作区域按 Ctrl+C，菜单关闭并执行原生复制。
4. 进入专用 paste-target 背景菜单按 Ctrl+V，生成 clipboard-fixture.txt；核对源仍存在、源目标 SHA256 相同。键日志 SearchFocus=false，排除仅复制/粘贴搜索文字。
5. 保持拼音输入状态按 W，正确打开新建(W) 侧边菜单；修正前同一场景出现中文候选，修正后键日志为 W，非 ImeProcessed。
6. 原生新建文本文档、文件夹后均直接进入名称编辑，分别捕获 selected 回调 flags=0 / 3。证据 new-text-rename-1.0.0.png、new-folder-rename-1.0.0.png。测试文件位于 artifacts/qa/native-live-1.0.0/paste-target，未操作用户文件。
7. 聚焦搜索框后按 W，仍出现拼音中文候选，证明父窗口隔离没有禁用搜索输入法。证据 search-ime-1.0.0.png。

最终发布程序另通过上述真实外部输入及侧边输入夹具。开发 native-live 的键/新建日志含中间失败和成功修正，不将整个历史日志当作最终单次结果。

## 环境及范围

已恢复真实安装 0.9.0.0 后台。配置 SHA256 仍为 `6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6`，登录启动项为空，隔离安装卸载；见 environment-restored-1.0.0.json。未自动覆盖真实安装；根正式包供升级。

未清空用户回收站、删除用户文件或修改辅助功能/安全设置。高对比度检查使用生产色板入口的受控参数，不改系统开关。设置回滚使用临时配置和模拟启动项异常，不改用户注册表。保留经典接口边界：无文字自绘/仅新式 Win11 菜单接口扩展、Explorer 自加导航树控制项、多设备混合 DPI/远程桌面矩阵未完整覆盖。Shift+右键保留系统入口。

1.0.0 为此次正式发布版本，安装包尚未使用发行者代码签名。CI 已明确分离无界面和本地桌面门禁，远端 CI 未执行。审查取舍见 REVIEW-1.0.0.md。
