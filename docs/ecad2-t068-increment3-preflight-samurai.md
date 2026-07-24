# T-068増分3（形状編集キャンバス本体）着手前調査（侍）

家老采配（2026-07-25）に対する成果。**調査のみ・実装未着手**。
対象DoD：(1) PoC成果から本実装へ移す際の差分・障壁 (2) 増分0検証書の申し送り2件の扱い方針
(3) Core層のinternal制約（`PartDrawing`）の解き方。

参照：`docs/ecad2-t068-part-editor-plan-onmitsu.md`、`docs/ecad2-t068-increment0-poc-verification-onmitsu.md`、
`poc/t068-part-editor-poc/`（README・`PartEditorCanvas.cs`）、本実装側の一次ソース（下記各節に path:line 記載）。

---

## 0. 結論の要旨

技術的な障壁は増分0の判定どおり無い。ただし**本実装へ移す前に確定すべき分岐が8点**あり、うち4点は
UI/UX判断（殿裁定を要する）、3点は技術方針（家老裁可）、1点は実装内で対処可能なバグ回避である（7節）。

最大の障害は技術ではなく**器**——現行の`PartEditorDialog`は440x420固定・`ResizeMode="NoResize"`であり、
形状編集キャンバスを載せる余地が構造的に無い（1.1）。ここが決まらねば他の設計が定まらない。

---

## 1. DoD(1) PoCから本実装への差分・障壁

### 1.1 ホスト先の構造的制約【最重要・分岐1】

| 観点 | PoC | 本実装（現行） |
|---|---|---|
| 器 | 専用`MainWindow`全面（既定900x650、`PartEditorCanvas.cs:653-654`） | `PartEditorDialog`（`Views/PartEditorDialog.xaml:4-5` Height=440・Width=420・`ResizeMode="NoResize"`） |
| 構成 | ツールバー＋キャンバス＋ステータスバー | `TabControl`2タブのみ（プロパティ／端子、`:16-65`） |
| 形状用の器 | キャンバスが主役 | **プレースホルダすら無い**（`PartEditorDialog.xaml.cs:11`「形状(Primitive)編集は増分3で別途扱う(本ダイアログのスコープ外)」） |

440x420の固定ダイアログへ、ツールバー9種＋キャンバス＋ズーム/ステータス表示を収めるのは実用的でない。
隠密プラン§4の分岐点3「パーツエディタの画面形態（モーダルダイアログ／AvalonDockペイン）」は、増分1で
**導線（メニュー「パーツ(_P)」新設＝案B）**が裁定されたのみで、**キャンバスを持つ画面形態そのものは未裁定**。

### 1.2 ツールの過不足

| ツール | GuiEcad原本 | PoC（増分0） | 増分3での扱い |
|---|---|---|---|
| 選択／線／折れ線／矩形／円／弧／回転 | あり | あり（`EditTool`、`PartEditorCanvas.cs:17`） | 移植（検証済み） |
| 文字（`PartText`） | あり | **無し**（設計書でスコープ外） | **未裁定＝分岐3** |
| 接続点（端子） | あり | **無し** | **必須。ただし方式が未確定＝分岐2** |

接続点は増分2の殿裁定「まずリスト形式で仮実装、増分3着手時にキャンバス上ドラッグへ正式統合」に基づき
増分3の必須要件だが、**PoCで一切検証されていない唯一の要素**である。既存のリスト形式（端子タブ、
`PartEditorDialog.xaml:46-64`）を残して併存させるのか、キャンバス操作へ置換するのかも未確定。

### 1.3 Undo基盤は流用不可——新規実装が要る

- App層の`UndoManager`（`src/Ecad2.App/Commands/UndoManager.cs:12`）は**`LadderDocument`全体をJSON文字列化して
  積むスナップショット方式**（`RecordSnapshot(LadderDocument)`:22、`Undo(LadderDocument)`:29）。
  パーツエディタのローカル編集（`PartDefinition`）には型からして適用できない。
- PoCの`List<PartPrimitive>`シャローコピー方式（`PartEditorCanvas.cs:505-538`）をそのまま持ち込むのが自然。
  全プリミティブが`sealed record`（`PartDefinition.cs:33-42`）でイミュータブルゆえ、シャローコピーで正しく機能する
  前提は本実装でも成立する。
- ただし**Undoの適用範囲が非対称になる**：形状タブ＝Undo可、プロパティ・端子タブ＝Undo不可（増分1・2は素の
  `TextBox`/`DataGrid`）。この非対称を許容するか、ダイアログ全体へUndoを及ぼすかは**分岐4**。

### 1.4 座標系・基準枠の連動

