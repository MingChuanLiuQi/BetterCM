# 桌面挂件兼容性调查（待实机反馈）

日期：2026-10-15。当前正式版本仍为 1.6.0；本文件记录下一补丁的调查，不代表问题已确认解决，也不是正式发布说明。

## 用户报告与已确认事实

- 一言挂件有时在 BetterCM 菜单显示期间整句消失，菜单关闭即恢复。用户补充云母、亚克力和纯色均会出现，因此不能通过换材质作为解决方案。
- 修改前保存标签 `backup-before-1.6.1`，指向 `6ef4b592f00d72edfa15cb780d067a32cf800f1b`。1.6.0 原安装包保留，SHA256 为 `0E97BDB61FB20C73F1F32587F71563784FDD3842E8C3099EDCC208509B97E569`。日常环境基线：`artifacts/qa/baseline-1.6.1.json`。
- 本机一言为 0.7.0，使用 Windows 11 Progman 下的分层 GDI 子窗口。在分别显示三种材质、包含菜单与挂件区域重叠的有限测试中，其窗口保持 visible、未被 DWM cloak，父窗口与桌面兄弟窗口顺序未变化；挂件自身诊断也未进入全屏暂停。
- 上述采样没有捕捉到用户所述的偶发现象。可见状态和窗口顺序不等于屏幕文字实际仍被合成，因此不能据此宣称问题已经复现、排除或根治。

## 当前候选改动

`ContextMenuWindow.SourceInitialized` 为每个根菜单和级联菜单 HWND 加上 `WS_EX_TOOLWINDOW`，清除 `WS_EX_APPWINDOW`。此前只有 `ShowInTaskbar=false`，没有显式工具窗口类型。保留既有 WPF 窗口框架、输入焦点、首帧呈现和系统材质行为。

这是针对窗口分类的兼容性调整，**尚没有证据证明缺少该标志就是挂件消失的根因**。微软对工具窗口的定义仅支持“不进入任务栏和 Alt+Tab”等窗口语义，不能当作它必然修复桌面合成问题的依据：[Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)。

没有修改一言源码、安装文件、用户配置或全屏暂停选项，没有对外部窗口发送显示、隐藏、重绘或重新排序操作，也没有通过禁用菜单焦点绕过问题。

曾试验直接改成 `WS_POPUP` 并去掉 caption，但自检进程异常退出（`0xC000041D`，未生成报告），已完全撤销该试验。最终候选只调整上述扩展样式，不包含 popup/caption 基本样式改动。

## 已完成验证

- Release 构建：0 警告、0 错误。
- 逻辑测试：205/205，`artifacts/qa/widget-1.6.1/headless/widget-candidate.trx`。
- 开发版 UI/Shell 自检：263/263，`artifacts/qa/widget-1.6.1/development-tests.json`。新增根菜单、侧边菜单原生工具窗口类型检查；原有搜索、材质、动画、布局、Shell 操作检查保持通过。
- 压力检查：67/67，`artifacts/qa/widget-1.6.1/stress-tests.json`。
- 三材质桌面观测及一言诊断：`artifacts/qa/widget-1.6.1/composition-*.json`、`widget-overlap-samples.json`。仅为状态证据，不是视觉验收证据。临时诊断夹具源码保存在同目录，诊断命令没有加入正式程序入口。

## 当前实机试用及下一步

已于 21:42 启动独立试用副本（具体 PID、路径见 `artifacts/qa/widget-1.6.1/live-candidate-session.json`），使用从日常配置复制的独立数据目录 `live-user-data`。原日常程序已正常退出，原安装文件、配置及启动项没有被替换；一言原实例持续运行。试用副本仍显示 1.6.0，因为尚未发布下一版本。

已请用户在桌面不同位置反复右键并打开二级菜单，等待“不再消失 / 仍会消失 / 暂时无法测试”的反馈。当前不能记录为已修复，也没有生成新的正式安装包。

若反馈仍消失，继续对比实际触发期间的前台窗口、扩展边界和桌面合成；不能仅凭自检全绿继续宣称修复。若反馈有效，再完成最终补丁版本号、发布和隔离安装验证、安装包及交接记录。测试结束或停止试用时，向试用副本发送带同一 `BETTERCM_DATA_DIR` 的 `--shutdown`，再恢复原日常安装；不要同时运行两份右键接管程序。
