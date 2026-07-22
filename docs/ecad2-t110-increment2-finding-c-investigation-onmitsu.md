# T-110 増分2 所見C調査書（隠密）：配置ツールタブ350pxドラッグの挙動と描画消失

調査日: 2026-07-22　調査担当: 隠密　委任元: 家老（増分2忍者実機確認の所見3件目）
本書は調査のみで修正には着手しない。

---

## 結論（先出し）

- **論点1（フロート化せず「基本機能」タブへ選択切替）: 実装回帰の証拠なし。UIA合成ドラッグの限界とAvalonDock正規のタブ操作セマンティクスの組み合わせで完全に説明できる**（§1）。タブのドラッグは単一ペインのタイトル帯ドラッグとは**全く別の経路**を通ることを一次ソースで確定した（家老の論点1の見立てどおり）。
- **論点2（タブ復帰後のボタン群描画消失、クリックで復旧）: 環境GPU HW描画不全（T-110増分0で確定済みの環境異常）の局所症状の疑いが濃厚**（§2）。ecad2側に「再描画トリガーを漏らしうる自前描画コード」が存在せず、実装回帰として帰す先が無い。
- DoDに従い、**忍者への物理操作再検証を家老へ提案する**（§3。論点2は`DisableHWAcceleration=1`での再現試験を併用すれば一撃で切り分く）。

---

## 1. 論点1: タブドラッグの経路と「選択切替」の機序（一次ソース確定）

### 1.1 タブからのフロート化は「タイトル帯」と別経路

一次ソース（AvalonDock v4.74.1、`docs-notes/vendor-reference/avalondock-v4.74.1/`にローカル保存あり）より:

| 経路 | 単一ペインのタイトル帯 | 2タブペインのTabItem（今回） |
|---|---|---|
| 実装 | `AnchorablePaneTitle`のマウス処理→即ドラッグ開始 | `LayoutAnchorableTabItem`+`AnchorablePaneTabPanel`の2段構え |
| フロート化条件 | 帯上でMouseDown+移動 | (1)TabItem上でMouseDownで`_draggingItem`セット（`LayoutAnchorableTabItem.cs:89-102`）→(2)**タブストリップ領域（AnchorablePaneTabPanel）からMouseLeaveした瞬間**、`e.LeftButton==Pressed`かつ`_draggingItem`非nullなら`StartDraggingFloatingWindowForContent`（`AnchorablePaneTabPanel.cs:84-97`） |
| マウスキャプチャ | 取らない | **取らない**（`Mouse.LeftButton`のプライマリデバイス状態とLeave/Enterイベント列に全面依存） |

### 1.2 「フロート化せず基本機能タブへ選択切替」の機序

`LayoutAnchorableTabItem.cs`にはさらに2つの正規動作がある:

1. **タブ上でのMouseUp＝そのタブをアクティブ化**: `OnMouseLeftButtonUp`が`Model.IsActive = true`を実行（120-125行）。
2. **押下状態で別タブへMouseEnter＝タブ並び替え**: `_draggingItem`が別タブで`LeftButton==Pressed`なら`containerPane.MoveChild(oldIndex, newIndex)`（143-158行）。

UIA合成ドラッグ（350px）で起きたことの再構成:
- 合成移動はワープ的で、キャプチャ無し+`Mouse.LeftButton`状態依存のこの機構ではMouseLeave/Enter/ボタン状態のイベント列が実操作と食い違い、**フロート化条件（1.1(2)）が成立しにくい**。これは既往調査で確定済みの「AttachDrag系はUIA合成入力で不成立になりうる」（`docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-onmitsu.md`調査3: キャプチャ不在+`Mouse.LeftButton`依存の脆さ）と同型。殿環境のMouseAssistant競合（`memory: env_mouseassistant_click_conflict`）が併走している可能性もある。
- フロート化不成立のままMouseUpが「基本機能」タブ上（または移動終点がその近傍）で処理されれば、上記1の正規動作で**「基本機能」がアクティブ化=観測どおりの選択切替**。バグではなくAvalonDockのタブセマンティクスそのもの。

### 1.3 付随の確認事項・注意（範囲外の気づき含む）

1. **タブ並び替えの有無**: 上記2の機構により、押下状態のまま「基本機能」タブ上を通過していれば**タブ順序が入れ替わっている**可能性がある（正規動作）。忍者はタブ順（基本機能/配置ツールの並び）が検証前後で変わっていないかも確認されたい。
2. **`_draggingItem`残留リスク**: `_draggingItem`は**staticフィールド**で、MouseUpハンドラはこれをリセットしない（`OnMouseLeftButtonUp`は`_isMouseDown`のみfalse化）。フロート化不成立でタブ外MouseUpとなった場合、`_draggingItem`が残留し、**後続検証で押下状態のまま別タブへ触れると意図しない並び替えが発火しうる**。検証間にタブ上で通常のマウス移動（ボタン非押下）を挟めば`OnMouseMove`でクリアされる（105-112行）。忍者の連続検証時の注意点として申し送る。
3. **基本機能タブ（CanFloat="False"）のドラッグアウトは無害**: `StartDraggingFloatingWindowForContent`冒頭に`if (!contentModel.CanFloat) return;`のガードあり（`DockingManager.cs:1701-1705`）。この方向の実害は構造的に無い（気づきとして記録、対応不要）。

