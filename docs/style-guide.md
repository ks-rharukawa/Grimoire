# Style Guide

Grimoire の visual / UX 仕様。参照画像（[design/mood-board-v1.png](../design/mood-board-v1.png), [design/combat-screen-v1.png](../design/combat-screen-v1.png)）から抽出した style spec を、Avalonia + SkiaSharp で **procedural** に再現するためのソース・オブ・トゥルース。

> **重要**: 参照画像はあくまで方向性確認用。実アセットは本仕様に従いコード生成する（[`../design/README.md`](../design/README.md) 参照）。

## 1. コンセプト

- **核**: 古代魔導書（spellbook）× pixel art × 技術図
- **世界観**: 呪文ではなく TCP/IP プロトコルが書かれた写本
- **温度感**: 神秘的・学術的・少し畏怖（過度に明るくない、シリアス寄り）

## 2. カラーパレット

| 名前 | HEX | 役割 |
|---|---|---|
| `Midnight` | `#0a0e2e` | 主要背景 |
| `MidnightDeep` | `#050715` | 最深部・影 |
| `Parchment` | `#f4e8c1` | 二次背景（書のページ） |
| `ParchmentAged` | `#d9c896` | パーチメント陰影 |
| `ArcaneGold` | `#d4af37` | アクセント・粒・線・装飾 |
| `ArcaneGoldDim` | `#8a6f1f` | 非活性のアクセント |
| `Crimson` | `#c0392b` | 警告・敵・失敗 |
| `CrimsonDim` | `#6b1c14` | 敵の影 |
| `EtherealCyan` | `#00d4ff` | 健全・成功・プレイヤー側 |
| `EtherealCyanDim` | `#0a6b88` | プレイヤー側陰影 |
| `LimeGreen` | `#3ddc4c` | HP バー |
| `Forest` | `#1a3a1c` | 安定状態・木霊 |

**禁則**: パステル系、ネオン系（深紅シアン以外）、白に近い灰色は使わない（コントラスト維持）。

## 3. タイポグラフィ

- **本文・カード説明**: システムデフォルト等幅寄り（macOS Hiragino Sans / Win Yu Gothic）
- **見出し・タイトル**: 同フォントの bold
- **テキストの anti-alias**: 日本語の可読性を優先し **AA を有効** にする（default のまま）。形状（円・矩形・線）の anti-alias 抑止とは分けて運用する。Latin 専用の見出しや極小 pixel フォント装飾でのみ `TextOptions.SetTextRenderingMode(.., Alias)` を局所適用

### フォント解決の実態（macOS で確認、commit f6917a9 時点）

- `Typeface.Default` = Inter + OS CJK fallback (macOS では Hiragino Sans)
- `new Typeface(new FontFamily("Hiragino Sans, Yu Gothic, Noto Sans CJK JP, sans-serif"), ...)` でも、**macOS では Typeface.Default と表示が同じ**（CJK が同じ Hiragino Sans に解決されるため）
- 違いが出るのは Latin 部分または非 Hiragino フォント (PingFang SC / Comic Sans MS 等) を混ぜたとき
- **コード上の明示指定は Windows (Yu Gothic) / Linux (Noto Sans CJK JP) の互換性のため残す**
- 漢字の見た目を本質的に変えたいときは: (a) Pixel 日本語フォント embed (PixelMplus / k8x12 / Misaki)、(b) 別 CJK フォント embed (Source Han Sans 等)、(c) システムフォントは変えずレイアウト/コントラストで対処、のいずれか。**フォント名指定だけでは macOS の漢字は変わらない**

### Bold weight と CJK

- **14px 以下で Bold weight は CJK の線が密集して潰れる**ことを実測 (commit f6e0f0d → f6917a9 で Bold 撤回)
- カード説明など小サイズの日本語テキストは **Regular + 16px 以上** を default にする
- Bold が必要な場合はサイズを 18px 以上に上げる
- **絶対に使わない**: 装飾フォント全般、Comic Sans 系、ハンドライティング系

## 4. ピクセルアート規律

- **解像度単位**: 1 unit = 2px（仮想ピクセル）。Window 内の全要素はこの倍数で配置する
- **アンチエイリアス**: 形状（円・矩形・線）には**かけない**（`RenderOptions.SetEdgeMode(this, EdgeMode.Aliased)` 相当）
- **発光（glow）**: ピクセルブラーは 2-3 段の同心ピクセルレイヤーで表現（カーネルブラーは使わない）
- **線太さ**: 1px or 2px のみ（fractional pixel 禁止）
- **円**: 半径が小さい場合（≦8px）は手書きパターンで描く（生Skia円は使わない）

