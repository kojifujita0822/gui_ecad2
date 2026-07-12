# T-080 行コメント機能 往復1周目 再レビュー報告(隠密)

- 対象コミット: `d7ed029`(core: A〜D)・`c4dd2b1`(app: E〜H+I)。差分範囲 `git diff 01b89a5..c4dd2b1`。
- 手法: 手動静的読解(数式再導出・境界値計算・全呼び出し元grep)＋`code-review`スキル(Agent tool、
  high、8角度finder→1票verify)。finder8体・verify7体、計15エージェント投入。
- 参照: `docs/ecad2-t080-rung-comment-review-onmitsu.md`(前回レビュー、A〜I指摘の原本)

## 検証観点(1): A〜I各件の判定

全9件、指摘の趣旨に合致する形で修正されていることを確認した(全件OK)。個別の根拠は以下。

| 指摘 | 判定 | 根拠 |
|---|---|---|
| A(判定基準を図面枠へ) | OK | `CalcPageScale`が`PageW-BorderMarginMm`基準に変更されたことを確認。数式`scale=(PageW-2*BorderMarginMm)/(neededWidth-BorderMarginMm)`を独立に代数的に再導出し一致を確認 |
| B(縮小変換をtranslate付きへ) | OK | `x'=BorderMarginMm*(1-scale)+scale*x`で固定点(x=BorderMarginMm)が不動点になることを計算で確認。新規`DiagramRendererPageTransformTests`3件で構造検証(PushTransform引数・PopTransform後の絶対座標描画)もGREEN |
| C(縮小率をページ単位へ) | OK | `PdfPageLayout.Build`が`CalcPageScale(sheet, p*RowsPerPage, RowsPerPage)`をページ毎に呼ぶ形へ変更されたことを確認。プレビュー(`PdfPreviewDialog`)・実出力(`PdfExporter`)とも`PdfPage.Scale`経由の一元化を維持しており分岐なし |
| D(主回路シートのmm実座標考慮) | OK | `MainCircuitContentMaxX`追加によりFreeLines/ConnectionDots/Framesの広がりを縮小判定へ反映することを確認、新規テスト3件GREEN。ただし関連して新規PLAUSIBLE指摘あり(下記) |
| E(編集ボックスY位置補正) | OK | `YRow(row)-RungCommentFontSizeMm`への変更で、描画側VAlign.Bottom基準との整合方向は正しい。ただし実際の残差ありPLAUSIBLE(下記、実機確認要) |
| F(IsMainContentEnabled連動) | OK | 配置バーと同型の「ViewModel単一の真実源」パターンを確認、VMテスト5件GREEN |
| G(HitTestRungCommentRowへMainCircuit判定) | OK | `if (sheet.MainCircuit) return null;`追加を確認。ただし関連して新規PLAUSIBLE指摘あり(下記) |
| H(CloseRungCommentEditorの条件付きFocusCanvas) | OK | 全呼び出し元(Commit/Cancel/Enter-Tab/LostKeyboardFocus)の`restoreFocus`引数をgrepで確認、意図通り(true/true/true/false) |
| I(F2キーボード経路追加) | OK | 境界条件(`!MainCircuit && row>=0 && row<TotalRows`)がダブルクリック経路(`HitTestRungCommentRow`)と一致することを確認。ただし関連して新規CONFIRMED指摘あり(下記) |

## 検証観点(2): code-reviewスキルによる新規欠陥の混入有無

8角度finder(正しさ3・再利用/簡潔化/効率/高度/CLAUDE.md整合)を投入し、重複排除の上7候補を1票検証した。
**ブロッキング級の新規バグは無し**。CONFIRMED2件・PLAUSIBLE4件・REFUTED1件、いずれも往復2周目を要する
重大度ではないが、家老の裁量判断のため全件記載する。

### CONFIRMED(4件、うち2件は保守性、2件は軽微)

