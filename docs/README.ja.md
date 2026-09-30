# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · [Português](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pt.md) · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · **日本語**

Windows 用の高速ランチャーパネル：ショートカット・フォルダー・タブをまとめるタイルグリッド、あいまい検索、コンソール付きミニエクスプローラーを内蔵。単一のポータブル EXE、インストール不要、.NET Framework 4.8 (WinForms)。

現在のバージョン：**v0.5** — ダウンロード：[Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [更新履歴（英語）](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md#changelog)。ステータス：**beta**。

## 特徴

- 1×1…6×6 のタイル、無制限にドラッグできるタブ、タブ内またはポップアップで開くフォルダー、エクスプローラーからのドラッグ＆ドロップ、カスタムグリッド、アイコン倍率。
- 名前・メタデータ・パス・説明を横断するあいまい検索、誤キーレイアウトの修正（`руддщ` → `hello`）。
- パンくずナビ、ブックマーク、ファイル検索、内蔵 `cmd.exe` コンソール付きのミニエクスプローラー。
- ファイル種別ごとのアイコンと「アプリで開く」ルール、インポート/エクスポート。
- タスクトレイアイコン、自動起動、グローバルホットキー、エクスプローラー純正メニュー、境界なしのリサイズ可能ウィンドウ。
- 一度だけ表示されるウェルカムウィンドウと、GitHub Releases による更新確認（隅に更新ボタン）。

## 初回起動と更新

- ウェルカムウィンドウは一度だけ表示されます（ベータのお知らせ、言語選択、更新確認の許可、サンプルタイル）。設定から再度表示できます。
- 更新確認は N 日ごと（既定 3 日）に GitHub Releases へ問い合わせます — 必ずユーザーの同意のもとで。新しいバージョンがある場合は設定ボタンの横に緑の「更新」ボタンが表示されます。「今すぐ確認」は手動チェックです。

## 寄付

Tilettes が役に立ったら、暗号通貨で開発を支援できます。EVM 互換ネットワークはアドレスを共有しています：

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

その他の支援方法：[Issues](https://github.com/AlexNoVibe/Tilettes/issues) でのバグ報告と提案、リポジトリへのスター、周りへの紹介。

## ビルド

```
build.bat
```

.NET Framework 4.x を持つ任意の Windows でOK — コンパイラは OS に同梱されています。リリースは `v*` タグごとに GitHub Actions が自動作成します。workflow がビルドを検証し、ポータブル zip（Tilettes.exe + README + LICENSE）を添付します（自動のソースアーカイブもあります）。exe は `build.bat` で自分でもビルドできます。

完全なドキュメント：[**README.md**](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) (English) · [README.ru.md](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) (Русский)