- PoCは`GridGeometry(cellMm: 9.0, marginMm: 30.0)`固定（`PartEditorCanvas.cs:27`）、基準枠も**8x4セル固定**（`:666`）。
- 本実装では`WidthCells`/`HeightCells`（1〜12、増分1でプロパティタブから編集可能、`PartEditorDialog.xaml.cs:104-113`）へ
  基準枠を連動させ、プロパティタブでの幅高さ変更を形状タブへ即時反映する配線が要る。
- 原点規約は「原点=最左ポート点・行中心線=y0」（`PartDefinition.cs:21`、`PartDrawing.cs:6-7`）。PoCは端子を
  一切描画していないため、**原点と端子の視覚的対応は未検証**。増分3では端子をキャンバス上へ描く必要があり、
  `RowOffset`/`BoundaryOffset`とキャンバス座標の対応づけを実装時に確定させねばならない。

### 1.5 座標変換の合成順は整合を確認済み（障壁ではない）

PoCは`renderer.PushTransform(_panMm.X, _panMm.Y, _zoom)`（`PartEditorCanvas.cs:662`）でpan/zoomを掛けつつ、
`DrawPrimitive`側では`CellToWorldMm`（`:126`）でmargin+cell変換を手動適用する二段構えを採る。
`WpfRenderer.PushTransform`は`scale→translate`順の`TransformGroup`で、translateのみ`×K`でDIP換算する
実装（`WpfRenderer.cs:28-34`）。最終DIP = `worldMm×K×zoom + panMm×K` となり、PoCの`CellToDip`（`:128-134`）・
逆変換`DipToCell`（`:136-143`）の式と**一致する**。この点に不整合は無い。

ただし既存ラダーキャンバスのズームは`LayoutTransform`の`ScaleTransform`方式（`MainWindow.xaml:1415-1424`、
`CanvasScale`バインド）であり、**PoCの`PushTransform`方式とは機構が異なる**。パン併用を考えるとPoC方式が
扱いやすいが、既存との不統一は認識しておく。

### 1.6 Primitivesの参照共有——キャンセル時のデータ破壊リスク【分岐8＝実装内で対処】

現行は`Primitives = original.Primitives`の**素通し**（`PartEditorDialog.xaml.cs:140`）。増分3で形状タブから
このリストを直接書き換えると、**キャンセルしても元の`PartDefinition`が破壊される**。編集導線は
`EditPartMenuItem_Click`（`MainWindow.xaml.cs:512`）が`entry.Definition`を渡す形で、この実体は
`PartPaletteViewModel.Load`（`:62-63`）が`Library.ById`へ投入したものと同一参照。キャンセル時は`Load()`が
走らないため、画面上のラダー図に破壊が残る。
→ **キャンバスへ渡す時点でリストのコピーを作る**こと。仕様判断ではなくバグ回避ゆえ実装内で対処する。

### 1.7 テスト可能性【分岐6】

- 増分1・2のロジック（名前・幅高さバリデーション、ポート2点未満拒否、`BoundaryOffset`昇順ソート）は
  すべてコードビハインド内にあり、**テストは0件**（`tests/`配下に`PartEditorDialog`を参照するテストは存在しない）。
- PoCも全ロジックが`FrameworkElement`派生クラス内。
- 増分3で加わる幾何ロジック（ヒットテスト距離計算 `DistanceToPrimitive`:593、回転 `RotatePoint`:455、
  スナップ `SnapValue`:145、退化判定 `IsDegenerate`:465、`BuildPrimitive`:474）は**`System.Windows`非依存の
  純粋関数へ切り出せば STAThread 不要で単体テスト可能**。切り出せば増分3の回帰テストがRED証明つきで書ける。
- ただし「純粋関数を別クラスへ分離する」のは采配範囲を超える設計判断ゆえ、家老裁可を仰ぐ。

### 1.8 `PartOptimizer.MergeCollinearLines`が保存経路で未適用【分岐7】

GuiEcad原本は保存時に適用（隠密プラン§1.4）。現行の保存経路（`PartPaletteViewModel.SaveNewPart`:82-86 /
`SaveEditedPart`:90-96）では**適用されていない**。`PartOptimizer`自体はpublicで利用可能
（`src/Ecad2.Core/Model/PartOptimizer.cs:4,11`）。線を描けるようになる増分3で初めて意味を持つ論点。

### 1.9 既配置要素の再描画（増分1・2からの積み残し、増分3で顕在化の見込み）

