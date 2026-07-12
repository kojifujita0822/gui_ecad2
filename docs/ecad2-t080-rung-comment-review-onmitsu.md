# T-080 行コメント機能 静的レビュー報告（隠密）

- 対象コミット: `3b9080f`（DoD(6)除く）／`27d3ba9`（DoD(6)、縮小フィット）。両者の間には無関係な
  docsコミット(`db366fd`・`af75815`)が挟まるため、各コミットを個別にdiff取得しレビュー範囲とした。
- 手法: `code-review`スキル（Agent tool、xhigh、10角度finder→1-vote verify）。finder10体・
  verify15体、計25エージェントを投入。
- 参照: `docs/ecad2-t080-rung-comment-design-onmitsu.md`（着手前設計調査）、
  `docs/todo.md` T-080節（殿裁定4点）、`docs/ecad2-p060-grid-overflow-investigation-onmitsu.md`
  （DoD(6)縮小フィット採用に至った経緯）

## 殿裁定4点との整合性（検証観点1）

1. 記入トリガー＝ダブルクリック → **実装済み確認**（`MainWindow.xaml.cs:582`、`ClickCount==2`）
2. 空文字列＝削除扱い → **実装済み確認**（`MainWindowViewModel.cs:1360-1381` `SetRungComment`、
   `trimmed.Length==0`で`sheet.RungComments.Remove`。単体テスト`SetRungComment_EmptyText_RemovesEntry`
   等6件で確認済み）
3. 印刷はみ出し対策＝20文字制限＋スペース事前確保 → 20文字制限は実装済み確認
   （`MainWindow.xaml:676` `MaxLength=20`）。「スペース事前確保」は**P-060発覚により殿裁定で
   「縮小フィット」方式へ変更済み**（`docs/todo.md`同節）。その縮小フィット自体に重大な欠陥が
   複数見つかった（下記A〜D参照）。
4. Undo＝対象外を追認 → **実装済み確認**（Undo関連コードの新規追加なし、grep0件）

## 空文字列削除の経路網羅性（検証観点5）

Enter/Tab確定・フォーカスロストの両経路とも同一の`CommitRungCommentEditor→SetRungComment`を通るため、
いずれの経路でも正しく削除される。Escは`CancelRungCommentEditor→CloseRungCommentEditor`のみで
`SetRungComment`を一切呼ばない（意図的な「取消＝モデル不変更」であり削除シナリオではない）。
**3経路とも設計通り、問題なし。**

## 結論（仕分け）

### 要修正・重大（8件、全てCONFIRMED）

**A. CalcPageScaleが印刷枠（用紙端5mm内側）を考慮せず用紙物理端(PageW)基準で判定するため、
行コメントは縮小の有無に関わらず印刷枠の外側/境界に描画される**
- 箇所: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:173`（`CalcPageScale`）、
  `DiagramRenderer.cs:1099-1104`（`DrawBorder`、枠は用紙端から5mm内側=205mm@A4に描画）
- verify: CONFIRMED、数値で実証。既定シート（Grid.Columns=20、A4）で
  `RightBusX(20)=204.5mm`、行コメント描画開始X=`206.5mm`。`DrawBorder`の枠右端は`205mm`。
  1文字のコメントでも`neededWidth=209.8mm<=PageW(210mm)`となり`CalcPageScale`は
  縮小不要(1.0)と誤判定、コメントは枠の1.5mm外側に描かれる。既存テスト
  （`DiagramRendererPageScaleTests`）はこの「縮小しない」挙動自体を積極的にGREENで固定しており、
  バグを再現する入力がまさにテストで保証された状態そのもの。縮小が発動するケースでも、
  スケール後の内容右端は定義上「用紙物理端(PageW)ちょうど」に一致する式であり、印刷枠(205mm)へは
  構造的に戻らない。

**B. PushTransform(0,0,pageScale)が原点基準の一様縮小のため、縮小時に左上余白・行間隔まで
比例して縮み、絶対座標のままの表題欄・外枠との間で余白バランスが崩れる**
- 箇所: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:225`（`PushTransform`呼び出し）
- verify: CONFIRMED。`GridGeometry.cs:18-20`よりMarginMm・行間隔とも原点基準の絶対mm値で
  スケール対象内。一方`DrawTitleAndRevisionBottomRight`・`DrawBorder`はPushTransformブロックの
  外側（`PopTransform`後）で絶対座標描画のためスケール対象外。数値例（pageScale≈0.71）では
  左マージン相当が実描画で20mm→約14mmまで縮小、縮小された図面が用紙左上に偏り、右母線は
  縮小後もなお枠外（用紙物理端側）に来る計算となる。

