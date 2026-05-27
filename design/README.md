# design/ — Visual Reference Only

このディレクトリの画像は **方向性確認用の参照（mood board / mockup）** であり、**ゲーム本体には組み込まない**。

## なぜ参照のみか

- **pixel grid が厳密でない**: AI 生成は "pixel art風" であって本物の pixel art ではない（拡大すると微妙にアンチエイリアスやサブピクセルが混ざる）
- **再現性なし**: 再生成のたびにブレる
- **プラットフォーム審査リスク**: Steam 等の AI 開示要件・著作権争点を回避
- **個性が出にくい**: 量産感を避け、Grimoire 固有の表現を作る

## 実アセットの方針

- 戦闘画面・カード・敵・UI 要素は **Avalonia + SkiaSharp で procedural 実装**（コード = アセット）
- カラーパレット / 構図 / 余白 / フォント等の **style spec** は [`../docs/style-guide.md`](../docs/style-guide.md) に抽出する
- procedural 実装と mockup を並べて比較・微調整するループで品質を担保

## ファイル一覧

| ファイル | 役割 | 位置づけ |
|---|---|---|
| `mood-board-v1.png` | ビジュアル方向の確認（pixel art × 古代魔導書） | 参照 |
| `combat-screen-v1.png` | 戦闘画面の UI レイアウト & 構成要素 | 参照（実装目標） |

## 生成・更新ルール

- `codex exec "..."` での AI 画像生成は **参照更新時のみ**（頻繁な再生成はコスト・依存増）
- 生成画像は必ずこの `design/` に置く（ソースに混入させない）
- 大きな方向転換時は v2, v3 と版を残す（v1 を上書きしない）
