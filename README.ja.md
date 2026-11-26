# ComCon

[![build](https://github.com/akihiro/ComCon/actions/workflows/build.yml/badge.svg)](https://github.com/akihiro/ComCon/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/akihiro/ComCon?include_prereleases)](https://github.com/akihiro/ComCon/releases/latest)
[![license](https://img.shields.io/github/license/akihiro/ComCon)](LICENSE)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/akihiro/ComCon/badge)](https://scorecard.dev/viewer/?uri=github.com/akihiro/ComCon)

[English](README.md)

ComCon は Windows Terminal 用のシリアル/COM ポート接続ツールです。

キー入力を VT シーケンスのままシリアルポートへ送り、受信したデータをそのまま Windows Terminal に書き出します。接続先（Linux コンソールなど）の矢印キー、色、カーソル移動は、普通の端末と同じように動作します。

## 動作環境

- Windows 10 バージョン 1809 以降（Windows Terminal を推奨）
- .NET Framework 4.8.1

## インストール

1. [最新リリース](https://github.com/akihiro/ComCon/releases/latest)から `ComCon.exe` をダウンロードします。
2. 任意のフォルダ（例: `C:\Tools`）に置きます。どこからでも `ComCon` で起動したい場合は、そのフォルダを `PATH` に追加してください。

ComCon はインストーラーのない単一ファイルです。翻訳とデバッグシンボルは exe に埋め込まれています。

各リリースは GitHub Actions でビルドされ、SHA-256 チェックサムと署名付きのビルド来歴（provenance）証明が添付されます。ダウンロードしたファイルがこのリポジトリからビルドされたものか確認するには、次を実行します（[GitHub CLI](https://cli.github.com/) が必要です）。

```shell
gh attestation verify ComCon.exe --repo akihiro/ComCon
```

実行ファイルはコード署名されていません。初回起動時に Windows SmartScreen が「Windows によって PC が保護されました」と表示することがあります。その場合は **詳細情報** をクリックし、**実行** を選んでください。あらかじめファイルのプロパティ画面でブロックを解除するか、PowerShell で `Unblock-File C:\Tools\ComCon.exe` を実行しておくこともできます。

## 使い方

```console
ComCon --list                    使用可能なポートを一覧表示
ComCon COM3                      COM3 に 115200 8N1 で接続
ComCon COM3 -b 9600 -p even      9600 8E1 で接続
ComCon 3 -f rtscts -n crlf       RTS/CTS フロー制御を使い、Enter で CRLF を送信
ComCon COM3 -r                   抜き差しやデバイスのリセット後に自動で再接続
ComCon COM3 --recv-newline lf    改行が LF だけのデバイスの出力を正しく表示
```

すべてのオプションは `ComCon --help` で、バージョンは `ComCon --version` で確認できます。

メッセージは Windows の表示言語で表示されます。英語と日本語に対応しており、それ以外の言語では英語になります。`--lang en` または `--lang ja` で切り替えられます。

接続中は `Ctrl+]` に続けて次のキーを押すと、ローカルコマンドを実行できます。

| キー | 動作 |
| --- | ------ |
| `q` | 終了 |
| `b` | ブレーク信号を送信 |
| `e` | ローカルエコーのオン/オフ |
| `?` | ヘルプを表示 |
| `Ctrl+]` | `Ctrl+]`（0x1D）そのものを送信 |

`Ctrl+C` では ComCon は終了しません。0x03 として接続先に送られます。

標準入力または標準出力がリダイレクトされている場合、ComCon は標準入力の内容をポートへ送り、入力の終わりで終了します。受信データは生のバイト列として書き出されます。

## Windows Terminal のプロファイル

新しいタブから直接接続するには、`settings.json` の `profiles.list` に次のようなプロファイルを追加します。

```json
{
    "name": "COM3 (115200)",
    "commandline": "C:\Tools\ComCon.exe COM3 -b 115200",
    "icon": "\uE703"
}
```

## ビルド

```shell
dotnet build -c Release
```

出力は単一ファイル `bin\Release\net481\ComCon.exe` です。翻訳とデバッグシンボルが埋め込まれているので、このファイル1つをコピーするだけでインストールできます。

## コントリビューション

バグ報告、翻訳、プルリクエストを歓迎します。詳しくは [CONTRIBUTING.md](CONTRIBUTING.md)（英語）を参照してください。セキュリティ上の問題は、[SECURITY.md](SECURITY.md) の手順に従って非公開で報告してください。

## ライセンス

ComCon は [Apache License 2.0](LICENSE) のもとで公開されています。
