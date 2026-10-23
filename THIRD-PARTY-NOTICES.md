# Third-party notices

The Windows installer includes Simplified Chinese messages maintained by Zhenghan Yang (Kira), from https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation. The original language file header is preserved in Installer/ChineseSimplified.isl.

MIT License

Copyright (c) 2019 - 2020 kirakira

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

The self-contained distribution also includes Microsoft .NET runtime files. The packaging script copies their license and notice files from the exact restored runtime package versions into the distribution, using DotNet- and WindowsDesktop- filename prefixes.

The menu system backdrop uses Microsoft Windows App SDK InteractiveExperiences and Base components. Their native composition dependencies are included in the self-contained distribution. The packaging script copies the license files from the exact restored Microsoft.WindowsAppSDK component packages using their package names as prefixes. Test-only xUnit and Microsoft.NET.Test.Sdk packages are not distributed with the application.


## BetterCM icon provenance

从 v1.9.0 起，`Assets/BetterCM.ico` 已替换为本仓库原创的几何图标（菜单面板与指针），由 `Assets/Generate-Icon.ps1` 绘制，不使用外部图片、字体或图标库。生成脚本及输出按仓库 MIT 许可证发布，Copyright (c) 2026 BetterCM contributors。脚本可重新生成 16、24、32、48、64、128、256 像素的 32 位图标。

此记录仅适用于 v1.9.0 及以后版本的新图标；历史安装包中的旧图标仍未确认来源。
