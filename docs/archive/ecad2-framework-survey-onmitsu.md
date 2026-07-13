# ecad2 技術スタック候補調査（隠密担当分）— 2026-07-03

> 家老依頼。**Part A: GuiEcad知見の抽出**／**Part B: .NET系5候補（WPF/Avalonia/WinUI3/Uno/MAUI）調査**。
> 調査・整理のみ。実装は行わない。忍者（Web/クロスPF系）・侍（Qt/ImGui/egui/Slint/GTK系）の並行調査と合わせ、家老が統合予定。
> 評価軸の背景資料: [docs-notes/ecad2-keyboard-requirements.md](ecad2-keyboard-requirements.md)（忍者作成・R1〜R10チェックリスト）。

---

## Part A: GuiEcad知見の抽出

### A-1. 保つべき設計（データモデル・描画抽象。フレームワーク非依存で継承可能）

| 設計 | 内容 | 出典 |
|---|---|---|
| **IRenderer抽象** | mm単位ワールド座標で画面(Win2D)/PDF(PDFsharp)/SVGを同一コードで扱う。`DiagramRenderer`がモデルを走査しIRenderer呼び出し、二重保守を防止。バックエンド差し替えが構造的に可能（ecad2で描画技術を変えてもこの抽象は転用できる） | [docs/rendering.md](../docs/rendering.md) |
| **ドキュメントモデル／ネットリスト分離** | 永続化する幾何モデルと、幾何から導出する非永続ネットリスト（Union-Find→不動点評価）を明確分離。`.GCAD`は前者のみ保存 | [docs/data-model.md](../docs/data-model.md), [docs/persistence.md](../docs/persistence.md) |
| **Device一元管理** | `DeviceTable`が状態・クロスリファレンス・BOM(型式/メーカー/数量)のキー。コイルと各所接点が同じ`DeviceName`参照 | [docs/data-model.md](../docs/data-model.md) |
| **記号(見た目)と種別(データ)の分離** | `ElementInstance`は`Kind`+`DeviceName`+`Params`のみ保持。記号グリフ・ポート・占有セル数はカタログが`Kind`で引く（将来の記号差し替えに対応） | 同上 |
| **接続点(Port)モデル** | 幾何推測を排し、カタログ定義ポート(`RowOffset`/`BoundaryOffset`)とグリッドノード座標一致で結線判定(Union-Find)。横隣接・分岐・母線接続すべてこの帰結として自然に成立 | 同上 |
| **線番号/回路番号の自動採番** | 線番号=ネット(電気的連続)単位、回路番号=横の回路線単位。いずれも読み順で自動順送り採番 | 同上 |
| **DrawingTheme** | 線スタイルを役割(`StrokeRole`)ごとのプリセットから引く一元管理。mm固定・画面/PDF一致 | [docs/rendering.md](../docs/rendering.md) |
| **.GCAD JSON永続化** | `System.Text.Json`のみ(外部依存なし)、`schemaVersion`でマイグレーション対応、GUID安定ID | [docs/persistence.md](../docs/persistence.md) |

**評価**: 上記はいずれも**UIフレームワーク非依存**（`GuiEcad.Core`・`GuiEcad.Pdf`に相当）。技術スタックを何に変えても丸ごと転用可能な資産であり、ecad2でも踏襲が合理的。

### A-2. 作り直すべき痛点

