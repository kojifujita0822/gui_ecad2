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
| T-003 | PoC結果を踏まえた最終スタック確定 | Done | 殿 | REQ-01 | gated | 殿裁定：WPFに正式確定。T-002/T-005/T-006の検証結果を踏まえた最終決定 |
| T-004 | 4セッション体制の立て直し・選定スタックで雛形/アーキ設計へ着手 | Done | 侍 | REQ-01 | gated | 雛形完成（コミットfb85746）。src/Ecad2.sln、Core(Model/Simulation/Rendering/Persistence)/Rendering.Wpf/Pdf/App、tests/。dotnet build成功。CLAUDE.md担当パス欄に反映済み |
| T-007 | GuiEcad実ソース（gui_ecad）からのModel/Rendering/Simulation/Persistenceの移植 | Done | 侍 | REQ-01 | gated | 全段階完了(コミット一覧: Model 6eab0ba／Rendering a762b20,d1198cc,3c67ab5,771fb08,b825ef4,ac0c703／Persistence 88ea0fd)。.GCAD互換性はgui_ecad実サンプルでのテスト3件で実証済み。気づき：PartFolderStore等の保存先フォルダ名が"GuiEcad"のまま→殿へ確認中 |
| T-008 | ecad2 UI/UX全体像設計（画面構成・操作フロー、GuiEcadの画面構成にこだわらず新規検討） | Done | 殿 | REQ-01 | gated | 殿裁定：区画分け(左パレット/中央キャンバス/右機器表・プロパティ/下部出力)は維持、中身は視覚シンプル化＋キーボード主体・ボタン補助に刷新。詳細`docs/ecad2-ui-ux-design-brief.md` |
| T-009 | Ecad2.App UI実装（視覚シンプル化・キーボード主体設計・GX Works3踏襲の反映） | Done | 侍 | REQ-01 | gated | 全8段階完了。コミット一覧：86898fb/1d682ea/211a945/94a9742/351d908/f20ae34/4fba36a/a15def0/12b6736/7c7d3a1/fa0a5ef/6d4d458。骨格実装完了、残課題はT-015〜T-024参照 |
| T-015 | 左パレットの図形ビジュアルプレビュー表示（SVG生成等） | Proposed | - | REQ-01 | gated | T-009残課題2 |
| T-016 | 要素配置ロジック本体（キャンバスクリックでSheetへ要素追加、ToolState接続） | Proposed | - | REQ-01 | gated | T-009残課題3 |
| T-017 | キャンバス上の要素選択・編集フォーカス制御の本実装（右パネル連動含む） | Proposed | - | REQ-01 | gated | T-009残課題4 |
| T-018 | DesignRuleCheckと下部出力パネルの接続 | Proposed | - | REQ-01 | gated | T-009残課題5 |
| T-019 | ドキュメント管理（GcadSerializer Load/Save、新規/開く/保存のメニュー・ツールバー接続） | Proposed | - | REQ-01 | gated | T-009残課題6 |
| T-020 | 空状態(濃紺)⇔作業領域(白)の動的切替 | Proposed | - | REQ-01 | gated | T-009残課題7。現状は作業領域色で暫定固定 |
| T-021 | キーボード規約の残り（Enter=配置確定、モーダル非ネスト、恒常モード/quasimode、アクセシビリティツリー明示管理） | Proposed | - | REQ-01 | gated | T-009残課題8 |
| T-022 | ステータスバーの高情報密度化（機種名/局番/ステップ数等） | Proposed | - | REQ-01 | gated | T-009残課題9。本実装機能への依存あり |
| T-023 | LadderCanvasへのAutomation Peer付与等アクセシビリティ強化 | Proposed | - | REQ-01 | gated | T-009残課題10 |
| T-010 | GX Works3のUI/UX調査（アイコン様式・配色・リボン/ツールバー構成・パネル配置の具体像） | Done | 隠密 | REQ-01 | gated | 完了（Web一次情報ベース）。`docs/ecad2-gxworks3-uiux-survey-onmitsu.md`参照 |
| T-012 | GX Works3実機によるUI/UX追加調査（T-010のWeb調査を実機で裏取り・精緻化） | Done | 隠密 | REQ-01 | gated | 完了。`docs/ecad2-gxworks3-uiux-survey-onmitsu-part2.md`参照。新発見：F-key併記アイコン・配置直後の浮動インライン入力・状態依存配色(空=濃紺/作業領域=白)。家老裁量で採用決定、T-009段階3以降着手可 |
| T-014 | GX Works3の技術スタック調査（殿直接依頼） | Done | 隠密 | REQ-01 | gated | 完了。`docs/ecad2-gxworks3-uiux-survey-onmitsu-part2.md`付録参照。結論：ネイティブC++(MFC/ATL)+.NET Framework(WinForms)+商用UIライブラリ3社(Syncfusion/DevExpress/Infragistics)のハイブリッド構成。参考情報として記録、直接のタスク影響なし |
| T-013 | ツールバー/メニューアイコンの本格的な意匠制作（記号・単色ベースのグラフィックデザイン） | Proposed | - | REQ-01 | gated | T-009段階3では簡易プレースホルダ(Path Geometry/Unicode記号)で仕組みのみ実装。本格的な意匠は別タスクとして切り出し(家老裁量) |
| T-011 | PartFolderStore/PinnedPartStoreの保存先フォルダ名をGuiEcad→Ecad2に変更 | Done | 侍 | REQ-01 | gated | 完了（コミット49b4554）。build/test成功 |
| T-006 | タブ切替時のフォーカス喪失対策の実装・再検証（同一テスト項目の再実行） | Done | 侍(実装)／忍者(再検証) | REQ-01 | gated | 対策：編集モード中はタブ切替操作自体を無効化（e.Handled=true）。再検証で問題なし・回帰なしを確認 |
| T-005 | WPF非技術面の多角検証（ライセンス・Microsoft保守体制/将来性・.NET対応ロードマップ・業界採用実績等） | Done | 隠密 | REQ-01 | gated | 完了。`docs/ecad2-wpf-nontechnical-survey-onmitsu.md` 参照。結論：非技術面リスクは低〜許容範囲、WPF本命判断を補強 |

<!-- 完了したタスクも消さず Done にして残す（履歴として活用） -->