**C. CalcPageScaleがシート単位で1回だけ計算され、複数物理ページに分割されるシートでは
行コメントが無いページにも同一の縮小率が一律適用される**
- 箇所: `src/Ecad2.Pdf/PdfPageLayout.cs:41`（`Build`内`CalcPageScale`呼び出し、以降のforループで
  全ページへ使い回し）
- verify: CONFIRMED。`RequiredContentWidthForScale`（`DiagramRenderer.cs:141-149`）は
  `sheet.RungComments`を行範囲フィルタなしで全件走査する一方、実際の描画（`DrawRungComments`）は
  ページごとに行範囲でフィルタする設計になっており、縮小率計算だけこの一貫性が欠落している。
  40行シート・RowsPerPage=28（2ページ分割）・1行目にのみ長いコメントがある例で、
  コメントの無い2ページ目も無関係に縮小される。

**D. RequiredContentWidthForScaleが主回路(MainCircuit)シートのFreeLines/Frames等mm実座標
コンテンツを一切考慮しないため、主回路シートでは縮小フィットが実質機能しない**
- 箇所: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:141-149`
- verify: CONFIRMED。縦方向は`MainCircuitVirtualRows`（同73-88行）でグリッド行範囲を超える
  広がりを既に考慮済みなのに対し、横方向の対応する仕組みが存在しない非対称。主回路シートは
  `DrawRails`（右母線）自体が描画されない（234行目、`!sheet.MainCircuit`時のみ呼び出し）ため
  グリッド列数による物理的な右端の縛りが元々無く、`FreeLine.X2Mm`等が`RightBusX`を大きく
  超えて配置されていても`CalcPageScale`は縮小不要と誤判定し、実際には用紙外へはみ出す。
  DoD(6)の目的（はみ出し防止）が主回路シートには実質効かない。

**E. RungCommentAnchorDip（編集ボックスの位置決め基準）のY座標が、実際の文字描画位置と
垂直方向で逆基準になっており、既存コメントを編集しようとすると編集ボックスが文字位置から
ずれて表示される**
- 箇所: `src/Ecad2.App/Views/LadderCanvas.cs:221-227`（`RungCommentAnchorDip`、行中心Y返却）
- verify: CONFIRMED。`DrawRungComments`（`DiagramRenderer.cs:1019,1022`）は
  `TextRole.DeviceName`の`VAlign.Bottom`を継承（`with`式はHAlign/FontSizeMmのみ上書き）したまま
  描画するため、文字は行中心から**上方向**に描かれる。一方編集ボックス
  （`MainWindow.xaml:673-674`、`VerticalAlignment="Top"`）は行中心から**下方向**に展開する。
  既存コメントをダブルクリックすると、編集ボックスは実際の文字位置よりFontSizeMm=3.0相当
  （概ね4mm弱＝1行分弱）下にずれて表示される。

**F. 行コメント編集中もMainContentAreaのIsEnabledバインドが編集状態を反映せず、
マウスでツールバー・メニュー操作が素通りする**
- 箇所: `src/Ecad2.App/MainWindow.xaml:99-100`（`IsEnabled="{Binding IsPlacementBarVisible,...}"`）
- verify: CONFIRMED。ElementPlacementBar表示中はこのバインドでマウス操作を含め他UIが完全に
  無効化される設計（殿裁定）だが、`_rungCommentEditingRow`はcode-behind専用フィールドで
  ViewModel側に対応プロパティが無く、バインド対象にできない。`Window_PreviewKeyDown`
  （733行目）のガードはキーボード経路のみで、マウス側（`Window_PreviewMouseLeftButtonDown`等）に
  対応するガードが無い。編集中にメニュー「新規」等をマウスでクリックすると、編集状態を無視して
  即実行される。

**G. HitTestRungCommentRowがsheet.MainCircuitを判定せず、主回路シート（右母線非表示）でも
右母線相当の座標帯を無条件にヒット扱いする**
- 箇所: `src/Ecad2.App/Views/LadderCanvas.cs:208-217`（`HitTestRungCommentRow`）
- verify: CONFIRMED。`RightBusX`は`MainCircuit`非依存の機械的な列数計算のため、右母線が
  視覚的に描画されない主回路シートでもこの座標より右をダブルクリックすると、ツールモード・
  他のヒットテスト（`HitTestConnector`等）より最優先で行コメントエディタが開いてしまう
  （`MainWindow.xaml.cs:580-587`のコメント自身が「ツールモードを問わず優先判定する」と明記）。

**H. CloseRungCommentEditorが無条件にFocusCanvas()を呼ぶため、編集後に他コントロールへ
マウスでフォーカス移動しても、直後にキャンバスへフォーカスが強制的に奪い返される**
- 箇所: `src/Ecad2.App/MainWindow.xaml.cs:1692-1697`（`CloseRungCommentEditor`）
- verify: CONFIRMED。同ファイル内のEscapeハンドラの既存コメント（796-808行付近）が
  「`FocusCanvas()`は`Keyboard.Focus()`経由で直前のフォーカス保持者に`LostKeyboardFocus`を
  同期的に再発火させる」ことを開発者自身が実証済みと記録しており、この再入機構により
  「機器表セルをクリック→フォーカスが再びキャンバスへ奪い返される」という経路が成立する。
  比較対象の`DeviceNameBox_LostKeyboardFocus`はこの種の`FocusCanvas()`呼び出しを行わず、
  パターンとして非対称。

### 要判断・未文書化スコープ差分（1件、CONFIRMED＝要殿確認の意）

**I. 行コメントエディタの起動経路がダブルクリック一本のみで、キーボード等価経路が存在しない
（CLAUDE.md「キーボードファースト」原則との整合、要殿確認）**
- 箇所: `src/Ecad2.App/MainWindow.xaml.cs:582-587`
- verify: CONFIRMED（要殿確認の意）。`C:/ECAD2/CLAUDE.md`「プロジェクト概要」節
  『**キーボードファースト**（マウス操作に頼らない操作性）を主眼に据える。』に対し、
  `ContextMenu`・`InputBinding`等のキーボード等価経路が皆無（grep0件）。殿裁定4点の(1)は
  「シングルクリックかダブルクリックか」というマウス内の分岐のみを裁定したものであり、
  「キーボードでも起動可能にすべきか」という論点自体が着手前調査（DoD(4)）で一度も
  殿へ提示されていない。同ファイル内の他の類似入力機能（要素配置・縦コネクタ確定等）は
  軒並みEnterキー等のキーボード経路を備えており、既存パターンから外れる。実装済みの新機能に
  対する追加のUI/UX判断であり、意図的な承認ではなく単純な見落としの可能性が高い。

### 経過観察（軽微、4件）

**J. PositionRungCommentEditorがPositionPlacementBar（T-033で座標系不一致バグを実際に
踏んだ実績あり）をほぼ丸ごとコピーしており、共通ヘルパーへ抽出されていない**
- 箇所: `src/Ecad2.App/MainWindow.xaml.cs:1662-1678`（`PositionRungCommentEditor`）、
  同1620-1641行（`PositionPlacementBar`）
- verify: CONFIRMED（保守性の指摘）。TranslatePoint変換・クランプ計算・Margin再設定の
  12行相当がほぼ一字一句一致。将来同種の座標系修正がPositionPlacementBar側にのみ適用され
  RungComment側が取り残されるリスクが構造的に残る。

**K. RequiredContentWidth/RequiredContentWidthForScaleが同一のmaxRungLen算出ロジック・
マジックナンバー(2.0mmオフセット・3.3mm/文字)を独立に4箇所へ重複保持している**
- 箇所: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:125-135,141-149`
- verify: CONFIRMED。将来この文字幅換算式を調整する際、片方だけ更新すると画面表示用の
  ページ幅計算とPDF縮小判定の基準が食い違う恐れがある。

