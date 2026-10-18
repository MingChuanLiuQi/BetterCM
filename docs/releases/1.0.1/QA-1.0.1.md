# BetterCM 1.0.1 验证记录

本文件记录前一轮构建。最新同版本布局与性能重打包见 QA-1.0.1-layout-perf.md；本文件提及的通用测试报告已另存 artifacts/qa/previous-1.0.1-before-layout-perf，当前通用报告属于最新构建。

2026-10-13，Asia/Shanghai，Windows 11 x64 build 26300，150% DPI。

## 交付与备份

正式包：根目录 BetterCM-1.0.1-Setup-x64.exe，61,188,549 字节，版本 1.0.1.0。SHA256：`EEEABB2C16C8EC7122708A029415E161E6EF26C19ED39D03431FDA9B99853C55`，附 .exe.sha256。自带 .NET/WPF 和材质运行组件。未自动覆盖当前真实安装。

修改前完整备份：Backups/BetterCM-1.0.0-before-1.0.1-20261013-145131.zip。10442 文件、原始数据 3,875,610,641 字节，逐个核对大小及 SHA256，manifest 的 Verified=true。压缩包 SHA256：`796A9D369A4F0F471EC209D18E82A5552BB7F5C5B8DD31A7C0F775EFB81C165D`。历史包和备份保留。

## 冻结发布验证

| 检查 | 结果 | artifacts/qa 证据 |
| --- | --- | --- |
| build / publish，警告视为错误 | 0 警告、0 错误 | build-installer-1.0.1.log |
| xUnit，无界面 | 146/146 | headless/headless-tests.trx |
| 最终发布 UI/Shell/设置 | 130/130 | published-tests.json |
| 最终隔离安装 UI/Shell/设置 | 130/130 | installed-tests.json |
| 压力、IPC、捕获、日志及启动反馈 | 61/61 | published-stress-1.0.1.json |
| 隔离安装、文件哈希、单实例及卸载 | 13/13 | installer-smoke.json |
| 未激活菜单外部点击及前台切换 | 2/2 | input-1.0.1.json |
| 多级侧边真实点击及准备请求取消 | 3/3 | cascade-input-1.0.1.json |

最终 BetterCM.dll SHA256：`3277247BF56C3DC1BA5A2AC8A20227252EE65A78F93CF2B5E087008257685CF4`。隔离安装全部发布文件逐个比对一致。GUI 检查串行执行，避免相互改变前台窗口。

QA 安装包首次直接编译到 artifacts/qa 时 Inno 返回 EndUpdateResource 110；随后改用独立新建 staging 目录正常编译，再复制 QA 包。正式包已一次正常编译，未关闭杀毒或改安全设置。失败与重试编译日志均保留。

## 问题定位和新增检查

1. 辅助 GUI 进程启动未指定 STARTF_FORCEOFFFEEDBACK，可能触发系统启动光标反馈。现在上下文宿主和 Shell 宿主均带 0x80；压力检查从真实子进程读 GetStartupInfoW 验证，并确认未用 STARTF_USESHOWWINDOW 隐藏后续属性窗口。参考 [STARTUPINFO](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfoa)。
2. 重用材质父窗口和 WPF 子窗口时旧位图可能先于准备好的打开状态显示。现在主菜单和侧边菜单等待首帧后呈现。测试验证解除 DWM cloak、有界准备、动画原生尺寸稳定、终点不回跳，以及首帧前取消、无动画重开不会重新显示旧菜单。
3. 1.0.0 实机选中文件夹的 Pebrel 已能打开，空白处实际调用先暴露 GIT RCW 跨 STA/RPC 的 E_NOINTERFACE。修复后进一步复验出现 0x80070483，最终定位背景调用遗漏当前目录，%V 无法解析目标。保留 baseline-background-launch-1.0.1.json 和 baseline-user-1.0.1/BetterCM.log，包含各阶段失败；不能把该历史日志所有条目视为最终构建失败。
4. 新增 xUnit 验证 Windows 引号/反斜杠参数及 COM 服务从 MTA、第二 STA 查询和释放后失效。真实桌面服务另从 MTA 查询 IShellBrowser。
5. 临时 GUID verb 注册到 HKCU 的文件、文件夹及 Directory/Background，通过真实 ShellBridge/IContextMenu 启动有限 argv 探针；中文、空格、& 目标完整，背景 %V 正确。探针只处理本次夹具，finally 清除自己的注册键，并通知关联变更。报告在 fixtures/launch-中文 & 空格-*/ 下。不存在硬编码 Pebrel 路径、自己拆解第三方注册表命令或失败后重复执行。
6. 真实 Shell 复制保留源文件，剪切后跨宿主粘贴确实移动文件并保留内容，读取回收站原生清空入口和启用态；不清空用户回收站。

## 实机操作

computer-use 在独立 BETTERCM_DATA_DIR 下测试。开发 dev4 与最终代码之间只增加真实服务查询空指针断言及测试注释，产品调用/呈现逻辑一致；最终发布另外执行上述真实输入夹具和 Pebrel 背景调用。

- 在 artifacts/qa/launch-fixture-1.0.1/背景 & 目标 的资源管理器背景菜单执行 Pebrel，修复后原生调用成功且 Pebrel 窗口成为前台。最终发布再次执行成功，final-native-launch-1.0.1.json 保存实际菜单和目标。Pebrel 使用既有窗口/进程；未输入终端命令或操作其工作区。路径解析的强断言由三个有限 argv 探针完成。
- 搜索并执行原生新建文本文档，创建后直接出现 Explorer 名称编辑；PIDL callback flags=0/3 转回 STA 后 selected。证据 new-text-rename-1.0.1.jpg、fixed-native-launch-1.0.1.json.new.jsonl。
- 从该测试文件的原生菜单打开属性，名称、路径和类型正确，窗口可见，随后取消关闭。证据 properties-1.0.1.jpg。
- 最终发布多级侧边菜单实际点击叶子，动作执行一次、整条菜单链关闭；外部按钮能取消尚未返回的旧请求。结果见上表。

## 环境恢复及覆盖范围

真实安装 1.0.0.0 已恢复 --background 运行。配置 SHA256 仍为 `6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6`，登录启动项仍为空，隔离安装已经卸载。证据 environment-restored-1.0.1.json。测试资源管理器和属性窗口已关闭，夹具及证据保留。

本轮覆盖公共 Shell 调用、COM 服务及实际文件操作，未逐项执行所有第三方扩展；清空用户回收站、管理员/安全动作没有用于测试。保留经典接口导入、无文字自绘扩展、新式 Win11 专有接口及其他设备/DPI 组合的既有范围。光标修复针对 BetterCM 隐藏宿主的系统启动反馈，不修改全局指针设置。最终安装包仍未使用发行者代码签名。
