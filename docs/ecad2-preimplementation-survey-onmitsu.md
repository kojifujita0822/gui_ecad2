# 実装前先行調査：T-015/T-018/T-020/T-021/T-023（隠密）

> 2026-07-03 隠密調査。家老依頼（T-026完了後の残課題のうち、実装前に調査しておくと侍の
> 着手がスムーズになりそうなもの）。T-019/T-017は先行調査済み(T-024/T-025)のため対象外。

---

## T-015: 左パレット図形ビジュアルプレビュー（SVG生成等）

**結論**: GuiEcadには「部品のサムネイル・プレビュー画像を汎用的に生成する」ロジックは存在しない。
パレット表示は固定種別（ElementKind）ごとの記号アイコンをSVGとして都度生成しているのみで、
自作パーツ（PartDefinition）用のサムネイル生成機構はない。

**根拠**:
- `MainPage.Palette.cs`の`LoadToolIconsAsync()`は固定11種のみに`SvgRenderer.GenerateSymbolSvg`を
  呼ぶ。`PartLibrary`/`PartDefinition`（自作パーツ）への参照はコード内に一切なし。
- `DiagramRenderer.cs`に`DrawPreview(IRenderer r, ElementInstance e, Color color)`という、
  1要素だけを指定色で描く軽量メソッドが既に存在（配置プレビュー＝ドラッグ中のゴースト表示用）。
  構造はサムネイル生成に酷似しており流用の土台になる。
- `IRenderer`/`PartDrawing`/`SymbolGlyphs`はUI非依存の純粋な描画抽象で、mm単位のワールド座標で
  完全に抽象化されている。

**推奨案**: GuiEcadから「丸ごと移植できる完成品」は無いが、①`IRenderer`抽象、②プリミティブ描画
ロジック（`SymbolGlyphs`/`PartDrawing`相当）、③1要素だけ描く`DrawPreview`のパターン、の3点は
設計ごと流用可能。ecad2で新規に書く必要があるのは「部品単体をオフスクリーン/小Viewportに
フィットさせてビットマップ化する」薄いラッパー層のみ。WPFでは`DrawingVisual`をオフスクリーンで
描き`RenderTargetBitmap`化する方式が、SVG経由（WPFに標準SVGデコーダが無くパスの自前変換が要る）
より実装コストが低く、既存の描画抽象とも自然に整合するため推奨。

---

## T-018: DesignRuleCheckと下部出力パネルの接続

**結論**: `DesignRuleCheck.cs`はGuiEcadから完全に同一ロジックで移植済み（namespace以外差異なし）。
GuiEcad側の出力パネルは素朴な`ListView`＋文字列整形で実装されており、構造化データバインディングや
色分けは未実装。

**根拠**:
- API: `enum DiagnosticSeverity{Info,Warning,Error}`、`record Diagnostic(Severity, Code,
  DeviceName, Message, IReadOnlyList<CircuitRef> Locations)`。5つの静的Checkメソッドが
  `IReadOnlyList<Diagnostic>`を返す。
- GuiEcad `MainPage.Drc.cs`: 結果を`"[E] DRC-XREF-001 [P1 行3] メッセージ"`という**文字列に整形**
  してプレーンな`ListView`にセット。重大度の色分け/アイコンは無し（`[E]/[W]/[I]`プレフィックスのみ）。
  選択変更で該当箇所へジャンプ（`CenterViewOnRow`）する機能はある。
- ecad2側`MainWindow.xaml`の出力パネルは固定文字列の空プレースホルダで未接続。DataContextに
  `MainWindowViewModel`が設定済みでMVVMバインディング前提の構造。

**推奨案**: ViewModelに`ObservableCollection<Diagnostic>`・`RunDrcCommand`・選択変更時のジャンプ
処理を実装し、XAML側は`ListView`/`DataGrid`で構造化バインディングにする（GuiEcadの文字列整形より
一段進んだ実装を推奨）。ジャンプ処理はGuiEcadの`CenterViewOnRow`相当をecad2キャンバスに移植。

**要確認**: GuiEcadに構造化UI実装例が無いため、**WPF版を文字列整形踏襲かDataGrid化するか（重大度
の色分けデザイン含む）はUI/UXの分岐であり、既定方針の延長に見えても殿への確認を推奨**。

---

## T-020: 空状態(濃紺)⇔作業領域(白)の動的切替

**結論**: GuiEcadに「プロジェクト/ドキュメント未作成時は特定の見た目、作成後は別の見た目」という
状態切替の実装は**存在しない**。GuiEcadは起動と同時に必ず新規ドキュメントを生成する単一ドキュメント
モデルで、「未作成の空状態」自体が存在しないため。

**根拠**:
- `MainPage.xaml.cs`のコンストラクタ相当の初期化で即座に空シートを持つドキュメントを生成しており、
  「ドキュメント無し」状態を経由しない。
- 存在する唯一の背景色切替はユーザーが手動で選ぶダーク/ライトの**作図色テーマ**（`DrawingTheme.cs`
  の`Default`/`Dark`）で、ドキュメント有無とは無関係。切替はコードビハインドで`CanvasControl.
  ClearColor`を直接書き換える方式（密結合）。
- 「濃紺」「空状態」「未作成」等のキーワードはコードベース全体で0件。

**推奨案**: ViewModelに`HasProject`（bool）等の状態プロパティを持たせ、XAML側は`DataTrigger`で
Background切替するのがWPFのバインディング機構に沿う（GuiEcadのコードビハインド直書き方式は
踏襲しない）。**GuiEcadは「未作成状態」という概念自体を持たない設計だったため、ecad2でGX Works3
同様の空状態⇔作業領域切替を導入すること自体が、GuiEcadには無かった新規のアプリ状態設計になる**
点に留意。「そもそもプロジェクト未作成状態を持つか」はUI/UXの前提設計であり確認事項たりうる。