**L. RungCommentBox.MaxLength=20はUTF-16コード単位でありサロゲートペア境界を意識しない
（クラッシュはしないが、サイレントなデータ破損の可能性）**
- 箇所: `src/Ecad2.App/MainWindow.xaml:676`
- verify: 元候補は「保存時JsonException」だったが実測でREFUTED（`GcadSerializer`実行で確認、
  下記REFUTED節参照）。ただし実測により**別の実害**が判明: サロゲートペア文字を含む
  コメントがMaxLengthで途中切断されると、System.Text.Jsonのシリアライズ時に不正な片割れ
  サロゲートが**例外なく黙ってU+FFFD（文字化け記号）へ置換**される。クラッシュではなく
  ユーザーが気づかぬまま情報欠損するサイレント破損のため、経過観察に留める。

**M. テストカバレッジ不足: 複数ページシート・主回路シート・構造検証（PushTransformの
境界=表題欄が誤って縮小対象に入らないか）の回帰テストが無い**
- 箇所: `tests/Ecad2.Core.Tests/DiagramRendererPageScaleTests.cs`、
  `tests/Ecad2.Core.Tests/PdfExporterTests.cs`
- verify: 上記A〜Dの欠陥がいずれも既存13件のテストでは検知されない構造（単一ページ・
  非主回路シートのみが対象）であることを確認。次の増分での追加を推奨。

