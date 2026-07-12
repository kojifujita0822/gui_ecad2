# P-060 グリッド用紙はみ出し 調査（隠密）

- 依頼元: 家老（2026-07-12、殿目視でP-060実在確定後の焦点更新2回を経て）
- 調査のみ・実装せず。出典は全てファイルパス:行。事実と推測は明示区分。

## (1) 殿のGuiEcad体験と「収める仕組みが無い」事実の整合

**結論：整合する。GuiEcadには「EnableBorder（枠あり/なし）」を画面上どこからでも視認性ゼロで
切替できるトグルがあり、枠なし時はページサイズがグリッド幅ぶん自動拡大されクリップが起きない。
殿は恐らく枠なし状態でGuiEcadを使用していた。**

- `DocumentSettings.EnableBorder`既定値はGuiEcadも`true`（`GuiEcad.Core/Model/Document.cs:48`、
  ecad2と同じ）。
- ただしGuiEcadには**「図面(D)→ドキュメント情報...」ダイアログ内に`ToggleSwitch`
  「図面枠を描画（PDF出力時）」が存在**（`GuiEcad.App/MainPage.Dialogs.cs:72-79,143-144`）。
  トップレベルメニューから容易に到達可能、タイトルブロック情報を入力する自然な導線上にある。
- **画面上の編集キャンバスはEnableBorderの値に関わらず常に全グリッドを無条件描画**
  （`GuiEcad.App/MainPage.Drawing.cs:57`、`pageRowStart/pageRowCount`省略＝全行描画）。
  EnableBorder=trueの唯一の視覚効果は点線のページ境界ガイド線オーバーレイのみ
  （同61-62行目）——**内容が隠れることは画面上一切無い**。
- PDF出力・プレビューとも`document.Settings.EnableBorder`の現在値をそのまま使う
  （`MainPage.Menu.cs:222,249`）、上書き・強制なし。
- 結論として、GuiEcadユーザーは画面上どちらのモードか判別できず、一度ドキュメント情報
  ダイアログでトグルを操作すれば（枠を消したい等どんな理由であれ）、以後の全PDF出力が
  無自覚に枠なし＝可変ページサイズへ切り替わる。行コメントを含む全内容が印刷に反映された
  殿の体験は、この状態と完全に整合する。

## (2) ecad2側現状：PageSize(enableBorder=false)は既にGuiEcadと同型実装済み

- `src/Ecad2.Core/Rendering/DiagramRenderer.cs:120-143`（`PageSize`メソッド）：
  `enableBorder=true`時は`(PageW, PageH)`固定、`enableBorder=false`時は
  `w = RightBusX(columns) + rightExtra`でグリッド幅に応じた可変サイズを返す
  ロジックが**既に実装済み**（T-060時にGuiEcadから移植確認済み、コメント「行コメント
  （右母線の右側）が長いとページ右にはみ出すため」も原文一致）。
- **しかしT-060裁定（`docs/todo.md`T-060節）で「枠あり/枠なし切替＝切替UIなし、常に枠あり」
  と確定したため、ecad2には`EnableBorder`を切り替える手段が一切無い**（`DocumentInfoDialog`
  にもトグル欄なし、T-065実装内容で確認済み）。既存の可変ページロジックは死んでいる
  （呼ばれる経路が存在しない）。これが殿目視のクリップ発生の直接原因。
- 【訂正・重要】侍の当初前提「GridSpec.Columns=40固定」は不正確。実際のシート作成コードは
  明示的に`Columns=20`を指定している：`NewDocument`
  （`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1660`）・`SheetNavigationViewModel`の
  シート追加（同ファイル107行目）とも`Grid = new GridSpec { Rows = 10, Columns = 20 }`。
  `RightBusX(20)≈204.5mm`はA4縦(210mm)にほぼ収まる計算になり、これはGuiEcad側のUI列数上限
  （後述）と一致する値。**列数変更UI自体がecad2に存在しない**（`SheetSettingsDialog`は行数
  (Rows)のみ、`src/Ecad2.App/Views/SheetSettingsDialog.xaml.cs`で確認済み）ため、通常操作では
  20列を超えない。P-060の実発生シナリオ（20列内で本当に発生しているか、行コメント文字数由来か、
  主回路シートのFreeLine由来か等）は、忍者の実測結果・隠密2のGuiEcad参考仕様書と合わせて
  家老突合が必要（本調査はコード事実の確認までがスコープ）。
- 別件で判明：GuiEcad原本のApp層には`MaxColumns=20`のUI制約が存在し
  （`GuiEcad.App/MainPage.xaml.cs:730,734`、シート設定ダイアログも列数2〜20にclamp）、
  Core層のCore層モデル既定値40には実質到達しない設計だった（ecad2はこの列数UI自体が
  無いため、この予防策は「たまたまシート作成時に20を明示指定している」という限定的な形でしか
  引き継がれていない）。

## (3) 対処候補の実装コスト概算・利害（ecad2現行アーキテクチャ上）