1. **コードビハインド中心・MVVM分離が低い**（[docs-notes/ui-framework-migration-estimate.md](ui-framework-migration-estimate.md)実測）: `src/GuiEcad.App`実ソース＝C# 30ファイル8,565行・XAML 5ファイル1,142行（生成物除く）。`MainPage`のpartial class群にUIイベント処理が直結、ICommand/ViewModelパターン未導入。**ただしUndo/Redo(`Commands/`)とモデル層の境界は明確で、この部分は移行時も無傷で再利用できる**。
2. **WinUI3固有API依存**: Win2D（27ファイル）、KeyboardAccelerator/フォーカス管理（12ファイル、`MainPage.KeyBindings.cs`296行等の複雑な状態機械）、WinUI3標準コントロール（17ファイル）、XamlRoot/DispatcherQueue等その他WinUI3/UWP固有API（17ファイル）。
3. **フォーカス/キー処理の脆さ【最大の痛点】**: [docs-notes/ecad2-keyboard-requirements.md](ecad2-keyboard-requirements.md)にA1〜A7・B1〜B2として詳細整理済み（忍者作成）。象徴例は **Issue #6179**（PointerReleased後、アプリコードのFocus()呼び出しを一切経由せずフォーカスが暗黙移動、Microsoft側 not planned）— 9ラウンドかけても根本解決できず機能自体を非表示化して決着。他にも「グローバルにキーを拾う公式APIが存在しない」（[Issue #3986](https://github.com/microsoft/microsoft-ui-xaml/issues/3986) not planned）、「Win2D CanvasControlは既定でフォーカス非取得」（[Win2D Issue #686](https://github.com/Microsoft/Win2D/issues/686)）等、構造的な弱点が複数。
4. **自動化・アクセシビリティの弱さ**: UI Automation上でWin2D CanvasControl内部が単一画像要素にしかならず走査不可。外部プロセスからの`SendInput`/`SendKeys`がアプリに一切届かない（実測確認済み）。
5. **モーダルUIの多重表示不可**: `ContentDialog`は同時に複数開けない制約があり、ダイアログを跨ぐ設計と衝突した実例あり（B1）。

### A-3. ecad2技術選定への示唆

- Part Aで洗い出した「保つべき設計」はフレームワーク非依存の資産のため、**技術選定の自由度を狭めない**（どの候補を選んでも`GuiEcad.Core`相当の移植コストは実質ゼロに近い）。
- 「作り直すべき痛点」はほぼ全てが**フォーカス管理の非決定性**(A-2の3)に起因する。ecad2の技術選定における最重要判定軸は、忍者整理のR1「フォーカスの所在をアプリが完全制御できるか」に収束する（家老指示の評価軸(1)と一致）。

---

## Part B: .NET系5候補調査（WPF / Avalonia / WinUI3 / Uno / MAUI）

**調査中（並列サブエージェント5体・実行中）。完了次第、比較表＋各候補考察を追記し家老へ報告する。**

### B-1. WPF (.NET 8)

**軸1（フォーカス管理・最重要）**: `FocusManager`が「キーボードフォーカス」（OS/HWNDレベル・1つ）と「論理フォーカス」（フォーカススコープ単位）を分離し、`GetFocusedElement`/`SetFocusedElement`でスコープ単位の明示制御が可能（[Focus Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview)）。`PreviewLostKeyboardFocus`で`e.Handled=true`によりフォーカス変更自体を**キャンセル可能**（WinUI3には無い防御線）。`KeyboardNavigation.TabNavigation`でコンテナ単位のタブ挙動を宣言的に固定可（`Cycle`でフォーカストラップも標準API範囲で実装可）。Issue #6179型（アプリのFocus()を経由しない暗黙のタブオーダー越境）に**完全一致する既知バグは調査範囲内では未発見**（断定不可・不明）。

**軸2〜5**: `DrawingVisual`+`VisualTreeHelper.HitTest`（Geometryヒットテスト対応）でCAD向け高性能描画に実績。PDFsharpは専用NuGet`PDFsharp-wpf`があり親和性最高。Windows専用（クロスPFは要サードパーティAvalonia XPF）。C#/XAML/MVVM知見は概ね直接転用可（Win2Dコード資産のみ再利用不可）。

**長所**: フォーカス制御の明示的・宣言的API（スコープ・キャンセル可能イベント）／CAD向け描画・ヒットテストの実績豊富／PDFsharp統合実績最高／20年の成熟エコシステム／OSS化・.NET10同梱で保守リスク低い。

**短所・リスク**: Windows専用／フォーカス関連の未解決バグは相互運用境界に残存／レガシー感あるAPI（`x:Bind`非対応）／Win2D資産は書き直し必要。