---

## 2. 論点2: タブ復帰後のボタン群描画消失（本丸）

### 2.1 症候の分析

忍者観測: 「配置ツール」タブへ戻すとF5-F10等ボタン群が視覚的に消失。**UIA上はIsEnabled=True/IsOffscreen=False**（=論理ツリー・レイアウトは正常）。PrintWindow/CopyFromScreen両方式で空白（=実画面のラスタライズが欠落、手法限界ではない）。クリックした瞬間ツールバー全体が再描画され復旧、**機能自体はクリック前から正常動作**。

この「論理・レイアウト・機能は全て生きているが、画面のラスタライズだけが欠落し、Invalidateが走ると復旧する」という型は、**T-110増分0で確定した環境異常（GPU HWアクセラレーション経由のWPF描画が機能していない、`memory: ecad2_gpu_hw_render_blank_screenshot`・`docs/ecad2-t110-poc-verification-ninja.md`§0）と同型**である。増分0では全面白紙として現れたが、今回は「タブ切替で新しく可視化された領域の再ラスタライズ」だけが落ちる局所版と見立てる。

### 2.2 「実装回帰」として帰す先が無いこと（静的確認）

- T-104の2タブ構造は標準機構のみ: `LayoutAnchorable`×2+TabControlの`SelectedContent`切替+`ContentTemplate`（`LayoutAnchorableControl`）。タブ切替時の表示更新はWPF/AvalonDock標準の経路であり、**ecad2側にInvalidate/再描画を自前管理するコードは無い**（中身のToolBarも純XAML定義。LadderCanvasのような自前描画層はこの領域に存在しない）。
- 増分1のecad2側変更（統合トポロジ・スタイル・永続化）にも描画パイプラインへ介入する要素は無い。
- よって「Invalidate/再描画トリガー漏れ」という仮説をecad2実装に帰す先が見当たらない。残る候補はWPF/AvalonDock自体の描画更新と環境GPU異常の相互作用であり、後者は本環境で既に実証済みの異常である。

### 2.3 留保（誠実な限定）

- 「350px合成ドラッグ後」という特殊状態を経てのタブ切替であり、合成入力による状態汚染（§1.3-2等）との複合の可能性は完全には排除できない。
- 増分0PoC・増分1実機確認ではタブ切替での描画消失報告は無かった（ただし当時この複合操作はしていない）。
- 断定には§3の切り分け実測が必要（静的調査の限界、`memory: feedback_static_vs_dynamic_investigation`の型）。

---

## 3. 切り分け実測の提案（家老へ、DoD対応）

1. **物理操作での再検証（論点1の決着)**: 物理マウスで「配置ツール」タブを掴みタブストリップ領域の外（例: キャンバス上）までドラッグ——正規経路ならフロート化が始まるはず。物理で成立すれば論点1は「UIA合成入力の限界」で確定。物理でも不成立なら初めて実装回帰を疑い再調査する。
2. **`DisableHWAcceleration=1`での再現試験（論点2の決着）**: ソフトウェアレンダリング強制で同操作（タブ切替往復）を行い、描画消失が出なければ環境GPU異常で確定。それでも出るなら実装/AvalonDock回帰として深掘りへ。増分0で確立した手順（殿裁可・検証中のみ一時適用・終了後復元・他役のdotnetプロセス不使用確認）を踏襲。
3. 2の再現時は**タブ順序の確認**（§1.3-1）と、検証間の**マウス状態リセット**（§1.3-2、タブ上でボタン非押下の移動を挟む）を併せて行うと、複合要因が分離できる。

---

## 出典

- AvalonDock v4.74.1一次ソース（`docs-notes/vendor-reference/avalondock-v4.74.1/`ローカル保存あり。本調査時はGitHub raw取得・scratchpad保存で実施）:
  - `LayoutAnchorableTabItem.cs:28-158`（_draggingItem static・OnMouseLeftButtonDown/Move/Up/Leave/Enter全読）
  - `AnchorablePaneTabPanel.cs:84-97`（OnMouseLeave→StartDraggingFloatingWindowForContent）
  - `LayoutAnchorablePaneControl.cs`（ドラッグ関連処理が存在しないことの確認）
  - `DockingManager.cs:1701-1712`（StartDraggingFloatingWindowForContentのCanFloatガード）
- `docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-onmitsu.md`（調査3: キャプチャ不在+Mouse.LeftButton依存の合成入力脆弱性）
- `docs/ecad2-t110-poc-verification-ninja.md`§0（環境GPU HW描画不全の確定記録）
- `src/Ecad2.App/MainWindow.xaml`（758-1051行 上段2タブペイン構造・CanFloat設定）
- memory: `ecad2_gpu_hw_render_blank_screenshot`・`env_mouseassistant_click_conflict`・`feedback_static_vs_dynamic_investigation`
