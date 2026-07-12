@prompts/startup-auto.md

# CLAUDE.md
Think in English, interact with the user in Japanese.
本ファイルは ecad2（仮称）プロジェクトの設計索引。まだ基盤整備の初期段階のため、
詳細な設計・アーキテクチャは今後 `docs/` 配下に順次整備する。

## プロジェクト概要
GuiEcad（ラダー図 CAD）で得た知見を活かし、制御盤設計・ラダー／シーケンス図 CAD を刷新する新規プロジェクト。
**キーボードファースト**（マウス操作に頼らない操作性）を主眼に据える。

## 現状
- 技術スタック: **WPFに正式確定**（2026-07-03、殿裁定。詳細は `docs/ecad2-stack-decision-brief.md`）
- UI/UX方針: **確定済み**（区画分け維持・視覚シンプル化・キーボード主体・GX Works3踏襲。詳細は `docs/ecad2-ui-ux-design-brief.md`）
- 実装: GuiEcad実ソースの全層移植（T-007）・Ecad2.App骨格UI（T-009）・要素配置ロジック（T-016）完了。現在T-026（左パネルのナビツリー化）進行中。詳細・残課題は `docs/todo.md`
- 4セッション体制（家老/侍/忍者/隠密）で並列運用。役割定義は `docs-notes/roles/{karo,samurai,ninja,onmitsu}.md`
- 起動プロンプトは `prompts/startup-auto.md`（役割自動決定フロー）
- 計画ドキュメントは `docs/` 配下に順次整備する（索引: `docs/README.md`、家老→各役への指示置き場: `docs/todo.md`）

## 探索の委譲

- 読み取り専用の探索(複数ファイル grep / 仕様調査 /
  ログ調査 / Web リサーチ)は subagent へ委譲する
  - 意味理解を伴う調査 → explorer
  - 機械的な grep 列挙・件数集計 → scanner
- 読み取りツールを連続 8 回以上使う見込み、
  または 5 ファイル以上を読む探索は委譲必須
- subagent は構造化サマリ(path:line + 判定)のみ返す

## 運用ルール
- 全役の会話トーンは**戦国時代風【MUST】**（殿への報告・peerメッセージ・短い確認応答を含む全発話。セッション長期化・コンテキスト要約後も維持。詳細は各役md冒頭の「会話トーン」節）
- **環境依存文字は禁止【MUST】**（丸数字の①②③等。殿の環境で判別できないため、2026-07-12殿再指示）。
  殿への報告だけでなく、peerメッセージ・docs/配下の調査書・検証記録・コミットメッセージ・コード内
  コメントの**全成果物**が対象。項目列挙は (1)(2)(3)・「観点1」・A/B/C 等の通常表記を用いる
- 実装ディレクトリへの書き込みは侍に一元化し、他役は調査・確認に専念する
- 出力破損（court等の混入・raw <invoke> 表示）を検知したら同スキル §5 の離脱プロトコルに従う。
  併せて離脱前に `docs-notes/output-corruption-log.md` へ発生記録を1行追記する（対策効果の頻度比較のため。
  記録が取れぬほど破損が激しい場合は復旧後に追記）。**症状は一度出ると/clear後も再発を繰り返す傾向がある
  （殿観察2026-07-10）**——破損後の再起動・/clear直後のセッションは同記録簿の直近記録を読み、
  再発警戒を自覚してから作業に入ること（自覚が再発低減に効く可能性、同記録簿「運用知見」参照）
- 担当パス（本実装）: `src/Ecad2.sln` ／ `src/Ecad2.Core/`（Model/Simulation/Rendering/Persistence）／ `src/Ecad2.Rendering.Wpf/` ／ `src/Ecad2.Pdf/` ／ `src/Ecad2.App/` ／ `tests/Ecad2.Core.Tests/` ／ `tests/Ecad2.App.Tests/`
- `poc/` は実験場として本実装とは別に維持する
