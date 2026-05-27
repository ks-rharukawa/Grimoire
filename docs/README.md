# docs — 要件詰めの作業場

`Design.md`（リポジトリルート）で合意した骨格を踏まえ、§8 の未決事項をここで1つずつ詰めていく。

合意事項に矛盾が出る場合は **先に `Design.md` を更新する** こと。docs/ 配下のドキュメントは合意の派生物として位置づける。

## §8 未決事項（書き起こし対象）

| # | テーマ | ファイル | 状態 |
|---|---|---|---|
| 1 | クイズのさじ加減 | [quiz-cadence.md](quiz-cadence.md) | 決定済 |
| 2 | 忘却対策の具体設計 | [forgetting-curve.md](forgetting-curve.md) | 決定済 |
| 3 | クラス構成（分野・初期デッキ） | [classes.md](classes.md) | 決定済 |
| 4 | レアリティの定義 | `rarity.md` | 未着手 |
| 5 | マイルストーン報酬 | `milestones.md` | 未着手 |
| 6 | 知識つながりマップ | `knowledge-map.md` | 未着手 |
| 7 | 学習ビジュアルのリッチ化 | `visual-richness.md` | 未着手 |
| 8 | コンテンツ検証ループ | `content-validation.md` | 未着手 |

ファイルは着手時に作る（空のスタブは置かない）。

## 派生スペック（§8 とは別系統、実装のソース・オブ・トゥルース）

| ファイル | 役割 |
|---|---|
| [style-guide.md](style-guide.md) | visual / UX 仕様（カラーパレット・カード仕様・レイアウト等）。`design/` 配下の参照画像から抽出した、procedural 実装可能な style spec |
