# BetterCM 0.8.0 项目交接

工作区 C:\Users\zky89\Desktop\文件夹\BetterCM。基线 HANDOFF-0.7.0.md。用户本轮要求：关闭更多折叠后没有二次弹出、小尺寸保留合并分组并悬停侧边展开、普通尺寸不闪烁缩短、除显示更多以外全部分组向侧边展开、修复重新固定的下载导航项识别。先备份，安装包放根目录。

## 实现

App.ShowMenuAsync 先捕获配置与上下文，等待本次 ShellResponse、映射基础原生命令、合成菜单树，再 ShowAt。此前 Show 后将禁用内置占位换成实际 Shell 能力并重排，会删除不支持入口、缩短菜单；ShowMoreItems=false / CompactMenu 时还会触发 GroupExpansionAnimation，形成二次扩展。现在在 Show 前完成尺寸和打开动画初态，系统加载不再触发高度动画。

MenuComposer 只输出真实菜单树，MenuEntry.Children 保存下一层；移除旧的列表内分组展开与 openGroups 分支，相关逻辑测试同步改为树契约。allItems 表示显示非常用一级入口，仍保留分组。搜索仍直接筛选所有叶子，固定项从原分组移出；命令 ID / ShellKey / Execute 不变。

ContextMenuWindow.Submenus.cs 复用主菜单的非分层窗口、透明内容源和材质控制器，每层有一个子窗口。普通及紧凑模式均悬停/点击侧边打开，嵌套继续侧边；MenuPlacement 按物理工作区翻转到左边。切换同层销毁旧分支，搜索/关闭/重开清理；叶子操作立即关闭整个链，避免影响原生对话框或编辑。

MenuObservation 保存主/子 HWND 和各自矩形，输入命中逐个检查，窗口间空隙仍为外部。前台切换到子窗口不误关主菜单；左右键和 Escape 支持层级导航。SubmenuHoverDelayMs 独立设置，默认 180 ms、80–600 ms；沿用 GroupExpansionAnimation/GroupExpansionMs 做淡入或水平滑入，主菜单只有更多采用高度动画。

小尺寸 root 300 DIP、side 280 DIP、12 DIP 字号及紧凑行高，搜索/更多/图标栏隐藏，工具栏动作以文字行进入列表；保留全部分组。普通 root 320 DIP、side 300 DIP。限高 640 DIP 并受工作区约束，超长滚动。

行模板放在实际 HwndSource 下按 DPI 测量，设备像素上取整。列表默认模板隐藏边距会让两行子菜单出现滚动条，改为无隐含边距 ScrollViewer/ItemsPresenter 模板。分组不显示多余提示浮层。CascadeLayout 的诊断将 nint 句柄转成 long，并显式输出矩形字段；新增 JSON 回归避免 QA 记录失败。

InputService.WatchPreparation 使用负代次，在 Shell 读取期间通过新指针输入或前台变更撤销旧准备，防止晚到菜单弹出；App 取消预览、设置、下一请求同步撤销。读取前置会增加慢扩展的首次等待，未加入缓存或改 UI 技术栈。

## 导航项根因

旧代码将 shell:Downloads / shell:Home 等资源管理器协议字符串传给 Shell.Application.NameSpace；本机为 null，因此重新固定后节点未能解析。现在每次枚举主文件夹 ::{F874310E-B6B7-47DC-BC84-B9E6B38F5903} 的 Items 读取顶层固定目录 Path，规范化 UIA 快速访问开始/结束、固定标记和格式字符。下载使用 ::{374DE290-123F-4565-9164-39C4925E467B} 等实际解析路径作为回退。保留歧义拒绝和嵌套层级检查，不缓存旧固定顺序，不改变用户固定列表。

真实 UI 中下载位于快速访问最后一项（结束标记），正确返回 C:\Users\zky89\Downloads，并读取原生菜单。不要声称此轮执行了用户目录的取消固定/重新固定；核对的是当前已重新固定后的节点。

## 验证与交付

修改前备份 Backups/BetterCM-0.7.0-before-0.8.0-20261013-014521.zip，3019 文件逐一核对归档，1,730,451,070 字节，SHA256 C8F0653BAC31665832594B50D9E32BA39F388EF9573A9CD5D0F0A70074C33116，旁有清单和哈希。

0 警告/错误，110 xUnit，最终发布和隔离安装各 102 项自检，压力 58，安装/卸载 13，真实外部输入 2、侧边及准备取消输入 3 项通过。报告位于 artifacts/qa，详见 QA-0.8.0.md / QA.md。最后提示浮层与调试 JSON 调整之后重新完成构建、发布、隔离安装自检；后台 IPC 路径未变。

真实安装基线为 0.7.0.0，配置与启动项保留，结束恢复原 0.7.0 后台。不要声称自动安装了 0.8.0；根目录安装包由用户升级。仍保留旧版本后端原生文件操作/OLE/宿主生命周期；没有对用户回收站执行清空。技术边界沿用经典 IContextMenu 文字叶子，保留 Shift+右键。

主要文件：App.xaml.cs、Views/ContextMenuWindow.xaml/.xaml.cs/.Submenus.cs、Models/MenuEntry.cs、Services/MenuComposer.cs/MenuPlacement.cs/MenuDismissPolicy.cs/InputService.cs/ExplorerContextService.cs/SurfaceAnimator.cs、Config/AppConfig.cs、MainWindow、MenuUiChecks/SettingsUiChecks/SelfTests/CascadeInteractionChecks、Tests/CascadeMenuTests/ShellMappingTests、LogicChecks、README/ARCHITECTURE/CHANGELOG。版本唯一来源 BetterCM.csproj 0.8.0。

## 最终交付核对

正式安装包 BetterCM-0.8.0-Setup-x64.exe，61157774 字节，文件版本 0.8.0.0             。SHA256：6F16E0DA67786E3B064F3ADCC0D1CCBC3A45485DA76FA536A7BCC85A44CA5989。同目录 sha256 文件已生成。真实 0.7.0 后台已恢复，配置、启动项不变，测试安装已卸载。

