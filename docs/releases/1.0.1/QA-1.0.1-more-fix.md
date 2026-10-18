# BetterCM 1.0.1 更多项目闪烁修复重打包验证

开发与打包：2026-10-13；最终交互、安装验证与环境恢复：2026-10-14，Asia/Shanghai。Windows 11 x64 build 26300，150% DPI。按用户要求，版本号仍为 1.0.1，文件版本 1.0.1.0；用 SHA256 区分同版本构建。

## 交付身份与修改前备份

根目录 `BetterCM-1.0.1-Setup-x64.exe`：61,170,433 字节。SHA256 `D358CEAB8E897C6968F337103E983D934BF4343707EB51A469B41DE6FF2586B8`，附 `.exe.sha256`。发布 DLL SHA256 `05045065E47D1C3D53BAEB898BECC5BCF6CFDC6FE92787004E268B73AA77C6CB`。

完整备份 `Backups/BetterCM-1.0.1-before-more-fix-20261013-214105.zip`：16981 文件逐一验证大小及 SHA256，Verified=true，压缩包 SHA256 `C0112AAAB9D993F8891ADD57F1CCC177CAC07BEBF5753D5156CE3EE9801FA28E`。同目录另存旧安装包 `BetterCM-1.0.1-before-more-fix-20261013-214105-Setup-x64.exe`（45D4DA6F…）。备份、manifest 和历史交付均保留。旧通用测试报告另存 `artifacts/qa/previous-1.0.1-before-more-fix`。

## 冻结构建结果

| 检查 | 结果 | artifacts/qa 报告 |
| --- | --- | --- |
| build/publish，警告视为错误 | 0 警告、0 错误 | build-installer-more-fix-final-1.0.1.log |
| xUnit | 157/157 | headless/headless-tests.trx |
| 发布构建 UI/Shell | 170/170 | published-tests.json |
| 隔离安装目录 UI/Shell | 170/170 | installed-tests.json |
| 压力与后端边界检查 | 61/61 | stress-more-fix-1.0.1.json |
| 安装、运行、卸载及保留配置 | 13/13 | installer-smoke.json |
| 外部左键及前台切换 | 2/2 | input-more-fix-1.0.1.json |
| 三级侧边点击及取消延迟准备 | 3/3 | cascade-more-fix-1.0.1.json |

隔离 TestBuild 安装包 SHA256 `336F2519291004C64DCBB15D3092861EE5DA1FB330B65124FD2B7EACB1E3E51C`。它使用独立 AppId、产品名和启动项，不替换用户安装；安装测试逐项比对全部发布文件 SHA256，且记录正式包 D358CEAB…的身份。

## 重点证据与范围

- fade、slide、fade-slide 的逐帧检查同时观察父窗口高度、固定内容窗口尺寸、行透明度和位移：不回缩、不先亮后暗，终点没有第二次内容重排。
- 工作区底部向上展开逐帧检查透明内容与父窗口 client origin 一致，底栏位于窗口内。原先实际采样偏移 3–213 px 的问题已加入检查；渲染同步期间的嵌套 Rendering 不被当作完成帧，也不能触发嵌套几何更新。
- 快速重开、展开中搜索/关闭、长列表到最后一行的滚动、复用行透明度及动作身份继续检查。取消无动画及紧凑模式仍保持其原行为。
- 延迟分组 A/B 的真实异步组件夹具检查 A 过期返回不能抢开 B；B 加载中再点击保持焦点且不重复查询。侧边和紧凑根菜单不能进入隐藏搜索。
- 设置配置外部更新后保留编辑、禁用保存，修改有效快捷键不能绕过冲突；直接触发保存事件也不落盘；还原最新已保存值后恢复保存。
- 文本 + Preferred DropEffect 不算文件载荷，Unicode/ANSI 虚拟文件描述符仍识别。真实 Shell 复制/剪切后跨宿主粘贴继续通过。回收站只读取原生菜单及清空入口状态，没有清空用户回收站。
- 实机增强模式更多展开、底部对齐和完整滚动区域复核。最终截图 `more-expanded-final.jpg`、观察状态 `more-fix-final-live.json`：Height/ActualHeight/SurfaceHeight=640 DIP，内容及父窗口高 960 px，底栏留在工作区。
- 增强模式“在资源管理器中定位”实机以 `带 空格 & 中文.txt` 验证：Explorer 打开正确父目录、状态栏选中 1 个项目、34 字节；截图 `reveal-live.jpg`。调用使用原生 PIDL 选择接口，没有 Explorer 参数引用猜测。

两份只读审查的逐条核实、采纳理由和未采纳项见 REVIEW-1.0.1-more-fix.md。没有采纳原生任务或窗口超时强退、吞掉视图校验后继续执行的建议；不能声称所有第三方扩展、离线网络、混合 DPI、远程桌面或 Explorer 多标签行为均已全面验证。本轮也未重新测量此前的性能百分比。

## 环境恢复与接手

隔离 QA 安装已卸载，交互/压力/预览测试进程已结束。恢复用户当前安装的 1.0.1 后台，其 DLL 是前轮布局性能构建 `4167C8D1…`；没有将测试安装冒充实际升级。用户配置 SHA256 `6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6`，原启动项 null，恢复前后相同。最终机器状态与交付汇总见 `environment-restored-more-fix-1.0.1.json` 和 `release-more-fix-1.0.1.json`。

中间开发报告有捕获到问题的失败样本，保留作诊断；最终冻结以本文件首段哈希、published/installed 报告及安装一致性报告为准。
