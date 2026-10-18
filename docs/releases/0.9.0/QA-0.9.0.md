# BetterCM 0.9.0 验证记录

2026-10-13，Asia/Shanghai。本机 Windows 11 x64 build 26300，实体界面为 150% DPI。

## 交付与备份

正式安装包：项目根目录 `BetterCM-0.9.0-Setup-x64.exe`，61,171,838 字节，文件版本 0.9.0.0。SHA256：`D3987CEACE09BD7C51DBE84220A291D92330E6DA63200AA29AF3CC82391AA3AE`。旁有 `.exe.sha256`。包含 .NET/WPF 和 Windows App SDK 材质组件，由 Build-Installer.ps1 / verify.ps1 构建验证后打包。

修改前备份：`Backups/BetterCM-0.8.0-before-0.9.0-20261013-024440.zip`，2,050,789,973 字节，3165 个文件逐一比较大小及 SHA256 通过；附清单及哈希。压缩包 SHA256：`07E3D3077DEC4FF84B90350FE41FF9947EA6B5BDA9D35CB38E64E6DFD1B393A5`。历史安装包及备份保留。

## 最终检查

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Release 构建、发布，警告视为错误 | 0 警告、0 错误 | artifacts/qa/build-installer-0.9.0.log |
| 无界面 xUnit | 119/119 | artifacts/qa/headless/headless-tests.trx |
| 最终发布 WPF / Shell / 设置 | 116/116 | artifacts/qa/published-tests.json |
| 隔离安装 WPF / Shell / 设置 | 116/116 | artifacts/qa/installed-tests.json |
| IPC / 慢宿主 / 捕获超时压力 | 58/58 | artifacts/qa/published-stress-0.9.0.json |
| 隔离安装与卸载 | 13/13 | artifacts/qa/installer-smoke.json |
| 外部点击及前台切换真实输入 | 2/2 | artifacts/qa/input-0.9.0.json |
| 多级侧边菜单及准备期间取消真实输入 | 3/3 | artifacts/qa/cascade-input-0.9.0.json |

安装检查使用独立 AppId 的 QA 包，安装文件与正式发布逐一 SHA256 比较通过；检查启动、单实例、退出、卸载、配置与启动项保护。installer-smoke.json 的 SetupSHA256 属于 QA 包，FinalSetupSHA256 才是正式包。

新增回归覆盖原生树顺序、分隔线、禁用/勾选/默认状态、访问键/快捷键、相同命令的多个显示别名、层级搜索、默认经典配置、Home 固定项目命令身份、动画终点实际帧状态、DWM 禁用系统过渡的设置 HRESULT、菜单焦点和搜索 IME。保留真实复制粘贴、剪切跨宿主粘贴、原生视图刷新及回收站命令读取检查。

动画检查采集实际 WPF 渲染帧，测量高度及完成后至少 180ms 的内容终态。压力夹具的受控 40 秒命令期间第二次 open 为 36.03 ms；压力检查在最终焦点/IME 调整前执行，之后 IPC 和捕获代码未变。最终完整构建、发布和安装自检重新执行。受控压力数据不代表任意第三方扩展耗时保证。

## 实机证据

使用 computer-use，在独立 BETTERCM_DATA_DIR 中测试，真实安装配置未改。

1. 当前固定“下载”导航项正确绑定物理路径及 Home 固定身份，菜单显示原生“从快速访问取消固定”，保留原生顺序和分组，无内置路径格式等额外注入。证据：`artifacts/qa/downloads-classic-0.9.0.png`、`live-downloads-classic-0.9.0.json`。没有更改用户固定列表。
2. 本机原版经典文件夹菜单也有 QQ 音乐两项；来源是 `HKCR\Directory\shell\QQMusic.1.Play` 和 `QQMusic.2.Add`。这些项目由 QQ 音乐注册，经典模式忠实保留。
3. 在专用 `artifacts/qa/native-new-0.9.0` 目录，通过原生“新建 → 文本文档”创建后直接进入名称编辑，提交 `0.9.0原生新建验证.txt`。证据：`new-text-rename-0.9.0.png`。该项在较早开发构建实测，后续保持同一新建回调路径。
4. 激活菜单后按原生访问键 W 打开新建，创建文件夹后直接进入名称编辑，提交 `0.9.0文件夹验证`。证据：`new-folder-rename-0.9.0.png`。dev-key 构建与最终发布 BetterCM.dll 的 SHA256 完全相同：`32C365386E25D18C8C94C0BB0F998DBE90D998B1F5C86F10FA2CAB437F26797B`。
5. 对测试文本文档，激活菜单后 Alt+Enter 正常打开 Windows 属性对话框，名称、位置、类型、大小正确，未修改属性。证据：`native-properties-0.9.0.png`，同样使用与最终发布 DLL 同哈希构建。验证的是激活后的键盘路由，不代表所有 Windows 前台限制下都能自动获得焦点。
6. 最终发布程序的真实输入夹具验证外部点击送达按钮、前台切换关闭菜单；进入两层侧边菜单后点击叶子，执行一次并关闭整个链；取消准备中的请求后旧菜单不晚到。报告见上表。

`.new.jsonl` 和 `.keys.jsonl` 开发诊断记录含中间失败和后续修正；整份历史日志不能作为最终单次成功结果。最终结论以上述报告、截图和冻结构建为准。

## 范围与环境恢复

默认 ClassicMenu=true，按经典 IContextMenu/IContextMenu2/3 树呈现并执行原命令；旧增强配置保留，需手动关闭经典模式才参与合成。全部一级项目一次显示，无先打开更多再二次展开；原生子菜单向侧边打开，小尺寸模式关闭搜索并保留分组。

新建通过 INewMenuClient 的真实绝对 PIDL 回调，让原 IShellView 选择并编辑对象，不用磁盘轮询、猜文件名或模拟 F2；已实测文本和文件夹。视图位置或选区变化拒绝旧上下文。

未清空用户回收站、删除用户文件、执行管理员命令或更改固定列表。回收站只检查原命令和启用状态。无文字自绘项目、仅新式 Windows 11 接口的扩展及 Explorer 自己添加的导航树控制项仍有接口边界，保留 Shift+右键系统入口。单机实测不替代混合 DPI、多标签、远程桌面和多硬件矩阵。安装包未使用发行者代码签名，远端 CI 未运行。

测试结束已恢复原安装 0.8.0.0 后台，配置 SHA256 仍为 `6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6`，登录启动项仍为空，隔离安装卸载。证据：`artifacts/qa/environment-restored-0.9.0.json`。未覆盖真实安装，根正式安装包供用户升级。
