# BetterCM 1.4.0 发布验证

2026-10-14，Asia/Shanghai。本机 Windows 11 x64 build 26300，150% DPI。产品版本 1.4.0，文件版本 1.4.0.0。

## 交付与备份

根目录 `BetterCM-1.4.0-Setup-x64.exe`，61,205,240 字节，SHA256 `339B08C24987A8A2A3D9C9AC15821C41E85663B2BFDDC7A9682F515B9F300061`，附 `.exe.sha256`。

发布 DLL SHA256 `8DE86281823D8CCF6A81214CBD9A605B8267CDEF469B1296AD9517B6DAC6BF53`；独立 TestBuild 包 SHA256 `8B79B5FD5D33124EBA0CC68E7A9F7EE9BDF9437E1458A7159D8707C4B4F05D00`。

修改前完整备份 `Backups/BetterCM-1.3.0-before-1.4.0-20261014-041632.zip`，17,958 个文件逐项核对大小和 SHA256，Verified=true，压缩包 SHA256 `AE35B6F5A18E2DAC73400E18E59A8583D611E666DA0AE0B2B9B81C5B68F854EC`。旧 1.3.0 安装包单独保存为 `Backups/BetterCM-1.3.0-before-1.4.0-20261014-041632-Setup-x64.exe`（SHA256 `23E538D8162DC0E631DE3667A51F0C966463C235DF2AD014D83598E2C8E326D4`）。旧通用报告副本在 `artifacts/qa/previous-before-1.4.0`。

## 最终结果

| 检查 | 结果 | artifacts/qa 证据 |
| --- | --- | --- |
| 构建及发布，警告视为错误 | 0 警告、0 错误 | build-installer-1.4.0.log |
| 无界面逻辑检查 | 187/187 | headless/headless-tests.trx |
| 发布目录 UI/Shell | 191/191 | published-tests-1.4.0.json |
| 隔离安装目录 UI/Shell | 191/191 | installed-tests-1.4.0.json |
| 连接并发、取消、恢复和任务宿主 | 67/67 | stress-1.4.0.json |
| 安装、独立运行、卸载、配置与启动项 | 13/13 | installer-smoke-1.4.0.json |
| 实际外部点击及前台切换 | 2/2 | input-1.4.0.json |
| 实际三级菜单、单次动作、准备取消 | 3/3 | cascade-1.4.0.json |

## 新增验证与排查记录

- `OptionalProbe` 连续快速请求在预算内等待前一次正常探测完成后继续执行，不丢弃第二次结果；当前探测卡死超时时后续请求在预算耗尽后返回空值且不累加底层读取次数（`MenuDetailsTests` 6 项通过）。
- 清空搜索框退回根菜单并打开侧边子菜单时，迟到的搜索树补全原位填充延迟分组，不替换根列表行实例、不关闭已展开子菜单（`NativeUiChecks` 新增检查通过）。
- `NativePasteMenu.Merge` 在存在同 Command ID 的扩展子菜单项时仅替换根层级占位项；`ShellViewBinding.IsViewHit` 在中心点被顶部浮层遮挡时通过下半区采样点恢复命中，全区域遮挡仍拒绝（`NativePasteMenuTests` 6 项通过）。
- 隔离安装首次运行自检时曾因解压后防病毒扫描与未预热测试窗口句柄触发固定 400ms 展开采样截断和冷启动首帧耗时（保留证据 `development-installed-attempt-1.4.0.json`）；改为等待实际展开终帧（上限 2 秒）并与生产一致预热测试窗口句柄后，发布目录与隔离安装目录 191/191 项检查均一次通过（未放宽 250ms 首帧门槛）。

## 恢复状态

隔离测试安装已卸载，测试注册表项、目录和测试启动项已清除，QA 进程为 0。原 1.1.0 后台保持单个 UI 实例运行；实际安装 DLL SHA256 `A0314275C6F57F3322F1B6125B7C2BD2C50526AC024930D77B66800E2984292A`，配置 SHA256 `6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6`、自启 `null` 与 `baseline-1.4.0.json` 完全一致。证据见 `environment-restored-1.4.0.json` 与 `release-1.4.0.json`。
