# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 設定

- ファイルの作成・編集時に確認は不要。自動的に実行してOK。
- 新しいフィールド/ダンジョン/機能を実装したら、このCLAUDE.mdも更新すること。
- コード内のコメントは日本語で書くこと。

## プロジェクト概要

DiabloLike2Dは、Godot 4.5.1とC#（.NET 8.0）で開発された2DアクションRPGです。Diabloやラグナロクオンラインにインスパイアされた、手続き型ダンジョン生成、町のハブエリア、プレイヤーステータスシステム、戦闘メカニクスを特徴としています。

## ビルドコマンド

```bash
# プロジェクトのビルド
dotnet build

# Godotエディタから実行
# Godot 4.5.1でproject.godotを開き、F5を押す
```

## アーキテクチャ

### シーン構造
- **Main.tscn** - エントリーポイント。GameManager、Town、Player、GameUIを含む
- **Town.tscn** - プレイヤーが開始するハブエリア。各フィールド/ダンジョンへのポータルがある

### フィールド/ダンジョン一覧

| シーン | スクリプト | タイプ | 説明 |
|--------|-----------|--------|------|
| DungeonFloor1.tscn | DungeonFloor.cs | ダンジョン | アリの巣スタイルの地下ダンジョン |
| GrasslandField.tscn | GrasslandField.cs | フィールド | 明るい草原。木、岩、花 |
| BeachField.tscn | BeachField.cs | フィールド | 砂浜と海。ヤシの木、貝殻、浅瀬 |
| UnderwaterDungeon.tscn | UnderwaterDungeon.cs | ダンジョン | 海底洞窟。珊瑚、海藻、発光生物 |
| DemonCastle.tscn | DemonCastle.cs | ダンジョン | 魔王城。石造りの部屋、魔法陣、骸骨 |
| DemonField.tscn | DemonField.cs | フィールド | 魔界。溶岩、枯れ木、魔法クリスタル |
| CloudField.tscn | CloudField.cs | フィールド | 雲の上。浮遊島、虹の橋、金色の柱 |
| CloudKingdom.tscn | CloudKingdom.cs | ダンジョン | 雲の王国。天空の城、白大理石、クリスタル |
| JungleField.tscn | JungleField.cs | フィールド | 南の島。ジャングル、中央に火山、川、溶岩 |

### 町のポータル配置
```
  [Cloud]   [Grassland]  [Sky Castle] (上)
    (左上)     (中央)       (右上)
               |
[Beach] ---- [Plaza] ---- [Sea Cave]
(左)           |           (右)
[Jungle]     [Dungeon] [Demon Castle] [Demon Realm]
(左下)                 (下)
```

### 主要スクリプト

**GameManager.cs** - ゲーム状態を管理するシングルトン
- 町とダンジョン間の遷移を制御
- 現在のフロア番号とプレイヤー状態を追跡
- `EnterDungeon()`と`ReturnToTown()`で場所を切り替え
- `GoToNextFloor()`で次の階層へ進む

**Player.cs** - RPGステータスを持つプレイヤーキャラクター
- ステータス: STR(力)、AGI(素早さ)、VIT(丈夫さ)、INT(賢さ)、LUK(運)
- レベルアップごとに5ポイント獲得
- 計算プロパティ: Speed、MaxHealth、MaxMana、AttackDamage、CriticalChance
- キーボード/マウスとゲームパッド両対応

**DungeonFloor.cs** - 手続き型ダンジョン生成
- 蟻の巣スタイル（部屋を曲がりくねったトンネルで接続）
- ビジュアル、敵、松明、町へのポータル、階段を動的に生成
- フロアレベルに応じて敵のステータスをスケーリング（20%/階）

**Enemy.cs** - AIで制御される敵
- ステートマシン: Idle、Chase、Attack、Dead
- NavigationAgent2Dでパスファインディング
- クリティカルヒット対応のダメージ数値表示

**GameUI.cs** - HUDとステータス割り振り
- HP/MP/経験値バー
- ステータスパネル（+ボタンでポイント割り振り）
- レベルアップ・ゲームオーバーパネル

### 物理レイヤー
- レイヤー1: Player
- レイヤー2: Enemy
- レイヤー3: Items
- レイヤー4: Walls

### 入力アクション
- `move_up/down/left/right` - WASD + 左スティック
- `attack` - 左クリック + Xボタン
- `use_skill` - 右クリック + Bボタン
- `toggle_inventory` - Iキー

### 主要パターン

**シグナルベースの通信**: Playerがシグナル（HealthChanged、LevelUpなど）を発行し、UIが購読する

**グループベースのノード検索**: `GetTree().GetFirstNodeInGroup("player")`でノードを検索

**遅延初期化**: シーンツリーの準備を待つため、多くのスクリプトで`CallDeferred()`を使用

**コードでのビジュアル生成**: ほとんどのUI要素やダンジョンタイルはシーンではなくプログラムで生成