**1. MainCircuitContentMaxXがMainCircuitVirtualRows等と3箇所で重複(前回指摘K・Jと同根)**
- 箇所: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:76-88`(既存Y軸版)・`96-117`(新規X軸版)・
  `607-611`(`DrawFrames`、既存)
- Frameの実座標フォールバック式`f.VisualYMm ?? (_geo.YRow(f.TopLeft.Row) - Cell*0.4)`等が3箇所で
  一字一句(または準一致で)重複。前回レビュー項目K(`RequiredContentWidth`系の重複)・項目J
  (`PositionRungCommentEditor`/`PositionPlacementBar`重複)と同型のリスクで、項目Jの根拠として
  引用された**T-033実座標系不一致バグ**(`docs/archive/ecad2-t033-review-onmitsu-3.md`)と
  同種の実害が過去に実際に起きている。将来Frameの実座標解決ロジックが変わった際、3箇所のうち
  一部だけ修正されて食い違うリスクは具体的。

**2. F2キー経路とHitTestRungCommentRowの適格条件(MainCircuit除外・行範囲)が重複実装**
- 箇所: `src/Ecad2.App/Views/LadderCanvas.cs:214,221`(`HitTestRungCommentRow`)、
  `src/Ecad2.App/MainWindow.xaml.cs:967-968`(F2ケース)
- 両経路とも同一ロジックを手書きで複製しており、共有ヘルパー(`CanEditRungComment(sheet, row)`等)への
  抽出はなし。`MainWindow.xaml.cs`のコメント自身が「対象条件はダブルクリック経路と揃える」と明記して
  おり、開発者が手動同期の必要性を自覚している=重複の証左。将来条件追加時に片方のみ修正し見落とす
  典型的な保守事故のリスク。この重複自体を検証する回帰テストも無い。

**3. `InverseBool`(InverseBooleanConverter)リソースがF修正で参照ゼロの未使用コードに**
- 箇所: `src/Ecad2.App/MainWindow.xaml:17`
- `MainContentArea.IsEnabled`が`IsPlacementBarVisible+InverseBool`から`IsMainContentEnabled`
  (計算プロパティ直結)へ置き換わったことで、`InverseBool`キーの参照が同ファイル含め全体で0件に
  なった。実害なし、ボーイスカウト・ルール上の軽微な片付け漏れ。

**4. テストダブル`RecordingRenderer`が2ファイルに独立して重複命名**
- 箇所: `tests/Ecad2.Core.Tests/DiagramRendererPageTransformTests.cs`(新規、ネストprivateクラス)、
  `tests/Ecad2.Core.Tests/DiagramRendererLabelTests.cs`(既存、トップレベルinternalクラス)
- 名前衝突はしない(スコープが異なる)が、`IRenderer`の全14メンバーを2箇所で独立にno-op実装しており、
  将来インタフェース変更時に両方の追従が必要。実害は軽微(テストコードのみ)。

### PLAUSIBLE(4件、いずれも現状は発現条件が限定的・実害未達)

**5. RequiredContentWidthForScaleが主回路シートでも無意味なRightBusX(Grid.Columns)を必要幅の
   下限として保持し続ける(指摘Dの隙間)**
- 箇所: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:192-196`
- 主回路シートは`Render()`が右母線自体を描画しない(`if (!sheet.MainCircuit) DrawRails(...)`)にも
  関わらず、`width = RightBusX(sheet.Grid.Columns)`という「描画されない座標」が`Math.Max`の基礎値
  として残る。既定シート(Columns=20)ではRightBusX=204.5mmで縮小閾値205mmに対し**僅か0.5mm差**。
  現状はGrid.Columnsを変更するUI機能が存在しない(コード内コメントで開発陣も欠如を認識済み)ため
  今すぐの実害はないが、前回レビューのどの項目にも含まれていない新規の論点であり、Dの修正としては
  片手落ちの可能性がある。将来列数編集機能が入れば実害化しうる。

**6. CalcPageScaleのrowEnd(pageRowStart+pageRowCount)がEffectiveTotalRowsへクランプされない
   (Renderは`Math.Min(totalRows, ...)`でクランプ済み、非対称)**