保存後に`Library.ById`の中身は更新される（`PartPaletteViewModel.Load`:62-63、インスタンスは差し替えず中身のみ
入替）が、**ラダーキャンバスの再描画トリガが保存経路に見当たらない**。増分1・2では形状を編集できなかったため
見た目の変化が生じず顕在化しなかったが、増分3では「形状を編集して保存→既に配置済みのパーツの見た目が
変わらない」という形で表面化する見込み。増分3の実機確認観点に含めることを推奨する。

---

## 2. DoD(2) 増分0検証書の申し送り2件の扱い方針

### 2.1 申し送りA：選択のみでUndoが1エントリ余分に積まれる

**原因（一次ソースで確認済み）**：`BeginSelectOrMove`（`PartEditorCanvas.cs:310-322`）が命中時に無条件で
`_dragSnapshotBeforeChange = _primitives.ToList()`を採取し、`CommitMove`（`:331-340`）が変化量を判定せず
`PushUndoSnapshot`を呼ぶ。回転側も同型（`BeginRotate`:363-375／`CommitRotate`:387-396）。

**方針：増分3実装時に修正する。殿・家老の判断は要しない。**
理由——本プロジェクトのUndo呼び出し規約は既に「**値が実際に変化する場合のみ記録する**」と確立している
（`UndoManager.cs:20-21`のコメント、`MainWindowViewModel.cs:2145`、`FindViewModel.cs:210-214`）。既存規約への
追従であり、新規の仕様判断ではないため。

**実装案**：GuiEcad原本と同じく変化フラグ（原本の`_dragChanged`相当）を持ち、`UpdateMoveDrag`/`UpdateRotateDrag`が
実際に値を書き換えたときのみ立てる。`dx==0 && dy==0`の単純判定より堅い（原本は「動かして戻した」場合も記録する
挙動であり、フラグ方式がそれを踏襲する）。

**テスト**：1.7の純粋関数分離が裁可されれば「変化なしでUndoが積まれない」をRED証明つきで書ける。分離しない場合は
実機確認（ステータスバーのUndoスタック数表示）へ委ねる。

### 2.2 申し送りB：Ctrl+ホイールズーム時にステータスバーが更新されない

**原因（一次ソースで確認済み）**：`Zoom`プロパティのsetter（`PartEditorCanvas.cs:91-99`）が`Notify()`
（`StateChanged`発火＋`Draw()`）ではなく`Draw()`のみを呼ぶため、ホスト側の状態更新が起動されない。隠密の
特定内容と一致する。

**方針：増分3実装時に修正する。殿・家老の判断は要しない**（表示専用の実装漏れ、修正はsetterで`Notify()`を
呼ぶのみ。`Notify()`は内部で`Draw()`も呼ぶため二重描画にはならない）。

**ただし従属論点あり**：本実装でズーム倍率を表示する場所を持つか自体が**分岐1（画面形態）に従属**する。
440x420のダイアログにステータスバーを置くのか、別形態にするのかで置き場所が変わる。画面形態が決まれば
自動的に定まるため、独立した分岐としては扱わない。

---

## 3. DoD(3) Core層 internal 制約の解き方

### 3.1 事実確認

- `PartDrawing`は**internal**（`src/Ecad2.Core/Rendering/PartDrawing.cs:9` `internal static class PartDrawing`）。
- `Ecad2.Core.csproj`は全9行で**`InternalsVisibleTo`指定なし**。リポジトリ全体でも`InternalsVisibleTo`は
  `src/Ecad2.App/AssemblyInfo.cs:7`の`Ecad2.App.Tests`向け1件のみ。
- `Ecad2.Rendering`名前空間でinternalなのは`PartDrawing`と`SymbolGlyphs`の2つだけ。他（`IRenderer`/`DrawingTheme`/
  `StrokeStyle`/`Point2D`/`Rect2D`/`Color`/`GridGeometry`/`DiagramRenderer`/`SvgRenderer`/`RenderOptions`）は
  すべてpublic。**「図形を実際に描く実装詳細」だけがinternal**という設計意図が読み取れる。
- PoCは「Core層無改変」（増分0設計書§6）を守るため描画ロジックを複製した（`PartEditorCanvas.cs:700-763`、約60行）。

### 3.2 選択肢の評価

**案A：`DiagramRenderer.DrawPreview`（public、`DiagramRenderer.cs:1119`）を使う** — 不採用を推奨
- 前例は`PartThumbnailRenderer.cs:52-62`。
- 難点1：引数が`ElementInstance`であり`PartLibrary`登録済みのパーツを前提とする。編集中の未保存
  `PartDefinition`を描くには一時ライブラリへの出し入れが要り迂遠。