| 候補 | 実装コスト | 概要 | 利害 |
|---|---|---|---|
| (a) 縮小フィット（枠内へスケール） | 中 | `PdfRenderer.PushTransform`に既に`scale`引数あり（`src/Ecad2.Pdf/PdfRenderSurface.cs:165`、既定1.0で未使用）。`DiagramRenderer.Render`内2箇所のPushTransform呼び出し（869,926行目）へスケール値を渡す変更＋スケール計算ロジック新設。画面用`WpfRenderer`側の対応要否は要確認 | 用紙サイズは保証されるが、列数が多いほど縮小率が上がり可読性低下。フォントサイズ下限の検討要 |
| (b) 列方向ページ分割 | 大 | `ColsPerPage`相当の新規概念、2次元ページ走査、`PdfPageLayout`構造変更、CircuitNumberer/CrossReferenceBuilderとの整合（行番号の列分割またぎ扱い）、プレビューUIのページ数・ナビ複雑化。GuiEcadに前例なく設計はゼロから | 可読性最大（縮小なし）だが実装コスト最大。1回路が複数ページにまたがる読みづらさとのトレードオフ |
| (c) 用紙横置き対応 | 中 | `PageW/PageH`の「縦固定」という既存設計方針（コメント明記）の変更、用紙選択肢UIの追加。A3横(420mm)なら現行20列(204.5mm)は当然、40列(384.5mm)も収まるが、A4横(297mm)は40列に届かない | 対症療法（列数を増やせば同じ問題が再発）。実装コストは中程度 |
| (d) 枠なし時の可変ページサイズ許容（T-060裁定一部見直し） | **小〜中（最小候補）** | レンダリングロジックは(2)の通り**既に実装済み**。必要な追加は主に(i)`EnableBorder`切替UI新設（`DocumentInfoDialog`へのトグル追加が自然、GuiEcad同型）(ii)T-060裁定の一部見直し裁定 | 実装コスト最小。ただし印刷用紙サイズが不定になる（プリンタ側の自動縮小/拡大に依存）実務上の懸念があり、「常に枠あり」裁定の意義（用紙サイズ保証）と正面から相反する |
| (e) 列数上限の導入（GuiEcad App層方式の踏襲） | 小 | `GridSpec`に`MinColumns`/`MaxColumns`定数追加（`MinRows`/`MaxRows`と同型パターン）、`SheetSettingsDialog`への列数入力欄新設＋clamp。実質「列数変更UIが無い」現状を「列数変更UI＋上限」に変える形 | 機能制限（40列など多列を使いたいニーズには応えられない）。ただし実装は最も定型的で低リスク |

**総評（推測含む、殿裁定材料として）**：(d)は実装コストが最小である一方、T-060裁定の理念
（用紙サイズの保証）と衝突する。(a)は用紙保証と実装コストのバランスが良いが可読性リスクを
伴う。(b)は理想的だが最も重い。(e)は現状の「列数固定20」という実態を追認する形で、
P-060の直接原因（もし本当に20列超過や列数変更経路が別にあるなら）を塞ぐが、そもそも
P-060の実発生原因（20列以内で起きているか否か）の特定が先決。

## 出典一覧

- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（PageSize・PushTransform呼び出し、Read）
- `src/Ecad2.Core/Rendering/GridGeometry.cs`（X座標計算式、Read）
- `src/Ecad2.Core/Model/Document.cs:48`（EnableBorder既定値、grep）
- `src/Ecad2.Core/Model/Sheet.cs:39`（GridSpec.Columns既定値40、grep）
- `src/Ecad2.Pdf/PdfRenderSurface.cs`（PushTransformのscale引数、Read全文）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1650-1663`（NewDocument、Read）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs:98-112`（シート追加、Read）
- `src/Ecad2.App/Views/SheetSettingsDialog.xaml.cs`（列数変更UI不在の確認、Read全文）
- `docs/todo.md`T-060節（枠切替裁定、Read）
- GuiEcad原本（サブエージェント2件経由で調査、査読済み）：
  `GuiEcad.Core/Model/Document.cs:48`（EnableBorder既定値）、
  `GuiEcad.App/MainPage.Dialogs.cs:72-79,143-144`（EnableBorderトグルUI）、
  `GuiEcad.App/MainPage.Menu.cs:222,249-272`（PDF出力のEnableBorder参照）、
  `GuiEcad.App/MainPage.Drawing.cs:45-62`（画面描画は常に全行無条件、EnableBorderは
  視覚ガイドのみ）、`GuiEcad.App/MainPage.xaml.cs:730,734`（MaxColumns=20 UI制約）、
  `GuiEcad.Core/Rendering/DiagramRenderer.cs`（PageSize・RightBusX同型計算式）

## 不明点

- P-060の実発生シナリオ（実際のecad2運用で列数20超過があったのか、行コメント文字数由来か、
  主回路シートのFreeLine由来か等）は本調査のスコープ外（忍者実測・隠密2のGuiEcad参考仕様書と
  家老突合が必要）。

## 派生提案の有無

なし（本調査は家老采配の緊急調査スコープ内で完結）。
