# ecad2 技術スタック調査: ネイティブ/軽量系（Qt・Dear ImGui・egui・Slint・GTK4）

- 担当: 侍
- 調査方法: 自セッション内の並列サブエージェント5体による調査（実装は一切行っていない）
- 一次情報の基準時点: 2026年7月
- 評価軸: (1)フォーカス管理APIの決定性【最重要】／(2)高性能な自前2Dベクター描画／(3)ベクターPDF出力経路／(4)デスクトップ優先／(5)C#資産の再利用可能性（加点）／(6)フォーカス・キー入力関連の既知バグ・Issue

背景: 既存のGuiEcad（WinUI3/C#）はフォーカス管理がOS/フレームワーク内部の暗黙ロジックに委ねられ非決定的（Microsoft側の既知バグを含む）であったことに長年悩まされた。次期プロジェクト「ecad2」の技術選定では、フォーカス管理APIが宣言的・確実か（決定的か）を最重要視する。

---

## 比較表（要約）

| 候補 | (1) フォーカス決定性【最重要】 | (2) 2D描画 | (3) PDF出力 | (4) デスクトップ優先 | (5) C#再利用 |
|---|---|---|---|---|---|
| Qt/PySide | `Qt::FocusReason`付きイベントで原因追跡可能。宣言的API（`focusPolicy`等）豊富。ただしポップアップ/複合エディタ絡みで3〜5年未解決バグ実績あり（QTBUG-11554はGuiEcad症状に酷似） | QPainter+QGraphicsView、公式に「CAD/diagram editor向け」と明記。最成熟 | `QPdfWriter`で同一QPainterコードをPDF出力に転用可（標準機能） | Widgetsは伝統的デスクトップUI向けと明言、妥協なし | ほぼ不可（Qyoto/QtSharp死亡、新Qt BridgesはBeta・Quick限定・Widgets非対応） |
| Dear ImGui | メンテナ自身がFocus/Active概念混同を認める。`SetKeyboardFocusHere()`が次フレームまで反映されずキー入力喪失（#7473） | `ImDrawList`高性能だがベクター情報非保持（毎フレーム使い捨て）、ベクター拡張提案（#6784）は不採用クローズ | 標準機能なし、高レベル図形情報も持たず自作橋渡し必須（#6384） | ツール/デバッグ用途向けと明言、一般エンドユーザーUIは対象外 | ImGui.NETはコア本体に約1年遅延、元メンテナ撤退・コミュニティ引継ぎ |
| egui (Rust) | `Memory`/`Context`経由で完全API管理、透明性は5候補中最高。ただし1フレーム遅延の状態機械、ID一意性はアプリ責務（#4940） | `epaint`がShapeベクター列→テッセレーション、Rerun Viewerで実績 | 標準機能なし、`printpdf`等外部クレート併用必須、成熟解なし | `eframe`はネイティブ一級だがWeb/ゲームエンジンも同格 | 事実上ゼロ、FFI連携（csbindgen等）も逃げ道の域 |
| Slint | `focus()`/`forward-focus`等の宣言的API群あるが、クリック外しで`TextInput`がフォーカス保持し続ける未解決（#3578、3年Open） | Skia/FemtoVG、`Path`要素で宣言的ベクター描画 | 標準機能なし、自前実装必須 | 組み込みと並ぶ主軸、投資継続中（v1.17.0、2026-06-24） | 非公式実験段階（slint-dotnet）、Linux限定・Windows未対応、事実上使えない |
| GTK4 | `EventControllerFocus`は観察のみ。GTK3の宣言的`focus-chain`はGTK4で廃止済み。2025〜2026にも新規バグ継続（#7952,#8057,#8227） | `GtkDrawingArea`+Cairoだが GSK内ではフォールバック扱い（3D変換非対応） | Cairo PDFサーフェス・`GtkPrintOperation.set_export_filename()`で成熟 | libadwaitaはモバイル収斂志向、X11/Broadway非推奨化進行、主軸はLinux/Wayland | GtkSharpはGTK4非対応、Gir.Coreは1.0未満プレビュー品質（v0.8.0） |