### REFUTED（指摘却下、3件）

- **未確定編集の無言消失懸念**（連続ダブルクリック時、前の行の未確定入力が上書きで消える）→
  verifierが実際のイベント登録順序を追跡し実証。`LadderCanvas`コンストラクタの
  `PreviewMouseLeftButtonDown += (_,_) => Focus()`（52-53行）がXAML側ハンドラより先に
  登録されるため、ダブルクリック1打目のMouseDown時点で`LostKeyboardFocus`が先行発火し
  既存編集が確定・保存されてから2打目の行移動処理に至る。消失経路は存在しない。
- **CommitRungCommentEditorの無条件RedrawCanvas()呼び出し**（変更有無を確認しない無駄な
  全体再描画）→ 同ファイル内`CommitDeviceNameEdit`が全く同一の設計パターン
  （setter内部の同値ガードに委ね、常時RedrawCanvas()を呼ぶ）を既に確立・文書化済みと判明。
  「本メソッドだけが規約に反している」という前提が誤りで、意図的な既存規約に倣った実装。
- **PushTransform/PopTransformの間の例外でPop不整合になる懸念** → T-060レビューでの実測
  （`DrawingContext.Close()`は不均衡なPushを許容し例外を投げないことを最小WPFアプリで実証済み）
  がそのまま適用可能。かつ区間内の全呼び出し（PartResolver・画像読込・netlistインデクサ等）は
  いずれも既に防御的実装済みで、現実的な例外トリガーが見当たらない。

## 総括

DoD(1)〜(5)(7)(8)（記入UI本体）は殿裁定4点と概ね整合するが、UI連動面で4件（E・F・G・H）の
実害あるCONFIRMEDが見つかった。特にDoD(6)（縮小フィット、P-060対応の要）は**核心的な目的
（印刷はみ出し防止）を4つの独立した経路（A〜D）で達成できていない**——印刷枠を考慮しない
判定基準（A）、縮小そのものが余白バランスを崩す副作用（B）、複数ページへの一律適用（C）、
主回路シートへの非対応（D）——いずれも独立した検証エージェントが具体的な数値・入力例で
実証しており確度が高い。P-060の根本原因究明は隠密の前回調査で完了していたが、その対処
（縮小フィット）の実装自体に新たな欠陥が複数内包される形となった。I（キーボードファースト
逸脱）はバグではなくスコープ判断事項として殿確認を推奨。J〜Mは経過観察で、次の増分または
着手時に拾えば足りる規模。
