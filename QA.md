# BetterCM 当前验证记录

当前迭代为 **1.7.0**。完整结果以 [QA-1.7.0.md](QA-1.7.0.md) 为准，建议取舍见 [REVIEW-1.7.0.md](REVIEW-1.7.0.md)，交接见 [HANDOFF-1.7.0.md](HANDOFF-1.7.0.md)。历史版本记录保留，不将过去的检查数量计入本轮。

正式安装包位于项目根目录 BetterCM-1.7.0-Setup-x64.exe，同目录提供 SHA256 校验文件。Build-Installer.ps1 要求源码已提交且工作区干净，依次构建、测试、发布和 UI/Shell 自检；通过后才编译安装包。Installer/Expected-Checks.json 是已审阅的自检名称清单，发布和安装后都必须精确匹配。

GitHub Actions 只执行无需交互桌面的部分；本机 UI/Shell、压力和安装测试分别生成报告。远端 CI、混合 DPI、多语言、ARM64 和所有第三方扩展组合均不据本机通过结果推定为已验证。安装包未签名，未执行用户日常安装的卸载演练。
