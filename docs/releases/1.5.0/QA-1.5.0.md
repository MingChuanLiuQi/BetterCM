# BetterCM 1.5.0 发布验证

2026-10-14，Asia/Shanghai。Windows 11 x64 build 26300，150% DPI；.NET SDK 10.0.401。程序集版本 1.5.0，文件版本 1.5.0.0。

## 交付与来源

- 项目根目录 `BetterCM-1.5.0-Setup-x64.exe`，61,231,677 字节。
- SHA256：`67E4580D08C47019EEBA62528BAAB6478FC9175FB59F06A93A03828E9D3C9B76`，附同名 `.exe.sha256`。
- 发布 DLL SHA256：`1A6298FAAA938115EA6192905133C9536C0D0473C2F9A54E0B1658418C7A94B5`。
- 独立 QA 安装包 SHA256：`C04CC991B8C51EE14B15342432473543B7EFF84F89A5B409977F56FD14926E75`。
- 编译源码提交：`ca5d9084959b1917dc32491f9b8f07fc1745ad5e`；完整构建标识 `1.5.0+ca5d9084959b1917dc32491f9b8f07fc1745ad5e`。末次文档/校验文件提交不改变已验证二进制。
- `artifacts/qa/payload-1.5.0.json` 记录 503 个发布文件的相对路径、大小和 SHA256、SourceCommit 与安装包哈希。

## 备份

修改前新建本地 Git，将原 157 个受忽略规则保护的源码/文档文件提交为 `dc08a296c71f3cab60f33a97d9d37fcc3c30fbe9`，标记 `v1.4.0`。原安装包保存在 `artifacts/releases/1.4.0/BetterCM-1.4.0-Setup-x64.exe`，SHA256 `339B08C24987A8A2A3D9C9AC15821C41E85663B2BFDDC7A9682F515B9F300061`；备份清单 `artifacts/qa/backup-1.5.0.json` 为 Verified=true。历史 Backups、根目录旧安装包均保留。此前通用报告副本在 `artifacts/qa/previous-before-1.5.0`。

## 最终结果

| 检查 | 结果 | artifacts/qa 证据 |
| --- | --- | --- |
| 警告视为错误的 Release 构建 | 0 警告、0 错误 | Build-Installer.ps1 的 verify 门禁 |
| 无界面逻辑 | 199/199，通过，无跳过 | headless/headless-tests.trx |
| 自带运行时发布自检 | 239/239，通过 | published-tests.json |
| 隔离安装后自检 | 239/239，通过 | installed-tests.json |
| 压力/故障/宿主生命周期 | 67/67，通过 | stress-1.5.0.json |
| 隔离安装、版本、运行时、503 文件哈希、自启与卸载恢复 | 13/13，通过 | installer-smoke.json |
| 真实外部鼠标与前台切换 | 2/2，通过 | input-1.5.0.json |
| 真实三级侧边点击、单次执行及准备期取消 | 3/3，通过 | cascade-1.5.0.json |
| 彩色条纹背景下的三级交互复核 | 3/3，通过 | cascade-pattern-1.5.0.json |
| 实际原生固定→取消固定→恢复状态 | 通过；需要系统菜单确认 | native-confirmation-1.5.0.json、live-1.5.0/pin-success.json、unpin-success.json |
| 原日常安装、配置、自启和后台进程恢复 | 通过 | environment-restored-1.5.0.json |

239 项自检包含新增的 48 项三级材质矩阵：Acrylic/Mica/纯色 × 深浅主题 × 标准/紧凑，以及关闭透明/高对比度回退。检查复用 HWND 的控制器、背景 alpha、实际内容覆盖；Windows 的宿主背景属性 getter 在本机不支持，改核对每级 setter 成功和控制器状态。新的布局检查使用有界条件等待。报告明确所需交互桌面、Explorer、剪贴板和本机 Shell 扩展环境，没有把缺环境时跳过的项目计为通过。

## 实机视觉与原生固定

独立测试背景仅绘制彩色条纹，真实产品窗口在其上显示。主菜单可见条纹经过模糊，三级叶子菜单位于测试宿主边缘时能看到红色背景经材质透出；覆盖不活动测试宿主的部分采样其系统回退背景，因此颜色更灰。这是不同实际背景，不以三个窗口 RGB 必须相同作为材质一致性判据。截图 `acrylic-root-pattern-1.5.0.jpg`、`acrylic-side-pattern-1.5.0.jpg`、`acrylic-deep-pattern-1.5.0.jpg`。

使用复制的独立测试程序及 `.lnk`，通过 BetterCM 的固定入口交接到 Explorer 菜单，手动点击同一项。原生动词实际由 PinToStartScreen 变为 UnpinFromStartScreen，再通过对应入口取消固定并恢复 PinToStartScreen。测试中的任务栏固定项亦经真实系统菜单撤销。未修改用户现有应用的固定项。

固定类入口右侧标注“系统确认”，详情说明需要在 Windows 菜单选择同一项。它修复了无效入口的流程，**尚未实现跳过 Windows 确认的一键固定**。不会把一个 COM 的 S_OK 当成固定成功，也不伪装 Explorer 进程身份、修改开始菜单数据库或使用模拟按键。

## 发现与修正

新增矩阵初版把 set-only 的 DWMWA_USE_HOSTBACKDROPBRUSH 当作可读属性，产生无效测试失败；查询返回 E_INVALIDARG，检查改用 setter 与真实呈现。矩阵也发现了真实缺陷：紧凑一行子菜单重复 Show 时，WPF 系统最小追踪尺寸把原生窗口由 53 加到 56 个物理像素，而内容层仍为 53；现在显示前的测量计入 SM_CYMINTRACK，全部覆盖检查通过。

固定入口的普通 COM、菜单代理与 FolderItemVerb 尝试均被系统拒绝；视图动词的 S_OK 不能证明目标操作发生，相关失败实验已删除。尝试阶段日志保留在隔离测试目录。本轮测试曾因运行中的开发 apphost 锁定输出导致构建失败，停止对应测试进程后重建；最终安装包走完整门禁，不使用失败构建作为交付依据。

## 环境与边界

原日常安装仍为 1.4.0，DLL SHA256 `8DE86281823D8CCF6A81214CBD9A605B8267CDEF469B1296AD9517B6DAC6BF53`；用户配置 SHA256 `6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6`、BetterCM 自启值均与修改前一致。日常主进程已重新后台启动，QA 主进程和 QA 注册项已清除。用户运行根目录安装包即可原目录升级。

安装包未使用发行者代码签名；本轮不声明远端 CI、ARM64、所有 Windows 构建和第三方扩展组合已经验证。系统关闭透明、高对比度或其他系统材质限制可能触发预期不透明回退。历史备份未批量清理，工作区体积没有被宣称降至 1 GB。

正式 Build-Installer 的当前 GUID 临时目录已按脚本清理。独立 QA 编译暂存目录 `artifacts/qa/installer-stage-1.5.0` 已核对仅有测试安装包且与保存副本哈希一致；工具自动审批拒绝其删除（仅返回 blocked by policy），因此保留这一临时副本，不以其他执行方式绕过拒绝。这不影响安装包或验证结果。
