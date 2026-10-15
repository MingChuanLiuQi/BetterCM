# BetterCM 1.5.1 处理取舍

用户指出 1.5.0 的“点 BetterCM 固定 → 在 Windows 菜单再选同一项”不可接受，并明确允许移除不能可靠实现的功能。本轮采纳这一产品边界，撤回此前将交接菜单描述为固定功能修复的处理。它没有解决 BetterCM 入口本身无法直接执行的问题。

## 实施

- 删除开始／任务栏固定和取消固定四种规范动词的导入入口，统一在 ShellMenuSession.ReadMenu 排除。经典、增强、完整读取、延迟展开及搜索补全均经过这里。既不返回无效果的成功，也不弹出另一个菜单让用户再选。
- 删除 NativeSelectionCommand、RequiresNativeConfirmation、ShowSelectionContextMenu、专用 WM_CONTEXTMENU 常量及“系统确认”提示，恢复普通命令的宿主生命周期路径。
- 只匹配 PinToStartScreen、UnpinFromStartScreen、taskbarpin、taskbarunpin（忽略大小写）；不按显示标题模糊过滤，不移除 Home／快捷访问固定，不调整 BetterCM 自身一级菜单偏好。
- 保留原生 HMENU，只不将这些项放入 BetterCM 的索引和树；其余项继续使用原始命令偏移。拒绝执行不属于当前索引的命令。Windows 自身菜单仍可通过 Shift+右键访问。

## 不采用的方向

不继续尝试进程身份伪装、开始数据库或注册表手工写入、模拟按键、反复调用失败 COM 动词，也不要求用户提升权限。1.5.0 的本机实验已经表明普通 IContextMenu/FolderItemVerb 被拒绝，某些接口返回成功不能证明实际固定；这不能扩展为对所有 Windows 构建或第三方接口的绝对结论，但足以支持移除不可靠入口。此次没有新增与缺陷无关的功能或重构。

## 回归范围

精确动词检查保留 open、properties、rename、pintohome、unpinfromhome、同名文字及带前后缀的其他扩展动词。真实快捷方式与文件／桌面菜单验证完整读取、按需展开、搜索补全、树和命令索引一致、普通打开／属性保留以及过期请求拒绝。材质与真实文件传输沿用完整发布和隔离安装门禁。
