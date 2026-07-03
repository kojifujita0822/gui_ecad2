# タスク台帳（家老が采配してよい根拠）

家老が采配してよいのは **Approved** または **In-progress** の行だけ。采配には必ずタスクIDを添える。
台帳に無い作業は、家老の裁量では着手せず `docs/proposed.md` へ記録して殿の承認を待つ
（詳細は `docs-notes/roles/karo.md` の「采配の権限線引き」）。

- 状態: Proposed → Approved → In-progress → Done / Rejected（+ Blocked＝外部要因待ち）
- 種別: auto-OK（家老の裁量で采配可） / gated（殿の承認を経たもの）

## 現在の要望スコープ

- REQ-01: 技術スタック選定（`docs/ecad2-stack-decision-brief.md` 参照）

## タスク

| ID | タイトル | 状態 | 担当 | 根拠 | 種別 | 備考 |
|----|---------|------|------|------|------|------|
| T-001 | 技術スタック裁定（路線A=WPF本命 ／ 路線B=Qt） | Blocked | 殿 | REQ-01 | gated | `docs/ecad2-stack-decision-brief.md` 参照。殿の裁定待ち |
| T-002 | フォーカス保持PoC（PointerReleased後のフォーカス保持・大量記号描画・PDF出力の最小検証） | Proposed | - | REQ-01 | gated | T-001裁定後に着手 |
| T-003 | PoC結果を踏まえた最終スタック確定 | Proposed | - | REQ-01 | gated | T-002完了後 |
| T-004 | 4セッション体制の立て直し・選定スタックで雛形/アーキ設計へ着手 | Proposed | - | REQ-01 | gated | T-003完了後 |

<!-- 完了したタスクも消さず Done にして残す（履歴として活用） -->
