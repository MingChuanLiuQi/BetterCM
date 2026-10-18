# BetterCM 1.5.1 验证记录

日期：2026-10-15；本机 Windows 11 build 26300、x64。此次撤回 1.5.0 的二次菜单固定流程，移除开始／任务栏固定和取消固定入口。

## 修改前保存

源码保存于带注释标签 `backup-before-1.5.1`，提交 `8d6796c8d1aec418d4495de420ab689b3211391a`；1.5.0 安装包保留于根目录及 `artifacts/releases/1.5.0`，两份 SHA256 均为 `67E4580D08C47019EEBA62528BAAB6478FC9175FB59F06A93A03828E9D3C9B76`。本轮基线见 `artifacts/qa/baseline-1.5.1.json`。没有批量删除历史备份或安装包。

## 发布身份

- 编译源码：`e6b524c4ab46169c05599030121adf4e65cd84aa`，程序集构建标识 `1.5.1+e6b524c4ab46169c05599030121adf4e65cd84aa`。
- 正式安装包：根目录 `BetterCM-1.5.1-Setup-x64.exe`，61,218,202 字节。
- SHA256：`907CFBB32851B3CBE7245AD63B4E97DA289144BB97DDECCE831F4A63CBF5148A`。
- 隔离 QA 安装包 SHA256：`FFC42B451ACE94517D2F049C0909D3D0AA1C93D3C23AFE3008DBCF344908F58B`。
- `artifacts/qa/payload-1.5.1.json` 包含 503 个发布文件的大小、SHA256、SourceCommit。正式安装包、校验文件和清单另存 `artifacts/releases/1.5.1`。

## 已完成验证

| 检查 | 结果 | 证据（artifacts/qa 下） |
|---|---|---|
| 零警告构建、发布 | 通过 | Build-Installer.ps1 完整执行 verify.ps1 |
| xUnit 逻辑回归 | 205/205 | headless/headless-tests.trx |
| 发布目录 UI/Shell 自检 | 252/252 | published-tests.json |
| 隔离安装目录 UI/Shell 自检 | 252/252 | installed-tests.json |
| 安装、503 文件哈希、自启、单实例、退出、卸载和配置保护 | 13/13 | installer-smoke.json |
| 请求竞争、断连、超时及原生执行宿主生命周期压力 | 67/67 | stress-1.5.1.json |
| 真实快捷方式的经典紧凑／增强标准菜单读取 | 2/2 | menu-modes-1.5.1.json、live-1.5.1/classic.json、enhanced.json |

相较 1.5.0，增加精确动词回归，明确保留 open、properties、rename、pintohome、unpinfromhome、同名显示文字及带前后缀的其他扩展动词；匹配忽略大小写。真实 `.lnk` 目标为系统 winver.exe，仅创建临时快捷方式，不执行任何固定或取消固定。自检覆盖完整读取、根层按需读取、展开后搜索补全、树形叶子与命令索引对应、普通打开／属性保留，以及旧会话和未知令牌拒绝。原有快捷访问取消固定检查仍通过。

252 项 UI/Shell 自检继续包括 48 项三级材质检查、真实剪切复制粘贴、原生参数传递、动画、布局及请求生命周期检查。两种模式的实际产品报告中均无四种受保护规范动词，保留 open/properties，报告原生亚克力可用。此处是实际产品窗口的诊断报告检查，不冒称本轮重新做了逐项人工点击或截图视觉验收。不会执行用户现有程序的固定操作来证明入口被移除。

首轮开发测试发现新增检查的可空引用警告，修正后重新通过构建、205 项测试及最终打包全门禁；交付依据为最终成功结果。

## 环境与范围

日常安装在本轮开始时已是 1.5.0，DLL SHA256 `1A6298FAAA938115EA6192905133C9536C0D0473C2F9A54E0B1658418C7A94B5`。此次用独立 AppId 及独立配置目录完成安装验证，正式安装包留给用户原目录升级。用户配置 SHA256 `6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6` 和 BetterCM 自启值与基线一致；日常后台已重新启动，最终校验见 `environment-restored-1.5.1.json`，汇总见 `release-1.5.1.json`。

只排除已识别的四种规范动词，不根据标题猜测第三方动作的用途。系统自身 HMENU 未被修改，原 Shift+右键入口保持既有实现。未更改开始菜单数据库、权限、安全设置或用户的既有固定状态。安装包仍未使用发行者代码签名；验证范围为本机环境，不声明所有 Windows 版本或扩展组合都已测试。
