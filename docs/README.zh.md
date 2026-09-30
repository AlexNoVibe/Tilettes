# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · **中文 (简体)** · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

适用于 Windows 的快速启动面板：磁贴网格，支持快捷方式、文件夹与标签页，内置模糊搜索和带控制台的迷你资源管理器。单个便携 EXE，无需安装，.NET Framework 4.8 (WinForms)。

当前版本：**v0.5** — 下载：[Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [更新日志（英文）](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md#changelog)。状态：**beta**。

## 特性

- 磁贴 1×1…6×6，数量不限的可拖动标签页，文件夹可在标签页内或弹窗打开，从资源管理器拖放，自定义网格，图标缩放。
- 模糊搜索覆盖名称、元数据、路径与描述，可纠正错误键盘布局（`руддщ` → `hello`）。
- 迷你资源管理器：面包屑导航、书签、文件搜索和内置 `cmd.exe` 控制台。
- 按文件类型的图标和“打开方式”规则，支持导入/导出。
- 托盘图标、开机自启、全局热键、原生资源管理器菜单、无边框可缩放窗口。
- 一次性欢迎窗口，通过 GitHub Releases 检查更新并在角落显示更新按钮。

## 首次启动与更新

- 欢迎窗口只出现一次（beta 说明、语言选择、是否允许检查更新、示例磁贴），可在设置中再次打开。
- 更新检查每 N 天询问一次 GitHub Releases（默认 3 天）——严格征得用户同意；有新版本时，设置按钮旁会出现绿色的“更新”角标。“立即检查”为手动检查。

## 捐助

如果 Tilettes 对你有所帮助，可以用加密货币支持开发。EVM 兼容网络共用一个地址：

<a name="donate-evm"></a>
### EVM — Ethereum · Polygon · Base · Monad · HyperEVM

```
0xf84897FA0b74083c16865315A5b148f4d92e6C2a
```

<a name="donate-btc"></a>
### Bitcoin (BTC)

```
bc1qu9cf5uqc5wxqwde8mk378xwdlnjatvmhxhvat5
```

<a name="donate-sol"></a>
### Solana (SOL)

```
7ffCFnJBNVaF268FsZGKBPEWe3UNrWbasgt3aidiCw68
```

<a name="donate-sui"></a>
### Sui (SUI)

```
0x3ca194b355bb00a1f5f646786407ebbcdaee361c6f56fb92f8df9abd73b0c3b1
```

其他帮助方式：在 [Issues](https://github.com/AlexNoVibe/Tilettes/issues) 反馈错误与建议、给仓库点星、向朋友推荐 Tilettes。

## 构建

```
build.bat
```

只需带 .NET Framework 4.x 的任意 Windows —— 编译器是系统自带的。发布版本由 GitHub Actions 在每个 `v*` 标签上自动创建，仅包含源码压缩包（workflow 也会验证编译）；exe 请自行用 `build.bat` 构建。

完整文档：[**README.md**](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) (English) · [README.ru.md](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) (Русский)