---

## 候補別詳細

### 1. Qt（C++）/ PySide（Qt for Python）

**(1) フォーカス管理APIの決定性**
`QWidget::focusPolicy`（`NoFocus`/`TabFocus`/`ClickFocus`/`StrongFocus`/`WheelFocus`）で受け入れ方針を宣言的に設定。`setFocus(Qt::FocusReason)`/`clearFocus()`/`focusNextChild()`/`setTabOrder()`で明示制御。最大の利点は、フォーカスイベントに必ず理由（`Qt::FocusReason`: Mouse/Tab/Backtab/ActiveWindow/Popup/Shortcut/MenuBar/Other等）が付与され、`QFocusEvent::reason()`で原因を必ず特定できる点。ただし公式ドキュメントも「マウスホイールでのフォーカス移動はプラットフォーム間で挙動が異なる」と明記しており完全決定論ではない。
出典: [QWidget Class](https://doc.qt.io/qt-6/qwidget.html), [Keyboard Focus in Widgets](https://doc.qt.io/qt-6/focus.html), [QFocusEvent Class](https://doc.qt.io/qt-6/qfocusevent.html)

**(2) 2Dベクター描画**
`QPainter`+`paintEvent`（Win2D CanvasControlに近い自前描画）。`QGraphicsView`/`QGraphicsScene`はBSPツリー空間インデックス・アイテムキャッシュ対応で、公式に「CAD programs, diagram editors and games に最適」と明記。
出典: [QPainter Class](https://doc.qt.io/qt-6/qpainter.html), [Graphics View Framework](https://doc.qt.io/qt-6/graphicsview.html)

**(3) ベクターPDF出力**
`QPdfWriter`（QPainter用ペイントデバイス）でPDF生成、複数ページ・PDFバージョン・カラーモデル対応。画面描画と同じQPainterコードを再利用でき、GuiEcadの`IRenderer`抽象と同じ発想が標準で用意されている。
出典: [QPdfWriter Class](https://doc.qt.io/qt-6/qpdfwriter.html)

**(4) デスクトップ優先**
公式ドキュメントがQt Widgetsを「成熟・機能豊富、伝統的なデスクトップ中心UIに最適」「タッチスクリーンや流麗なアニメーションには不向き」と明言。Widgetsを選ぶ限りモバイル/Web兼用の妥協は組み込まれない。
出典: [Qt Widgets](https://doc.qt.io/qt-6/qtwidgets-index.html)

**(5) C#資産の再利用可能性**
Qyotoは開発終了（obsolete）、後継QtSharpも長期放棄・Qt5専用でQt6非対応。2025年5月にQt社が「Qt Bridges」（C#/.NET含む多言語統合）を発表したが、現状は早期アクセス/ベータ段階、Windows(x64)/Linux(x86_64)限定、Qt Quick/QMLのみが対象でQt Widgetsは対象外。既存WinUI3コードの移植経路は現状存在しない。
出典: [Languages/QtSharp - KDE TechBase](https://techbase.kde.org/Languages/QtSharp), [GitHub - ddobrev/QtSharp](https://github.com/ddobrev/QtSharp), [Qt Bridges](https://www.qt.io/qt-bridges), [Phoronix記事](https://www.phoronix.com/news/Qt-Bridges-New-Languages)

**(6) 既知バグ・Issue**
- [QTBUG-69710](https://bugreports.qt.io/browse/QTBUG-69710)「QLineEdit can't be used for keyboard input after popup menu from another QDialog」2018-08-01報告→2021-09-30（Qt6.3.0）解決。約3年。
- [QTBUG-11554](https://bugreports.qt.io/browse/QTBUG-11554)「QTableView loses focus when tabbing in combination with custom editor which is a complex widget」2010-06-18報告→2015-08-17解決。**GuiEcadの「インライン編集完了後にキーボード入力が別コントロールに奪われる」症状に酷似**。5年間未解決だった。
- [QTBUG-46812](https://bugreports.qt.io/browse/QTBUG-46812) Alt+ショートカットのキー離す順序でQMenuBarハイライト残留、約1年で修正。

**適性総括**: フォーカス変更の原因を必ず特定できる点でWinUI3より「調査可能・介入可能」。ただしポップアップ・複合エディタ絡みのフォーカス受け渡しはQtでも長年（3〜5年）未解決だった実績があり「バグが皆無」ではない。CAD/PDF要件への適合度は5候補中最高。C#資産の再利用性はほぼゼロ、C++またはPythonでのフルスクラッチが前提。

---

### 2. Dear ImGui

**(1) フォーカス管理APIの決定性**
メンテナocornut自身がDiscussion #3945で「Focus」（Navigation有効時のハイライト対象）と「Active」（ボタン押下中・編集中）の概念混同を認め、`SetKeyboardFocusHere()`が両方を同時に操作してしまう矛盾を説明。[Issue #7473](https://github.com/ocornut/imgui/issues/7473)「How to immediately focus elements」(Open, 2024-04-05)は`SetKeyboardFocusHere()`が次フレームまで反映されず、フォーカス切替と同時に来たキー入力が失われる問題を報告——GuiEcadの症状と構造的に類似。[Issue #5805](https://github.com/ocornut/imgui/issues/5805)(Open, 2022-10-21)もオンスクリーンキーボードでの`io.WantTextInput`とフォーカス保持の競合を報告。
出典: [Discussion #3945](https://github.com/ocornut/imgui/discussions/3945), [Issue #7473](https://github.com/ocornut/imgui/issues/7473), [Issue #5805](https://github.com/ocornut/imgui/issues/5805)

**(2) 2Dベクター描画**
`ImDrawList`は最適化された頂点バッファをDirectX9-12/Vulkan/Metal/OpenGL/WebGPU等へ出力。太線・AA最適化で頂点50%減の実績。ただし毎フレーム三角形へテッセレーションして描き捨てる設計で「ベクター図形として保持」しない。[Issue #6784](https://github.com/ocornut/imgui/issues/6784)「Vector Graphics Support」はテッセレーションライブラリ統合提案だが応答なくクローズ（不採用）。32bitインデックス設定なしでは頂点数上限65536の制約あり。
出典: [imgui README](https://github.com/ocornut/imgui), [Issue #6784](https://github.com/ocornut/imgui/issues/6784)

**(3) ベクターPDF出力**
標準機能なし。[Issue #6384](https://github.com/ocornut/imgui/issues/6384)「Rendering to SVG and preserving info about higher-level primitives」で報告者が「三角形テッセレーション後の頂点データしか保持せず高レベル図形情報が失われる」と指摘、自作`Primitive`構造体パッチで対応した例があるが本家未マージ。GuiEcad同様の自前`IRenderer`層構築が前提。
出典: [Issue #6384](https://github.com/ocornut/imgui/issues/6384)

**(4) デスクトップ優先**
README明記：「designed to enable fast iterations...content creation tools and visualization / debug tools (as opposed to UI for the average end-user)」。一般エンドユーザー向けUIは明示的に対象外。
出典: [imgui README](https://github.com/ocornut/imgui)

**(5) C#資産の再利用可能性**
主要バインディングImGui.NET（NuGet最新v1.91.6.1、2025-01-06）は元メンテナが2023年2月から撤退表明、コミュニティ引継ぎ。コア本体はv1.92.8まで進み約1年・複数マイナー分の遅延あり。View層は丸ごと作り直しになるがModel/Simulation等UI非依存部分は流用可能。
出典: [ImGui.NET NuGet](https://www.nuget.org/packages/ImGui.NET), [imgui Releases](https://github.com/ocornut/imgui/releases)

**(6) 既知バグ・Issue**
上記(1)参照: #7473（Open）、#5805（Open）、Discussion #3945。

**適性総括**: 毎フレーム自前で状態を再計算する透明性はあるが、Focus/Active概念混同・1フレーム遅延によるキー入力取りこぼしという開発元も認める別種の非決定性を抱え、「予測可能なフォーカス管理」を無条件に満たさない。ベクターPDF出力・図形情報保持はコアが意図的に提供せず自作コスト大。

---

### 3. egui（Rust）

**(1) フォーカス管理APIの決定性**
フォーカス状態は`egui::Memory`（`Context`経由）に一元管理。`Memory::focused()`/`has_focus()`/`request_focus()`/`surrender_focus()`/`move_focus()`/`set_focus_lock_filter()`で完全にAPI経由で照会・制御可能——WinUI3的な「フレームワーク内部の非公開ヒューリスティックによる暗黙のフォーカス奪取」は構造的に存在しない。ただし`Focus`構造体は`id_previous_frame`/`id_next_frame`を持ち、公式ドキュメントに「次フレームで反映される」と明記——1フレーム遅延の状態機械である。[Issue #1655](https://github.com/emilk/egui/issues/1655)(Open, 2022-05-21)はこの遅延に起因するフォーカスの一瞬の"flicker"を報告。IDベースの状態紐付けのためID一意性はアプリ責務で、[Issue #4940](https://github.com/emilk/egui/issues/4940)(Open)はドラッグ可能領域+コンテキストメニューでのID重複警告を報告。
出典: [Memory in egui](https://docs.rs/egui/latest/egui/struct.Memory.html), [Focus in egui::memory](https://doc.servo.org/egui/memory/struct.Focus.html), [Issue #1655](https://github.com/emilk/egui/issues/1655), [Issue #4940](https://github.com/emilk/egui/issues/4940)

**(2) 2Dベクター描画**
`epaint`の`Shape`列挙体（Path/CubicBezier/QuadraticBezier/Rect/Circle/Text/Mesh/Callback等）が真のベクタープリミティブを保持してからテッセレーション。Rerun Viewer（eguiメンテナ企業のフラッグシップ製品）で高密度データ可視化の実績あり。`rayon`による並列テッセレーションオプションあり。
出典: [epaint docs.rs](https://docs.rs/epaint), [Shape in egui](https://docs.rs/egui/latest/egui/enum.Shape.html)

**(3) ベクターPDF出力**
標準機能なし。`epaint`のShape列を`printpdf`（線・図形・ベジェ曲線対応、`svg2pdf`経由のSVG埋め込みも可）へ変換する自作ブリッジが現実的経路。GuiEcadの`IRenderer`と設計思想的に最も整合するが、成熟した「egui→PDF」専用ソリューションはない。参考事例のMuginCAD（Rust+egui製2D CADエンジン）はPDF出力を謳うがGitHub star 0の初期段階プロジェクト。
出典: [printpdf](https://github.com/fschutt/printpdf), [MuginCAD](https://github.com/Hakkology/MuginCAD)

**(4) デスクトップ優先**
`eframe`はネイティブ（Windows/macOS/Linux、`winit`+`wgpu`/`glow`）を一級市民として扱うが、Web（WebGPU/wasm）も同格のクロスプラットフォーム設計。印刷ダイアログ・UIAツリー・MSIXパッケージング等のOS統合機能はエコシステムクレート（`accesskit`等）頼みで、[Issue #4527](https://github.com/emilk/egui/issues/4527)はaccesskit有効時の高CPU使用率を報告——この統合はまだ発展途上。
出典: [eframe docs.rs](https://docs.rs/eframe/latest/eframe/), [Issue #4527](https://github.com/emilk/egui/issues/4527)

**(5) C#資産の再利用可能性**
コードの直接再利用は事実上ゼロ。`csbindgen`（Cysharp製）等でFFI連携は技術的に可能だが、「ecad2として一新する」前提と矛盾しやすく複雑性を追加するだけの逃げ道。Model/Simulationは設計仕様としての移植（Rust再実装）は可能。
出典: [csbindgen](https://github.com/Cysharp/csbindgen)

**(6) 既知バグ・Issue**
[#5609](https://github.com/emilk/egui/issues/5609)(Open, 2025-01-15, TextEditがEscキー等を消費せずフォーカス喪失とアプリ側ハンドラが二重発火)、[#2877](https://github.com/emilk/egui/issues/2877)(Closed, DragValueでTab移動時に入力値喪失)、[#2142](https://github.com/emilk/egui/issues/2142)(Closed, Tabでパネル先頭へ飛びlost_focusも発火せず)、[#4940](https://github.com/emilk/egui/issues/4940)(Open)、[#1655](https://github.com/emilk/egui/issues/1655)(Open)。日本語IME成熟度は本調査では実績薄く要実機検証。

**適性総括**: フォーカス管理の透明性・決定性は5候補中最高（原因を必ず`Memory`経由で追跡可能）。ただし1フレーム遅延・ID一意性というimmediate mode特有の制約は残る。PDF出力層は自作コスト大、C#資産再利用はほぼゼロでフルスクラッチ前提、IME実績は要検証。

---

### 4. Slint

**(1) フォーカス管理APIの決定性**
`FocusScope`要素が`has-focus`/`enabled`/`focus-on-click`/`focus-on-tab-navigation`プロパティと`focus()`/`clear-focus()`関数、`focus-changed-event`等のコールバックを公開。`Window`/`PopupWindow`の`forward-focus`で初期フォーカス要素を宣言的に指定可能——WinUI3よりは宣言的。ただし[Issue #3578](https://github.com/slint-ui/slint/issues/3578)「Focus doesn't go away when clicking outside TextInput」(2023-09-30報告、**3年近くOpen**)はクリック外しでTextInputが自動的にフォーカスを失わない設計上の制約を報告、回避策は「ルート全体を覆うFocusScope+TouchArea」という公式にも"hack"と呼ばれる手法のみ。[Issue #7159](https://github.com/slint-ui/slint/issues/7159)「Components Losing Focus when they shouldn't be」(2024-12-18報告)はスクロールで部分的に隠れた状態でLineEditに入力すると即座にフォーカスを失う不具合で1.9.0で迅速に修正された。
出典: [FocusScope reference](https://docs.slint.dev/latest/docs/slint/reference/keyboard-input/focusscope/), [Focus Handling guide](https://docs.slint.dev/latest/docs/slint/guide/development/focus/), [Issue #3578](https://github.com/slint-ui/slint/issues/3578), [Issue #7159](https://github.com/slint-ui/slint/issues/7159)

**(2) 2Dベクター描画**
3レンダラー: Skia（最高機能）、FemtoVG（軽量、テキスト/パス品質が「最適でない場合がある」と明記）、Software（CPUのみ、回転/スケール等未対応）。`.slint`言語内の`Path`要素でSVGライクな宣言的ベクター描画が可能。Rust APIの`Window::set_rendering_notifier()`で低レベルカスタム描画も可能だがC#提供は未確認。
出典: [Backends & Renderers](https://docs.slint.dev/latest/docs/slint/guide/backends-and-renderers/backends_and_renderers/), [Path element](https://docs.slint.dev/latest/docs/slint/reference/elements/path/)

**(3) ベクターPDF出力**
標準機能なし（"print"/"pdf"関連の機能要求はIssue検索でヒットせず）。GuiEcadと同様のIRenderer自作パターンが必要。

**(4) デスクトップ優先**
「Embedded, Desktop, Mobile」を並列主軸とし、Web対応は明確に副次的・デモ用途。組み込み用途が最も成熟しているが、デスクトップ専用ページ（slint.dev/desktop）でNative/Fluent/Cupertinoスタイルのネイティブバイナリ生成を謳い投資継続中（直近v1.17.0、2026-06-24）。
出典: [Slint Desktop](https://slint.dev/desktop)

**(5) C#資産の再利用可能性**
非公式バインディング`slint-dotnet`（microhobby/slint-dotnet）はREADMEに「experimental and not ready for production use!」と明記。star49、Issue/PR共に0件で活動停滞、最終リリースv1.7.1（2024-08-11）でコア1.17系に対し大幅遅延。**サポートプラットフォームがLinux x64/arm/arm64のみでWindows対応の記載なし**——GuiEcadの実行環境（Windowsデスクトップ）と不整合。型サポートも不完全。
出典: [slint-dotnet](https://github.com/microhobby/slint-dotnet), [Discussion #3550](https://github.com/slint-ui/slint/discussions/3550)

**(6) 既知バグ・Issue**
上記(1)参照: #7159（1.9.0で修正済み）、#3578（3年Open）。

**適性総括**: 宣言的APIの表面は充実しているが、クリック外し時の自動フォーカス解除が3年間未解決という核心的な制約が残る。C#バインディングが非公式・実験段階でWindows未対応のため、GuiEcadのC#資産を活かす道は事実上閉ざされている。

---

### 5. GTK4

**(1) フォーカス管理APIの決定性**
`Gtk.EventControllerFocus`は`enter`/`leave`シグナルと`is-focus`/`contains-focus`プロパティで検知のみ可能（制御は不可）。`Widget.grab_focus()`は明示要求できるが失敗理由は不透明。**GTK4ではGtkContainerが全廃され、GTK3にあった宣言的な`set_focus_chain`（順序リストで明示指定）も削除**。代替は各ウィジェットの`focus()` vfuncオーバーライドという命令的方式のみで、既定実装は内部ツリー構造・幾何ヒューリスティックでフォーカス移動先を探索する。[Issue #7952](https://gitlab.gnome.org/GNOME/gtk/-/issues/7952)(2025-12-25報告、Open)は横並びBox2列で右矢印キーが視覚的に同じ行ではなく別行へ「跳ぶ」不具合。[Issue #8057](https://gitlab.gnome.org/GNOME/gtk/-/issues/8057)(2026-02-21、Open)は`gtkstack.c`の条件式次第でフォーカス委譲挙動が変わる問題。[Issue #8227](https://gitlab.gnome.org/GNOME/gtk/-/issues/8227)(2026-06-02、Open)はX11環境でウィンドウがキーボードフォーカスを得られない重大バグ。
出典: [Gtk.EventControllerFocus](https://docs.gtk.org/gtk4/class.EventControllerFocus.html), [migrating-3to4](https://docs.gtk.org/gtk4/migrating-3to4.html), [vfunc.Widget.focus](https://docs.gtk.org/gtk4/vfunc.Widget.focus.html), [Issue #7952](https://gitlab.gnome.org/GNOME/gtk/-/issues/7952), [Issue #8057](https://gitlab.gnome.org/GNOME/gtk/-/issues/8057), [Issue #8227](https://gitlab.gnome.org/GNOME/gtk/-/issues/8227)

**(2) 2Dベクター描画**
`GtkDrawingArea`+`set_draw_func()`でCairo直接描画は第一級サポート。ただしGTK4の新描画層GSK内では「CairoレンダラーはGSKの比較用フォールバックに位置づけられ3D変換非対応、使用は避けるべき」と明記。CAD向けの公式ベンチマークは不明。
出典: [Gtk.DrawingArea](https://docs.gtk.org/gtk4/class.DrawingArea.html), [Gsk.CairoRenderer](https://docs.gtk.org/gsk4/class.CairoRenderer.html)

**(3) ベクターPDF出力**
Cairo自体の`cairo_pdf_surface_create`で成熟した多ページベクターPDF生成が可能。GTK統合経路として`Gtk.PrintOperation.set_export_filename()`で印刷ダイアログを経ずファイル直接エクスポート可（現状PDFのみ対応）。
出典: [Cairo PDF Surfaces](https://www.cairographics.org/manual/cairo-PDF-Surfaces.html), [Gtk.PrintOperation](https://docs.gtk.org/gtk4/class.PrintOperation.html)

**(4) デスクトップ優先**
GNOME独自の`libadwaita`はデスクトップ・ラップトップ・タブレット・スマートフォンへの「アダプティブ」適応を志向し、GNOMEエコシステム全体はモバイルとの収斂を含む方向性。Win32バックエンドは「デフォルト有効な成熟バックエンド」だがX11/Broadwayは2025年にGTK5での削除を前提に非推奨化——開発の主軸はLinux/Wayland。
出典: [libadwaita](https://news.itsfoss.com/gnome-libadwaita-library/), [GTK on Windows](https://docs.gtk.org/gtk4/windows.html)

**(5) C#資産の再利用可能性**
GtkSharpはGTK3(3.22)/.NET Standard 2.0のみ対象で**GTK4非対応**、最新リリース2021年4月で停滞。Gir.Core（GTK4向け新バインディング）はNuGet配布・開発継続中（直近v0.8.0、2026-06-25）だが「1.0未満でAPIは破壊的変更対象」と明言、ネイティブ仮想関数のオーバーライド未対応等の制約あり。
出典: [GtkSharp](https://github.com/GtkSharp/GtkSharp), [Gir.Core](https://github.com/gircore/gir.core)

**(6) 既知バグ・Issue**
上記(1)参照: #7952（Open）、#8057（Open）、#8227（Open、重大）。その他 #4880（Closed 2022）、#1425（2018年報告、2024年時点でOpen）。

**適性総括**: フォーカス移動先の「決定」は各ウィジェットのfocus() vfunc既定実装（内部ツリー順序・方向ヒューリスティック）に委ねられ、GTK3にあった宣言的focus-chain APIもGTK4で廃止済み。2025〜2026年にも新規フォーカスバグが報告され続けており、WinUI3と同種の非決定性リスクを構造的に抱える。PDF/描画インフラはCairo基盤で成熟しているが、C#バインディングもGtkSharp非対応・Gir.Coreプレビュー品質で薄い。

---

## 総合考察

- **フォーカス決定性（最重要軸）**では egui（完全API管理・透明性最高）と Qt（`FocusReason`付きイベントで原因追跡可能・実績豊富）が相対的に優位。Dear ImGui/Slint/GTK4はいずれも改善はあれど暗黙ロジック/フレーム遅延/内部ヒューリスティックが残り、WinUI3と同種のリスクを構造的に抱える。
- **PDF/描画インフラの成熟度**は Qt が頭一つ抜ける（`QPainter`→`QPdfWriter`へそのまま転用可、GuiEcad現行`IRenderer`設計と自然合致）。GTKも Cairo 経由で同等クラスの経路を持つがフォーカス面の懸念が大きい。egui/Dear ImGui/Slintはいずれもベクター保持+PDF橋渡し層を新設する追加コストが要る。
- **C#資産の再利用**は5候補いずれも「View層は全面書き直し」が前提で決定打なし（Qt BridgesもGir.Coreもベータ/プレビュー品質）。GuiEcadのUI非依存部分（Model/Simulation）は設計知識としてどの言語へも移植可能、という程度に留まる。
- **総括**: フォーカス決定性を最優先するなら egui（Rust）か Qt（C++/Python）。PDF/描画の実務成熟度重視なら Qt。ただしいずれもC#資産は活かせずフルスクラッチが前提。Dear ImGui/Slint/GTK4は各々（フォーカス決定性/PDF成熟度/C#連携）のいずれかで明確な弱点を持つ。