## 5. レイアウト（戦闘画面ベース、参考: combat-screen-v1.png）

Window 1280x720（PoC では 900x600、本実装で 1280x720 に拡大）

```
┌──────────────────────────────────────────────────────┐
│ [bookpage-frame-left]                       [right] │
│   ┌──────────────────────────────────────────┐       │
│   │ [Enemy Area]      HP bar    Intent       │       │
│   │   敵 sprite (pixel art)                  │       │
│   ├──────────────────────────────────────────┤       │
│   │ [Stage]                                  │       │
│   │   §5 動く図 (network topology)            │       │
│   │   字幕 caption strip                      │       │
│   ├──────────────────────────────────────────┤       │
│   │ [Player Strip]  HP  Energy  End Turn     │       │
│   ├──────────────────────────────────────────┤       │
│   │ [Hand] 5 cards in fan                    │       │
│   └──────────────────────────────────────────┘       │
└──────────────────────────────────────────────────────┘
```

**寸法 (1280x720 想定):**
- Book frame margin: 80px (left, right)
- Enemy area: y=40 〜 200 (高さ 160)
- Stage: y=220 〜 440 (高さ 220)
- Player strip: y=460 〜 510 (高さ 50)
- Hand: y=530 〜 700 (高さ 170, fan layout)

## 6. カード仕様

| 属性 | 値 |
|---|---|
| 標準サイズ | 160 x 220 px |
| 枠の太さ | 3px |
| 枠色 | `ArcaneGold` (active) / `ArcaneGoldDim` (inactive) |
| 背景 | `Midnight` (上半分) → `Parchment` (下半分の説明欄) |
| 名前位置 | 上中央 (y=12) |
| コスト | 右上 (gem icon, ArcaneGold) |
| アイコン | 中央 (64x64 px の pixel art) |
| 効果テキスト | 下半分の中央寄せ、12px |
| fan layout の角度 | 中央 0°、端 ±10° |

## 7. 動く図（§5 動く粒）

- **粒のサイズ**: 4x4 px 〜 8x8 px
- **線**: 1px-2px の ArcaneGold、必要なら点線（pixel-step）
- **流れ速度**: 60fps で 3px/frame (=180px/sec)
- **詰まり表現**: 粒が連続して停止、後続が積み重なる（visual queue）
- **遮断表現**: 粒が ×印に変化（fade out）

## 8. 字幕（caption strip）

- **位置**: Stage の最下部内側、高さ 32px のストリップ
- **背景**: `Parchment` に半透明 `Midnight` オーバーレイ
- **テキストプレフィックス**: `> ` （プロンプト風）
- **テキストスタイル**: 12px regular、`Parchment` 上に `Midnight` 文字
- **長さ**: 1行に収める（複数状態を `|` で区切る）

## 9. アニメーション原則

- **60fps 駆動**: `DispatcherTimer` で 16ms ティック
- **イージング**: 線形のみ（pixel art 流儀）
- **トランジション**: 状態変化は 100-200ms（短い）
- **発光のパルス**: 1.5秒周期で透明度 70%-100% を sin で振る

## 10. アクセシビリティ（[`quiz-cadence.md`](quiz-cadence.md) 連動）

- 時間制限のあるカードはタグ表示（`⏱` icon）
- 時間制限なしモードでは `⏱` icon を消す（visual cue）
- カラーで状態を区別する場合は必ず形状でも区別（赤色盲対応）
- 字幕は最低 14px、明確なコントラスト

## 11. アセット生成ガイドライン

- **AI 画像生成は禁止**（直接の game asset としては）。`design/` の参照画像は target のみ
- カード icon は SkiaSharp の primitive shapes で構成（円・矩形・線の組み合わせ）
- 敵 sprite はピクセル単位の手書き配列（後続のコンテンツ作成タスクで詰める）
- 装飾フィリグリーは SVG path or Skia パスで描画

## 試作で検証すべきこと（実装時に確定）

- フォントの実描画品質（Hiragino Sans の pixel size での読みやすさ）
- 1280x720 が本当に必要か、900x600 で十分か
- fan layout のカード回転が pixel art を破壊しないか
- 60fps が古い Mac でも維持できるか（最低 30fps を許容ラインに）