- 難点2（決定的）：**プリミティブ単位の描き分けができない**。形状編集では「選択中の1プリミティブだけ別色」
  「作図中ドラフトを破線で」が必須要件だが、パーツ全体を単一`StrokeStyle`で描くAPIでは実現できない。

**案B：`PartDrawing`をpublic化するだけ** — 単独では不足
- 変更は最小だが、`Draw`は`part.Primitives`全体をループするAPI（`PartDrawing.cs:11-13`）のため案Aと同じ
  「プリミティブ単位で描けない」問題が残る。

**案C：`PartDrawing`へ「1プリミティブを描く」public APIを切り出す** — **侍の推奨**
- 現行`Draw`のswitch本体をそのまま`DrawPrimitive(IRenderer, DrawingTheme, PartPrimitive, double cell, StrokeStyle)`
  として切り出し、`Draw`はそれを`foreach`で呼ぶ形へ整理。クラスをpublic化する。
- 利点：(a) Appからプリミティブ単位で呼べる (b) PoCで複製した約60行が本実装では不要になり、描画ロジックが
  Coreに一元化される (c) 既存の唯一の呼び出し側（`DiagramRenderer.cs:1130`）は無改変で済む。
- 難点：**Core層の改変**にあたり、増分0の「Core層無改変」方針からの変更となるため裁可が要る。

**案D：App側で複製を維持（PoCと同じ）**
- Core無改変を貫けるが、描画ロジックがCoreとAppの2箇所へ恒久的に分岐する。将来プリミティブ種別を追加した際に
  片方の修正漏れを招く典型形（PR-07「共通ロジックの複製検知」が扱う類型そのもの）。

### 3.3 案C採用時の注意（座標系の差異）

Coreの`PartDrawing.Draw`はパーツローカル座標を`×cell`するのみで、原点合わせは呼び出し側の`PushTransform`に
委ねる設計（`DiagramRenderer.cs:1129`）。対してPoCの`DrawPrimitive`は各点へ`CellToWorldMm`（margin加算込み、
`PartEditorCanvas.cs:126`）を適用している。案Cを採るならApp側はmargin分もPushTransformへ寄せる形になる。
pan/zoomの合成順自体は整合を確認済み（1.5参照）。

### 3.4 推奨と次善

**案Cを推奨**。ただしCore層改変ゆえ家老の裁可を仰ぐ。裁可が下りぬ場合の次善は案D（複製維持）とし、その際は
「Coreの`PartDrawing`を変更したらApp側の複製も追随させること」の相互参照コメントを両側へ入れて発散を抑える。

---

## 4. 家老へ上げる分岐一覧

| # | 分岐 | 種別 | 侍の見立て | 節 |
|---|---|---|---|---|
| 1 | 形状編集の画面形態（440x420固定ダイアログをどうするか） | UI/UX（殿裁定） | 現ダイアログ拡大／別ウィンドウ／AvalonDockペインのいずれか。**他の設計がこれに従属するため最優先** | 1.1 |
| 2 | 接続点（端子）編集のキャンバス統合方式 | UI/UX（殿裁定） | 増分2のリスト形式を残して併存か、キャンバス操作へ置換か | 1.2 |
| 3 | 文字（`PartText`）ツールの要否 | UI/UX（殿裁定） | 原本にはあり、PoC未検証 | 1.2 |
| 4 | ダイアログ内Undoの適用範囲 | UI/UX（殿裁定） | 形状のみか、プロパティ・端子も含めるか | 1.3 |
| 5 | Core層`PartDrawing`のpublic API切り出し | 技術（家老裁可） | 案C推奨、次善は案D | 3 |
| 6 | 幾何ロジックの純粋関数分離（テスト可能化） | 技術（家老裁可） | 分離すれば増分3の回帰テストをRED証明つきで書ける | 1.7 |
| 7 | `MergeCollinearLines`の保存時適用 | 技術（家老裁可） | 原本踏襲なら適用 | 1.8 |
| 8 | `Primitives`の参照共有対策（キャンセル時のデータ破壊） | 実装内で対処 | 仕様判断でなくバグ回避、裁可不要と判断 | 1.6 |

---

## 5. 申し送り（増分3のスコープ外だが記録に残す）

- **既配置要素の再描画欠落**（1.9）：増分1・2の保存経路に再描画トリガが無い。増分3で顕在化する見込みゆえ、
  実機確認の観点に含めることを推奨。スコープ外の修正が要ると判明した場合は家老へ差し戻す。
- **増分1・2のロジックがテスト0件**（1.7）：`PartEditorDialog`のバリデーション・ポートソートは現状テストで
  守られていない。増分3の純粋関数分離（分岐6）が裁可されれば、同時に整理できる余地がある。
