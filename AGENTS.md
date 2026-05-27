# AGENTS.md — Grimoire 作業契約

このリポジトリで作業する AI エージェント（Claude Code / Cursor / Copilot 等）への作法書。
人間向けの説明は `README.md`、設計の合意事項は `Design.md` を参照。

## Grimoire とは

技術概念を「カード」として集め、その中身を理解することで実際に戦力になっていく、ローグライク・デッキ構築型の学習ゲーム。理解＝強さを直結させ、AI に頼れない「知らない存在(unknown unknowns)」を広く浅く知っていくことを目的とする。

## 一次ソースの場所

| 対象 | 場所 |
|---|---|
| 合意事項・却下案・未決事項 | `Design.md`（リポジトリルート） |
| §8 未決事項の詰め作業 | `docs/` 配下 |

## 会話言語

日本語。

## 作業の作法

- **Design.md の §9「却下した案」は蒸し返さない。** 一度否定された設計上の選択肢は、再提案する前に必ず §9 を確認する。
- **新要素を加える時は Design.md の §1-7 と矛盾しないか確認する。** 矛盾する場合は先に Design.md を更新してから派生作業に移る。
- **未決事項は `docs/` 配下で詰める。** `Design.md` は合意の総元締めなので、議論中の内容は書き込まない。
- **要件はまだ流動的。** 仕様が固まる前に実装の細部を詰めない。

## コードの2モード使い分け

| モード | 用途 | 置き場所 |
|---|---|---|
| **Vibe coding（ノリで書く）** | 描画・コア体験の試作。動けばよし、後で読まない | `prototypes/` 配下（捨てる前提） |
| **Spec-driven（仕様駆動）** | 残すコード。カード定義・コアループ・本番に使う実装 | `src/` 配下（仕様 → 実装 → レビュー） |

**残すコードに vibe coding は禁止。** AI 生成コードは脆弱性発生率が約10倍高いという実測がある以上、本体コードはノールック採用しない。

## 自律モード（Grimoire 固有）

このリポジトリは「自律モード」で運用される。Claude Code は以下のスタイルで動く:

- **iterative review loop**: 決定ブロック書き出し直後、`.claude/agents/reviewer.md` を独立 context で呼び、`codex exec` も並行起動。両者の指摘を統合 → 修正 → 再 reviewer → `VERDICT: CLEAN` または最大 3 サイクルで停止。
- **escalation**: 後述の Escalation Matrix に従い、root-level だけユーザーに上げる。それ以外は自走、完了時に 1 文サマリ。
- **過剰確認禁止**: 「これでいいですか？」の反復はしない。判断材料があれば執行する。

## Escalation Matrix

| 必ずユーザーに上げる | 自走可 |
|---|---|
| `Design.md` §1-7（合意事項）に影響する変更 | `docs/` 配下の派生決定の詰め |
| §9 却下案を上書きする提案 | カードの仮名・パラメータ等の試作レベル決定 |
| 新規外部依存の追加（NuGet パッケージ等） | 既存スタック内の実装詳細 |
| 破壊的 git 操作（reset --hard / force push / branch -D） | 通常の commit / push（区切れる単位で適度に） |
| 外部 API 呼び出し（codex 以外） | codex exec によるレビュー（既存ルール） |
| 大きなスコープ変更（試作 → 本実装移行 等） | 個別のバグ修正・リファクタ |

## レビュー・サイクル仕様

決定ブロックが `docs/` または `Design.md` に書き出されたら以下を自動実行する。**「書き出した瞬間」がトリガー、A/B/C 議論の途中ではやらない**（早すぎる介入は収束を阻害する）。

1. **サイクル1**:
   - `.claude/agents/reviewer.md` を独立 context で呼ぶ（read-only）
   - 並行で `codex exec` を起動（ユーザーの `~/.claude/CLAUDE.md`「Codexレビュー」準拠）
   - 両者の指摘を統合: Critical/High は即修正、Medium/Low は試作検証項目に追記
2. **サイクル 2-3**:
   - `.claude/agents/reviewer.md` のみ呼ぶ（コスト最適化、codex は1サイクル目のみ）
   - 差分レビュー（前ラウンドの修正部分のみ、振動防止）
   - 修正
3. **停止条件**: reviewer が `VERDICT: CLEAN` を返す、または 3 サイクル到達
4. **コミット**: 各サイクル後に区切れる単位で commit + push
5. **ユーザー報告**: ループ終了時に 1 文サマリ

## サブエージェント一覧

`.claude/agents/` 配下のカスタムエージェント。それぞれ独立 context で動き、結果を main agent に返す。

| エージェント | 役割 | 呼び出しタイミング | 書き込み |
|---|---|---|---|
| `reviewer` | 決定ブロック (docs/ または Design.md) のレビュー | 上記レビュー・サイクル仕様の通り | Read-only |
| `designer` | visual / UX のデザイン批評（mood-board / mockup / style-guide vs 実装） | visual の大きな更新直後、ユーザーが「リッチに」等と言ったとき、新画面実装直後 | Read-only |

main agent はこれらの返答を受けて、適用する改善を選別し実装する。**両エージェントとも書き込み権限を持たない** ことで、ループ的暴走と振動を防ぐ。

## Git 運用ルール

- **commit / push は Claude が行う**。ユーザーは依頼するだけでよい。
- **コミットメッセージ規約**: `<label>: <日本語サブジェクト>` 形式（Conventional Commits の薄い版）。
  - `docs:` ドキュメント変更（Design.md / docs/ / README 等）
  - `chore:` 設定・雑務（.gitignore / AGENTS.md / ディレクトリ構造）
  - `feat:` 機能追加（実装コード）
  - `fix:` バグ修正
  - `refactor:` リファクタリング
  - `proto:` プロトタイプ追加・更新（`prototypes/` 配下）
- **Author は git config の値を使用**。`Co-Authored-By: Claude ...` の trailer は**付けない**。
- **push タイミング**: 区切れるタイミングで適度に。毎コミット push する必要はない。
- **push 先**: `origin/main`（feature ブランチ運用は将来必要になったら追加）。
- **NEVER**: hooks スキップ（`--no-verify`）、署名バイパス、main への force push。

## 技術構成（Design.md §7 より）

- 言語/フレームワーク: **C# + Avalonia + SkiaSharp**（Mac/Windows 両対応、自前描画）
- HTML/Web 描画は使わない
- アプリ実行時に AI API は叩かない。コンテンツは事前生成＋検証
- ローカル LLM は使わない

## TODO（コード実体ができたら追記）

- ビルド・実行コマンド
- コード規約（命名・フォーマッタ）
- テスト方針
- セキュリティ留意点