**R10**: [Issue #9257](https://github.com/dotnet/wpf/issues/9257)（Open, 2024年報告）— `WindowsFormsHost`をPopup内でホストした際のフォーカス復帰不良（WPF-Win32相互運用の境界起因、Issue #6179とは性質が異なる）。関連: [Issue #3769](https://github.com/dotnet/wpf/issues/3769)（Open, 2020年報告）。

**総合所見**: フォーカス管理の明示的APIが揃い、Issue #6179型の構造的リスクはWinUI3より低いと推測されるが一次情報での断定はできず実装実証が必要。描画・PDF・C#知見の再利用性は良好でGuiEcadの後継として現実的な最有力候補の一つ。

### B-2. Avalonia UI（2026年7月時点：安定11.3／次期12がプレビュー）

**軸1（フォーカス管理・最重要・要警戒）**: `FocusManager`を宣言的に呼び出す設計思想はWinUI3より明快だが、**Avalonia自身が「旧フォーカス機構は間に合わせだった」と認め、WinUI/UWP流のFocusManager（LosingFocus→GettingFocus→LostFocus→GotFocus階層イベント）へ12.0で作り直し中**（[Issue #7607](https://github.com/AvaloniaUI/Avalonia/issues/7607)）。つまりWinUI3が踏んだのと同系統のフォーカスモデルを後追いで移植している最中。2026年時点でも`area-focus`ラベルのOpen Issueが12件以上残存し、GuiEcadのIssue #6179同系統（アプリコード非経由の意図しないフォーカス遷移）の事例が現行版に存在する（下記R10）。GotFocus/LostFocusのHandled可否は公式ドキュメント未明記（不明）。

**軸2〜5**: `ICustomDrawOperation`でSkiaSharp直描画可、大量オブジェクト最適化の実践知見あり。Avalonia12（プレビュー）で35万要素規模FPS最大1867%向上との報告。PDFsharp直接統合の先行事例は未確認（不明。技術的障壁はないと推測）、SkiaSharp内蔵のためSKDocument経由PDF出力も選択肢。Windows/macOS/Linux等クロスPF対応（ecad2には過剰スペックの可能性）。XAML/MVVM知見はWPF/WinUI3から転用しやすい。

**長所**: MVVM構造の転用性／SkiaSharp高性能描画・Avalonia12の性能強化／フォーカスAPI設計思想はWinUI3より明快／PDF出力手段が複数（PDFsharp・SKDocument）／クロスPF将来性。

**短所・リスク**: **フォーカス関連Open Issueが12件以上と多く、Issue #6179同系統の事例(#14100)が現行版に存在**／GotFocus/LostFocusのルーティング仕様が非公式ドキュメント／PDFsharp統合の先行事例なし（要自前検証）／Windows専用用途ではクロスPF機構が不要な複雑さ／Avalonia12はまだプレビュー(2026-07時点)、フォーカス改善の恩恵を受けるには非安定版採用リスク。

**R10**: [Issue #14100](https://github.com/AvaloniaUI/Avalonia/issues/14100)（Open, 2024-01-03, `area-focus`/`bug`）— アプリ外からの復帰時に触っていないTextBoxへGotFocusが発火（Issue #6179同系統）。[Issue #18278](https://github.com/AvaloniaUI/Avalonia/issues/18278)（Open, 2025-02-21）— Focus()呼び出し後にGotFocusイベントが無限伝播。

**総合所見**: 「WinUI3のバグを回避する」という当初目的に対し、完全解決を保証する材料はない。むしろWinUI/UWP流フォーカスモデルを後追い実装中という経緯自体がリスク要因。採用検討時はプロトタイプでのフォーカス制御シナリオ実証を強く推奨。

### B-3. WinUI 3（続投案の再評価）

**軸1（フォーカス管理・最重要・一次情報で「続投は非推奨」の結論）**: **Issue #6179を一次情報(GitHub API)で確認**——`state_reason: not_planned`、2023-08-03にstale bot経由で**自動クローズ**（人間による修正判断ではなく無活動放置による自動終了）。2021-11-17を最後にMicrosoftからの実質コメントなし、2026年7月時点でも再オープン・修正予定の言及なし。**Microsoftが直す意思がないことが一次情報で確定**。さらに同種の暗黙フォーカス移動バグが他にも新規・継続報告されている：[Issue #10366](https://github.com/microsoft/microsoft-ui-xaml/issues/10366)（2025-02報告、Open、無効RadioButtonがTabナビゲーションを寸断）、[Issue #3825](https://github.com/microsoft/microsoft-ui-xaml/issues/3825)（2020年報告、Open、2025-11-11にも新規コメントで再燃「今も発生している」）。**これはGuiEcadの問題が孤立事例ではなく、WinUI3フォーカス管理サブシステム全体の構造的弱点であることを示す**。

**軸2〜5**: Win2D-WinUI最新1.4.0（2026-03、直前版から約13ヶ月）、保守継続だがOpen Issue 204件滞留＝メンテナンスモード寄り。PDFsharp統合はGuiEcad自身が実績を持つ（外部一次情報は乏しいがGuiEcadのコードが最有力な証拠）。Build 2026でMicrosoftが「WinUIは別後継を作らない・唯一の標準」と明言、ブランドを単に「WinUI」に変更（"one-off"でなく"the line"を強調）、ただしWindows App SDKのOS inbox化時期は不明。C#/.NET知見はGuiEcadと完全同一で転用最大（＝地雷再踏襲のリスクも最大）。

**長所**: GuiEcad資産をほぼそのまま転用可能／Win2D・PDFsharp実績あり／Build 2026でMicrosoftの継続コミット表明。

**短所・リスク**: **Issue #6179がnot_planned確定、同系統バグが2025年時点でも新規発生**／Win2Dはメンテナンスモード寄り／Windows App SDK inbox化時期不明／過去のSilverlight→UWP→WinUIの乗り換え履歴があり「コミット表明」と「実際のバグ修正体力」に乖離した前例。

**R10**: [Issue #6179](https://github.com/microsoft/microsoft-ui-xaml/issues/6179)（Closed, not_planned, 2023-08-03自動クローズ）／[Issue #10366](https://github.com/microsoft/microsoft-ui-xaml/issues/10366)（Open, 2025-02）／[Issue #3825](https://github.com/microsoft/microsoft-ui-xaml/issues/3825)（Open, 2020年〜2025-11再燃）。

**総合所見（続投可否）**: **「フォーカス所在をアプリが完全制御できるか」という最重要評価軸において、続投は妥当とは言い難い**。同種バグが構造的サブシステムの弱点として2025年時点でも新規発生しており、ecad2で同じ問題に再度直面する可能性が高い。

### B-4. .NET MAUI

**軸1（フォーカス管理・最重要・要警戒）**: MAUI on WindowsはWinUI3を**ネイティブ基盤として使う「ハンドラー・アーキテクチャ」**のため、フォーカス管理は最終的にWinUI3の`FocusManager`実装に委譲される。Issue #6179を直接継承するかの再現報告は見つからなかった（不明）が、**アーキテクチャ上は同種の問題を継承するリスクを否定できない**上、MAUI自身の抽象化層が問題の切り分けをさらに困難にする。加えてMAUI自体にも独自のフォーカスバグが多数（[Issue #27368](https://github.com/dotnet/maui/issues/27368) .NET9でFocused/Unfocusedの適用範囲が破壊的変更・not planned、[Issue #21053](https://github.com/dotnet/maui/issues/21053) Windows上でTab/クリックによるフォーカス解除不可）。**デスクトップキーボード操作支援自体がMicrosoft内部で「Backlog」＝未着手**（[Issue #14021](https://github.com/dotnet/maui/issues/14021)、Priority:1・コストXL、9件の依存課題）。

**軸2〜5**: `GraphicsView`(`ICanvas`)は存在するが再描画不具合・性能懸念あり、高性能自前描画には事実上SkiaSharp追加依存が必要。PDFsharpコア(6.2.x)はクロスPF設計でMAUI固有の障壁なし（GuiEcadのIRenderer抽象がほぼそのまま移植可能な数少ない利点）。モバイル・タッチ優先設計がデスクトップキーボード操作との相性で根本的にネック（macOS側もキーボードナビゲーション未成熟）。UI層（ハンドラーアーキテクチャ）はGuiEcadのWinUI3直接操作経験と別物で転用しにくい。

**長所**: PDFsharp統合に障害なし／SkiaSharp統合実績豊富／Windows/macOS/Android/iOS単一コードベース（ecad2には基本不要）。

**短所・リスク**: **WinUI3のフォーカスバグを継承しうる構造+MAUI自身の抽象化層が問題切り分けを困難化**／デスクトップキーボード操作支援が「Backlog」扱い／`GraphicsView`は性能・再描画に懸念／モバイル優先の設計思想がキーボードファースト要件と根本的に方向性が異なる。

**R10**: [Issue #14021](https://github.com/dotnet/maui/issues/14021)（Open, Priority:1, Backlog）— デスクトップ向け高度キーボード操作支援の包括的未対応（メニューアクセラレータ・Tabナビゲーション・Shift+Tab逆方向・macOSフォーカスイベント等9件）。根本要因側として[Issue #6179](https://github.com/microsoft/microsoft-ui-xaml/issues/6179)も参照。

**総合所見**: PDFsharp連携以外はキーボードファースト要件との相性が悪く、**現時点の一次情報からは積極的に推奨できない**。

### B-5. Uno Platform（2026年7月時点: Uno Platform 6.4系、.NET10対応）

**軸1（フォーカス管理・最重要・不明点が多いが楽観できない）**: `Microsoft.UI.Xaml.Input.FocusManager`を**Unoが独自にC#で丸ごと再実装**（ネイティブWindows App SDKのC++実装とはコードベースが別物）。公式は「WinUIのロジックにmatchする」と明言するが、これは「WinUI由来のクセを意図的に模倣する可能性」も意味する。**フォーカスナビゲーションEpic([Issue #5730](https://github.com/unoplatform/uno/issues/5730))が現在もOpen・達成率0/20**＝未完成。個別の乖離バグも継続報告（[#19134](https://github.com/unoplatform/uno/issues/19134) ListViewItemフォーカス挙動がWinUIと不一致、[#17384](https://github.com/unoplatform/uno/issues/17384) Closed済みだが「ポインタ操作後に別要素へフォーカスが飛ぶ」というGuiEcadと構造的に酷似した過去バグ）。**#6179が「そのまま継承されるか回避されるか」は一次情報からは断定不可（不明）、実機検証必須**。

**軸2〜5**: Win2D`CanvasControl`は**存在せず**、Uno独自フォーク`Uno.SkiaSharp`への**実質的な作り直し**が必要（2026年にSkiaSharp4.0共同メンテとして関与、最大24%高速化の報告あり・開発の勢いは強い）。PDFsharpとの直接統合実績は未確認（不明、技術的障壁はないと推測）。Windows専用用途にはクロスPF機構自体がオーバースペック（ヘッド選択WPF/Win32/GTK等の複雑さ）。自己完結型exe配布・ClickOnce配布は整備済み。Apache 2.0で無償。WinUI3のXAML/MVVM知見は転用しやすいが、**描画コア(IRenderer実装)はSkiaSharpへの新規作成**となり「知見転用による工数削減」効果は限定的。

**長所**: 無償・活発なOSS（2026年もSkiaSharp4.0共同メンテ等）／XAML/MVVM知見・UI非依存ビジネスロジック層の転用性／SkiaSharp描画の性能実績／配布経路整備。

**短所・リスク**: **フォーカス管理は独自再実装で挙動一致は非保証、関連Epicが未完成(0/20)**／Win2D非存在で描画コアは実質再実装／PDFsharp統合実績未確認／クロスPF機構がWindows専用用途にはオーバースペック／「WinUIのロジックに合わせる」設計方針自体がWinUI由来のクセを継承するリスクを内包。

**R10**: [Issue #22641](https://github.com/unoplatform/uno/issues/22641)（Open, 2026年時点）— WebView2子要素へフォーカスが奪われウィンドウ制御不能（アプリ意図しないフォーカス移動と同種）。加えて[#5730](https://github.com/unoplatform/uno/issues/5730)（Open, 0/20）・[#19134](https://github.com/unoplatform/uno/issues/19134)（Open, 2024-12）。

**総合所見**: 「WinUIバグを回避している」という肯定的根拠は見つからず、独自再実装ゆえの新たな乖離バグとフォーカスEpic未完成が確認された。採用前に実機での直接検証が必須。描画コアの実質作り直しにより知見転用効果もGuiEcad期待值より小さい可能性。

---

## Part B 比較表

| 評価軸 | WPF | Avalonia | WinUI3(続投) | Uno Platform | MAUI |
|---|---|---|---|---|---|
| **軸1 フォーカス決定性【最重要】** | ◎ 明示的スコープAPI＋`PreviewLostKeyboardFocus`でキャンセル可能。#6179型の一次証拠は未発見（不明） | △ 宣言的設計だが2026年もarea-focus Open Issue多数、#6179同系統事例あり(#14100) | **× #6179がnot_planned確定(一次情報)。同系統バグが2025年も新規発生、構造的弱点と判明** | △ 独自再実装で挙動一致は非保証、Epic未完成(0/20)。#6179継承有無は不明 | **× WinUI3基盤に依存し継承リスク＋MAUI独自バグ＋デスクトップキー操作がBacklog扱い** |
| **軸2 高性能自前2D描画** | ◎ DrawingVisual+VisualTreeHelper.HitTest、CAD向け実績豊富 | ◎ SkiaSharp直描画、Avalonia12で大幅性能強化予定 | ◯ Win2D実績あるがメンテナンスモード寄り(Open Issue204件) | ◯ Uno.SkiaSharp（Win2D非存在、実質作り直し） | △ GraphicsViewは性能懸念、実質SkiaSharp依存必要 |
| **軸3 ベクターPDF出力** | ◎ 専用NuGet`PDFsharp-wpf`あり、統合実績最高 | ◯ PDFsharp/SKDocument両対応、直接統合の先行事例は不明 | ◎ GuiEcad自身が実績を持つ | △ 統合実績未確認（技術的障壁はないと推測） | ◯ PDFsharpコアがクロスPF設計で障壁なし |
| **軸4 デスクトップ優先性** | ◎ Windows専用、GuiEcadと同じ前提 | ◯ クロスPFだがWindows専用でも性能ペナルティ小、機構自体は過剰 | ◎ Windows専用、Build2026で継続コミット表明（ただし過去の乗り換え履歴あり） | △ クロスPF機構が明確にオーバースペック | △ モバイル優先設計、デスクトップは発展途上 |
| **軸5 C#/.NET知見再利用** | ◎ XAML/MVVM/ルーテッドイベント直接転用可 | ◎ XAML/MVVM転用しやすい | ◎◎ GuiEcadと完全同一（地雷再踏襲リスクも最大） | ◯ XAML/MVVM転用可だが描画コアは新規 | △ ハンドラーアーキテクチャで転用しにくい |
| **R10 既知バグの深刻度** | 中（相互運用境界に限定、#6179型は未発見） | 中〜高（area-focus Open12件超、#6179同系統あり） | **最高（#6179確定+構造的サブシステム弱点）** | 中〜高（独自実装ゆえ不明点多い、新規乖離バグあり） | 高（WinUI3継承リスク＋独自Backlog課題） |

## Part B 総合考察

1. **WinUI3続投は非推奨**: 一次情報（GitHub Issue の `state_reason`）でMicrosoftが#6179を修正する意思がないことが確定し、同系統の暗黙フォーカス移動バグが2025年時点でも新規発生している。GuiEcadの9ラウンドにわたる教訓が「孤立事例」ではなく「WinUI3フォーカス管理サブシステム全体の構造的弱点」であると、Part B調査により裏付けられた。
2. **WinUI3基盤に依存する候補（Uno Platform, MAUI）は同種リスクを引き継ぐ可能性**があり、いずれも独自の抽象化層が問題の切り分けをさらに困難にする。特にMAUIはデスクトップキーボード操作支援自体がMicrosoft内部で「Backlog」扱いであり、キーボードファーストという核心要件と設計思想レベルで相性が悪い。
3. **WPFが軸1で最も具体的な安心材料を持つ**: `FocusManager`のスコープAPI・`PreviewLostKeyboardFocus`の`e.Handled`によるキャンセル機構という、WinUI3にはない明示的な防御線がある。#6179型の障害報告は調査範囲内で見つからず、残存する既知バグも`WindowsFormsHost`等の相互運用境界に限定的。ただし「WPFなら絶対に同種バグが起きない」と断定できる一次情報はなく、あくまで「構造的リスクが相対的に低い」という評価にとどまる。
4. **Avaloniaは次点**: フォーカスAPIの設計思想はWinUI3より明快だが、2026年時点でarea-focusラベルのOpen Issueが12件以上と多く、#6179同系統の事例（#14100）も現行版に存在する。加えて「WinUI/UWP流フォーカスモデルを後追い実装中」という経緯自体が、GuiEcadが踏んだ問題を新たに移植するリスクを孕む。
5. **どの候補も「#6179を確実に回避できる」という一次情報レベルの保証はない**。R10で確認した既知バグはいずれも「発生しない証拠」ではなく「発生確率の相対比較」に留まる。**最終選定前に、有力候補（WPF最有力、Avalonia次点）でプロトタイプによる実機検証（PointerPressed→PointerReleased後のフォーカス保持シナリオ）を行うことを強く推奨する**。
