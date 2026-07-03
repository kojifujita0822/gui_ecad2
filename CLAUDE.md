@prompts/startup-auto.md
# CLAUDE.md

本ファイルは ecad2（仮称）プロジェクトの設計索引。まだ基盤整備の初期段階のため、
詳細な設計・アーキテクチャは今後 `docs/` 配下に順次整備する。

## プロジェクト概要
GuiEcad（ラダー図 CAD）で得た知見を活かし、制御盤設計・ラダー／シーケンス図 CAD を刷新する新規プロジェクト。
**キーボードファースト**（マウス操作に頼らない操作性）を主眼に据える。

## 現状
- 技術スタック: **WPFに正式確定**（2026-07-03、殿裁定。詳細は `docs/ecad2-stack-decision-brief.md`）
- 実装は雛形・アーキ設計に着手（`docs/todo.md` T-004）
- 4セッション体制（家老/侍/忍者/隠密）で並列運用。役割定義は `docs-notes/roles/{karo,samurai,ninja,onmitsu}.md`
- 起動プロンプトは `prompts/startup-auto.md`（役割自動決定フロー）
- 計画ドキュメントは `docs/` 配下に順次整備する（索引: `docs/README.md`、家老→各役への指示置き場: `docs/todo.md`）

## 運用ルール
- 実装ディレクトリへの書き込みは侍に一元化し、他役は調査・確認に専念する
- 担当パス（本実装）: `src/Ecad2.sln` ／ `src/Ecad2.Core/`（Model/Simulation/Rendering/Persistence）／ `src/Ecad2.Rendering.Wpf/` ／ `src/Ecad2.Pdf/` ／ `src/Ecad2.App/` ／ `tests/Ecad2.Core.Tests/`
- `poc/` は実験場として本実装とは別に維持する
