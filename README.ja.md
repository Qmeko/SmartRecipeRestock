# Smart Recipe Restock

[English](README.md)

製作ノートのレシピを読み、各リテイナーから不足材料を取り出す Dalamud プラグインです。

**SND（Something Need Doing）は不要です。**

## インストール

1. `/xlsettings` を実行し、**試験的機能**タブを開く
2. **カスタムプラグインリポジトリ** に次の URL を追加する:

```
https://raw.githubusercontent.com/Qmeko/DalamudPlugins/refs/heads/main/pluginmaster.json
```

3. `/xlplugins` を実行し、**Smart Recipe Restock** をインストールする

## 使い方

1. 製作ノートを開いてレシピを選ぶ
2. チャットで `/srr` を入力する
3. 「レシピを読み取る」を押す
4. リテイナーベルで一覧を開く
5. 「スタックごと取り出してよい」にチェックを入れる
6. 「全リテイナーから取り出す」を押す

## 注意

- ゲームの仕様で、**1スタック全部**出ます。必要な数だけ、ではありません
- クリスタル（アイテムID 2〜19）は取り出しません
- リテイナー一覧は自分で開いてください
- [Allagan Tools](https://github.com/Critical-Impact/InventoryTools) が入っていると、在庫があるリテイナーだけを開きます。無い場合は全員を順に開いて確認します

## コマンド

| コマンド | 説明 |
| --- | --- |
| `/srr` | 窓を開く / 閉じる |
| `/smartreciperestock` | 同じ |

## 開発者向け

```powershell
.\install-dev.ps1
```

コピー先:

```text
%APPDATA%\XIVLauncher\devPlugins\SmartRecipeRestockHelper\
```
