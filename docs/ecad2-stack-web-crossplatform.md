# ecad2技術スタック調査：Web＆クロスプラットフォーム系 — 2026-07-03（忍者）

> 家老依頼。ecad2(C:\ECAD2)技術スタック選定、忍者担当ファミリー。
> 対象: Electron / Tauri / Neutralino / Flutter desktop / Compose Multiplatform / JavaFX
> （隠密が.NET系、侍がQt/ImGui/egui/Slint/GTK系を並行調査、家老が統合）
> 各候補、並列サブエージェントによるWeb調査（2026年最新一次情報）。**調査のみ、実装は行っていない。**

## 評価軸

1. 【最重要】フォーカス管理APIの決定性（宣言的・確実 vs 暗黙・非決定的委譲）
2. 高性能な自前2Dベクター描画（グリッド・記号・ヒットテスト）
3. ベクターPDF出力経路
4. デスクトップ優先での適性（クロスPF加点）
5. C#資産の再利用可能性（加点）
6. (R10) フォーカス/キー関連の既知バグ・Issue

## 比較表

| 候補 | R1 フォーカス決定性 | R2 描画性能 | R3 PDF出力 | R4 デスクトップ適性 | R5 C#再利用 |
|---|---|---|---|---|---|
| **Electron** | △ 単一エンジンで比較的マシだが無条件安全ではない | ○ Canvas2D/WebGL/WebGPU標準サポート | △ Canvas直結はラスター化の罠（要IRenderer的分離） | ○ electron-builder成熟、実績多数だが大規模図でラグ報告 | △ edge-jsで技術的可能だが移行動機と矛盾 |
| **Tauri** | △ 致命的単一バグはないが「多数の小さな地雷原」 | △ Canvas2Dは良好、WebGL/WebGPUはLinux(WebKitGTK)で不安定 | ○ Rust側svg2pdf/printpdf等で真のベクトル出力可 | ○ 2.0安定、WebView2標準搭載で実運用障壁低い | ✕ 公式相互運用パスなし |
| **Neutralino** | △〜✕ 解決済み1件＋未解決2件、フレームワークが薄い分OS依存の気まぐれが露出 | ○ 制約なしだがWebView実装差で性能ばらつき | ○ jsPDF+svg2pdf.js等で対応可能 | △ 軽量だがエコシステム薄い、直近サプライチェーン侵害あり | ✕ Extensions APIでプロセス分離のみ |
| **Flutter desktop** | ✕ Windows desktop固有の未解決重大バグ(#151457)＋設計上の欠陥(#107037) | ○ CustomPainter+Skia/Impeller、複雑パスでスタッター報告あり | ○ dart_pdfパッケージでネイティブベクターPDF生成可 | △ Desktop対応はstableだが主戦場はモバイル | ✕ dart:ffiはC ABIのみ、書き直し前提 |
| **Compose Multiplatform** | △ API設計は宣言的だがmacOSでrequestFocus()が約50%失敗(#4803)等 | △ 複雑Path図形は1万要素から不安定化、10万要素で2FPS | △ Skiko統合PDF機能が未マージで停滞 | △ 機能は揃うが枯れ具合は発展途上、大規模事例乏しい | ✕ gRPC/REST等プロセス間のみ |
| **JavaFX** | △ フォーカス委譲の標準API不在の議論が今も継続(JDK-8343956) | ○ Canvas+GraphicsContextはGuiEcadのWin2D設計とほぼ同型、転用しやすい | ○ PDFBox/OpenPDFで実現可能、IRenderer設計と同型で移植しやすい | ○ 長期実績・jpackage成熟だがコミュニティ規模小 | ✕ JNI/商用ブリッジのみ |

**R6(C#再利用)は6候補すべて実質「書き直し前提」**（.NET系以外を選ぶ場合の共通コスト）。

## 各候補の詳細考察

### Electron
Chromium固定のため複数フレームワーク間の非決定性は避けやすいが、Windows限定で`alert()`後に入力欄がフォーカス不能になる既知バグ（[#19977](https://github.com/electron/electron/issues/19977)、2019年報告・2026年5月時点も更新継続、6年以上未修正）がある。ただしOSダイアログ境界起因で、WinUI3 #6179（フレームワーク内部起因）とは性質が異なる。`webContents.printToPDF()`にCanvas描画を渡すとラスター画像として埋め込まれる落とし穴があり、PDFKit等でGuiEcadの`IRenderer`的な分離設計が必要になる。draw.io DesktopというElectron製ベクター図形CADの実績があるが、開発元自身が大規模図での「顕著なラグ」を認めている。

### Tauri
OS標準WebView（Windows=WebView2, macOS=WKWebView, Linux=WebKitGTK）に全面委任するため、単一の「決定打」バグはないが、tauri/tao/wry・WebView2側で15件以上の未解決/解決不明フォーカスissueが見つかった。「多数の小さな地雷原」という性質で、プラットフォームごとの個別検証が前提になる。代表例: `tao#940`（KeyboardInput WindowEvent発火せず、"not planned"）、`MicrosoftEdge/WebView2Feedback#5144`（input要素が即座にフォーカスを失う退行バグ）。WebGL/WebGPUはLinux(WebKitGTK)で2026年時点も実験的扱いのため、Linux対応を求めるなら危険域。

### Neutralino
軽量性（配布サイズ約2MB）が魅力だが、Windows環境でネイティブウィンドウがフォーカスされてもHTMLへキーボードフォーカスが渡らない不具合が報告・修正された実績があり（[#1491](https://github.com/neutralinojs/neutralinojs/issues/1491)、2025年12月対応）、未解決も2件残る（[#1065](https://github.com/neutralinojs/neutralinojs/issues/1065)、[#985](https://github.com/neutralinojs/neutralinojs/issues/985)）。GuiEcadが経験した「ネイティブ⇔ビュー境界でのフォーカス取りこぼし」と同型のリスクが構造的に残る。2026年3月に公式リポジトリへの不正コミット注入（サプライチェーン侵害）が発生した経緯もあり、ガバナンス面の留意が必要。

### Flutter desktop
FocusNode/FocusScope/Shortcuts/Actionsという明確な階層構造を持ち設計思想は宣言的だが、**Windowsデスクトップで最小化→復帰後にキーボード入力が完全に無反応になる未解決バグ**（[#151457](https://github.com/flutter/flutter/issues/151457)、Open、P2、Windowsチームtriage済みだが修正未着手）が最も直接的なリスク。さらにTextFieldにフォーカスがあってもグローバルShortcutsが先にキーを奪う設計上の欠陥（[#107037](https://github.com/flutter/flutter/issues/107037)）もある。C#資産は`dart:ffi`がC ABIのみのため実質再利用不可。

### Compose Multiplatform
`FocusRequester`/`onPreviewKeyEvent`/`onKeyEvent`というキーイベント伝播順は仕様として明文化されており、6候補中もっともAPI設計が宣言的・決定的に見える。しかしデスクトップ固有の非決定性issueが複数存在：macOSで`requestFocus()`が**約50%の確率でしか効かない**（[#4803](https://github.com/JetBrains/compose-multiplatform/issues/4803)、Swing/AWTイベントループとの同期問題）、フォーカス要素消失時にルートへ強制遷移する（[#2741](https://github.com/JetBrains/compose-multiplatform/issues/2741)）。描画性能は複雑Path図形（電気記号相当）で1万要素から不安定化、10万要素で2FPSという劣化幅の大きさが実測ベンチマークで判明しており、ラダー図CADとしては要注意。PDF出力もSkiko統合が未マージで停滞中。

### JavaFX
`Canvas`/`GraphicsContext`によるimmediate-mode描画はGuiEcadのWin2D `CanvasControl`設計とほぼ同型で、描画・ヒットテスト設計思想がそのまま転用しやすい（ヒットテストは自前実装必須な点も同じ）。PDFBox/OpenPDFはGuiEcadの`IRenderer`抽象と同じ設計思想で追加できる。一方フォーカス管理は、公開API整備を求めるRFE「JDK-8090456」が2013年提出→2024年にようやくクローズという約11年がかりの案件で、現在も「JDK-8343956 Focus delegation API」（非フォーカス可能なコンテナへのフォーカス委譲の標準APIが不在）が議論継続中。致命的な単一バグは見当たらないが「フォーカス管理APIが成熟に時間がかかる」フレームワークという評価。

## 忍者の総合所見

- **フォーカス決定性（最重要軸）で「無条件に安全」と言える候補は6つ中1つもなかった**。それぞれ異なる形でリスクを抱える：
  - Flutter desktop / Compose Multiplatformは**Windows/macOSで具体的かつ未解決の重大バグ**（#151457, #4803）を抱え、GuiEcadのIssue #6179と同種の「実装しても土台から崩れる」リスクが高い。
  - Tauriは単一の致命傷はないが「多数の小さな地雷原」でプラットフォームごとの個別検証コストが高い。
  - Electron・JavaFX・Neutralinoは、それぞれ程度の差はあるが致命的な単一バグは確認されず、相対的にはリスクが低いグループと言える（Neutralinoはガバナンスリスクが別軸で存在）。
- **描画性能とPDF出力の両面で、JavaFXがGuiEcadの既存設計（Win2D Canvas + IRenderer抽象）ともっとも近く、移植の見通しが立てやすい**。ただしJVM系のためC#資産は再利用できず、コミュニティ規模もWinUI3系より小さい。
- **C#資産の再利用は、このファミリー全体で実質不可**（Electronのedge-jsのみ技術的には可能だが、Web技術統一というElectron採用の動機と矛盾するため現実的でない）。この点は隠密が調査中の.NET系ファミリーとの比較で大きな差別化要因になるはずで、家老の統合判断で重視されたい。
- 6候補いずれも、採用するなら**キーボードファースト操作の小規模PoC（フォーカス制御・大量記号描画・PDF出力の3点）を実装前に行うこと**を強く推奨する（Compose Multiplatform調査でも同様の結論、他候補にも同じ論理が当てはまる）。GuiEcadの教訓（実装後の不具合対応で9ラウンドを要した）を踏まえると、選定段階での実地検証コストは惜しむべきではない。