- 箇所: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:227`(CalcPageScale) vs `267-270`(Render)
- 数値例(40行シート・RowsPerPage=28)で最終ページのrowEndが56となりEffectiveTotalRows(40)を超える
  ことを確認。ただし`RungComment.Row`は新規作成時のヒットテスト・行挿入削除時のシフト
  (`RowOps.cs`)・行削除ガード(`IsRowOccupied`)の3重ガードで常に有効範囲内に収まるため、現状のUI
  操作では到達不能。手動改竄ファイル等の非現実的経路のみ理論上リスクが残る。

**7. RungCommentAnchorDipのY位置補正がフォント行送り係数(概ね1.2倍)を考慮せず、実測0.5〜0.9mm相当
   の残差が残る**
- 箇所: `src/Ecad2.App/Views/LadderCanvas.cs:235`
- `WpfRenderer.DrawText`のVAlign.Bottomは`position.Y*K - ft.Height`(FormattedTextの実測高さ)を
  使う一方、編集ボックス側は`RungCommentFontSizeMm`(3.0mm、emSize相当)を直接差し引くのみで行送り
  係数を含まない。編集ボックス自体の枠線・パディング・フォント差という既存の近似要因と同程度か
  それ以下の残差であり、視覚的に問題化するかは実機でしか確定できない(家老采配どおりView層は
  忍者マター)。

**8. 「主回路シートは行コメント対象外」というポリシーがモデル層に不在でUI入口2箇所
   (ヒットテスト・F2)にのみ分散**
- 箇所: `src/Ecad2.App/Views/LadderCanvas.cs:214`、`src/Ecad2.Core/Rendering/DiagramRenderer.cs`
  の`DrawRungComments`/`RequiredContentWidthForScale`(いずれも`MainCircuit`を見ずRungCommentsを
  そのまま扱う)
- 通常のUI操作(シート作成後にMainCircuitフラグを変更する機能は無い)では到達しないが、
  `GcadSerializer`はスキーマ検証なしの素のJSON直列化のため、手編集ファイル・旧/将来バージョンの
  ファイルで`MainCircuit=true`かつ`RungComments`に値が入った状態が読み込まれると、描画はする
  のに編集/削除の入口が両方塞がれた矛盾状態になりうる。ファイル読み込み経路に限定される。

### REFUTED(1件)

**F2ケースの`e.Handled=true`無条件実行が「既存の設計方針」に反するという指摘 → REFUTED**
- 「内部条件不成立時はHandledにしない」という文言はKey.Enterケース内の1箇所のみで、これは
  「同キーが複数caseに分岐する際のswitch文の技術的制約」の注記であり汎用の設計哲学ではないと
  判明。実際にはKey.F10・矢印キー等、ファイル内の支配的な慣行はF2と同じ無条件Handled=trueであり、
  F2はこれに整合している。キャンバスフォーカス時にF2へ反応する他のハンドラも存在せず実害なし。

## 検証観点(3): テスト妥当性

- RED先行証明(侍申告のstash実測)は妥当。現在の実装は全89件(Core.Tests)・439件(App.Tests)GREEN
  (`dotnet build`→`dotnet test --no-build`で実測済み)。
- テスト名・アサーションの整合性: 確認した範囲で不整合なし。「必ず通るテスト」も見当たらず、
  境界値(204.5mm/205mm/272.5mm等)がリテラルでピン留めされており仕様値と実装値の混同を避ける
  設計になっている(`PrintableRightMm`定数へのコメントで「実装に合わせたテストにしない」と明記)。
- カバレッジ: E/G/H/IはView層依存のためVMテスト(F関連の`IsMainContentEnabled`連動5件)のみに
  絞られており、View層の実挙動確認は忍者へ委譲する設計(家老采配の範囲内で妥当)。ただし上記
  CONFIRMED2「F2/マウス経路の適格条件重複」に対する回帰テスト(両経路の整合性を直接検証する
  テスト)は無い。

## 検証観点(4): 既存PageScaleテスト2件の期待値補正の妥当性

妥当と判断する。「1文字コメントで縮小しない」テストを、A修正後の新しい境界(図面枠205mm)に対して
`Columns=18`+1文字(191.8mm、収まる)と`Columns=20`(既定)+1文字(209.8mm、収まらない→縮小する)の
2ケースへ分離補正しており、仕様の骨抜き(「必ず通るテスト」化)には当たらない。数値はいずれも
手計算でリテラル記載されテストの意図が明確。

## 総括

A〜I全9件は指摘の趣旨どおり修正され、ビルド・全テストGREEN。ブロッキング級の新規バグは無い。
CONFIRMED2件(重複コード、MainCircuitContentMaxX系・F2/マウス適格条件系)は保守性観点で往復2周目
または次の増分での対応を推奨、CONFIRMED2件(InverseBool・RecordingRenderer)は軽微で対応任意。
PLAUSIBLE4件のうち5(RightBusXベースライン)は前回レビューに無かった新規論点でDの完全性に関わる
ため殿・家老の判断を仰ぎたい、6(rowEnd未クランプ)は3重ガードで現状無害、7(フォント行送り)は
忍者実機確認へ、8(ポリシー分散)はファイル読み込み経路限定で優先度低。REFUTED1件は指摘却下。
