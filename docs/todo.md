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
| T-001 | 技術スタック裁定（路線A=WPF本命 ／ 路線B=Qt） | Done | 殿 | REQ-01 | gated | 殿裁定：路線A（WPF本命）で仮確定。Qt不採用・Avaloniaはクロス保険で保留。最終確定はT-003（PoC結果次第） |
| T-002 | フォーカス保持PoC（PointerReleased後のフォーカス保持・大量記号描画・PDF出力の最小検証） | Done | 侍(実装)／忍者(実機検証) | REQ-01 | gated | 実機検証完了。フォーカス保持=良好、記号描画=5万個113ms、PDF出力=良好。タブ切替時のフォーカス挙動のみ考慮事項あり |
| T-003 | PoC結果を踏まえた最終スタック確定 | In-progress | 家老 | REQ-01 | gated | T-002・T-005完了。家老が結果を統合し殿の最終裁定を仰ぐ段階 |
| T-004 | 4セッション体制の立て直し・選定スタックで雛形/アーキ設計へ着手 | Proposed | - | REQ-01 | gated | T-003完了後 |
| T-006 | タブ切替時のフォーカス喪失対策の実装・再検証（同一テスト項目の再実行） | Done | 侍(実装)／忍者(再検証) | REQ-01 | gated | 対策：編集モード中はタブ切替操作自体を無効化（e.Handled=true）。再検証で問題なし・回帰なしを確認 |
| T-005 | WPF非技術面の多角検証（ライセンス・Microsoft保守体制/将来性・.NET対応ロードマップ・業界採用実績等） | Done | 隠密 | REQ-01 | gated | 完了。`docs/ecad2-wpf-nontechnical-survey-onmitsu.md` 参照。結論：非技術面リスクは低〜許容範囲、WPF本命判断を補強 |

<!-- 完了したタスクも消さず Done にして残す（履歴として活用） -->