---

## T-021: キーボード規約の残り（GX Works3マニュアル追加調査）

**技術的到達状況**: `sh081214as.ema`はzip形式。展開すると`melsec/database/`配下に3つのSQLite DB
（`SH081214-C.msql`=本文HTML、`SH081214-F.msql`=検索用、`SH081214-T.msql`=目次）が得られた
（前回想定と異なり目次は`-T.msql`側。次回参照時の訂正メモ）。

**重要な留保**: マニュアルは全てのキー名を`alt=""`（空）のアイコン画像で表現しており、"Enter"や
"Esc"という文字列は本文に一度も登場しない。以下は状況証拠に基づく推定であり、直接的な文言確認
ではない。

**結論・根拠**:
1. **Enter確定挙動**: 回路入力・コメント編集・ST/FBD/LD入力で「セル選択→[Enter相当アイコン]
   またはダブルクリック」という同一表現が使われ「開く」動作の一貫性は状況証拠として強い。しかし
   コメント/ステートメント/ノート編集は**モーダルダイアログではなくメニュートグル型モード**で、
   Enter/Escでなく「同じメニュー項目を再選択」して終了するという**非対称性**を発見。「Enterで
   確定」という明示文自体は0件で断定できない。
2. **モーダル非ネスト**: 「モーダル」という用語自体は本文に一度も出現しないが、多段遷移の実例を
   2箇所発見（回路入力ダイアログ内でOK→コメント入力画面が別途開く／パラメータエディタ→詳細設定
   →ネットワーク構成ウィンドウ→さらに別画面）。**「常に単一ダイアログで完結」という前提はGX
   Works3では完全には成立していない**。
3. quasimode・アクセシビリティに関する記述は0件（全アイコンのalt属性も空でアクセシビリティ
   非配慮の状況証拠）。

**推奨案**:
- R6: GX Works3の「セル選択→Enter/ダブルクリックで編集開始」の一貫性は踏襲可能だが、「Enterで
  確定して閉じる」を万能ルールとして断定する根拠はGX Works3側になかった。ecad2ではダイアログ種別
  ごとにEnter/Escの挙動を明示的に定義し、GX Works3のトグル型モードの曖昧さは反面教師とすべき。
- R7: GX Works3にも多段遷移の実例があるため、**「常に単一ダイアログで完結」をGX Works3由来の
  模範として引用するのは不正確**。ecad2でR7（モーダル非ネスト）を採用するなら、GX Works3の実例
  としてではなく、AutoCAD公式ガイド（T-008調査済み）を根拠とする独自設計判断として明記すべき。
- quasimode/アクセシビリティはGX Works3に前例がないため、他社事例（Eclipse、Visual Studio等）の
  参照が必要。

---

## T-023: LadderCanvasへのAutomationPeer付与

**結論**: WPFの一般的パターンは「`FrameworkElement.OnCreateAutomationPeer`をオーバーライドし、
コントロール固有の`AutomationPeer`派生クラスを返す」方式。子要素が実ビジュアルを持たない場合は、
そのピアの`GetChildrenCore()`で論理要素（記号1つ1つ）ごとに独自の`AutomationPeer`インスタンスを
手動生成して返す（`ItemsControl`の仮想化アイテムと同種のパターン）。**GuiEcadには前例が無い**
（`AutomationPeer`/`CreateAutomationPeer`/`GetChildrenCore`で検索し0件、`AutomationProperties.
AutomationId`の宣言的指定のみ）。忍者の実測（keyboard-requirements.md D節）の「Win2D CanvasControl
は単一画像要素にしかならない」という課題への対策はGuiEcadでは一切試みられていなかったことを確認。

**根拠**: [UI Automation of a Custom Control - WPF (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/ui-automation-of-a-wpf-custom-control)

**推奨案**: `LadderCanvas`に専用`LadderCanvasAutomationPeer : FrameworkElementAutomationPeer`を
作成し`OnCreateAutomationPeer`で返す。記号1つ1つに対応する`SymbolAutomationPeer`（`AutomationPeer`
直継承、Ownerは非UIElementの記号モデル）を`GetChildrenCore()`内で生成・キャッシュし、
`GetBoundingRectangleCore`で論理座標→スクリーン座標変換を行う。選択状態を扱うなら
`ISelectionProvider`/`ISelectionItemProvider`も実装。**実装コストは中程度**（骨格自体は1〜2日
程度だが、記号の追加・削除・再配置のたびにピアツリー同期とイベント発火の整合を取る作業が継続的に
発生する点が主コスト）。GuiEcadの教訓（フォーカス委譲の暗黙性がバグ温床）を踏まえ、ピアの生成・
破棄タイミングをキャンバスの再描画ロジックと一元管理する設計が望ましい。

---

## 総括：殿への確認が必要な分岐点

今回の5調査で、**UI/UXの前提設計に関わり殿の確認を要する可能性がある論点**が2つ浮上した
（既定方針の延長に見えても分岐は人間確認、の原則に基づき明記）:

1. **T-018**: DRC出力パネルを「文字列整形（GuiEcad踏襲）」にするか「構造化DataGrid＋重大度色分け
   （新規）」にするか
2. **T-020**: ecad2で「プロジェクト未作成の空状態」というアプリ状態自体を新規に導入するか
   （GuiEcadには無かった概念）。導入する場合、GX Works3同様の濃紺⇔白の切替仕様まで踏襲するか
