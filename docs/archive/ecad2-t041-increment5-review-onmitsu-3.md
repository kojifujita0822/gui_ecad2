# T-041増分5 再レビュー（隠密、往復2周目の確認）

> 2026-07-08 隠密レビュー。対象コミット`1c23b5d`（`fix(app): T-041増分5修正往復2周目 -
> CurrentSheetIndexのsetter粒度をSelectedCellへ揃える`）。家老指定観点(1)(2)(3)を確認。
> 実測検証（`dotnet test`、および最小再現プログラムによるPropertyChanged発火実測）併用。

---

## 結論：**要修正（重大・再発）。所見Lは解消されたが、その解消手段が往復1周目で解消した
はずの観点3本体（シート削除で再描画が飛ばない）を、別の条件下で再発させている**

`docs/ecad2-t041-increment5-review-onmitsu-2.md`の所見L（改名で記入中ドラフトが警告なく破棄
される）は解消を確認した。しかし所見M対応（`CurrentSheetIndex`のsetterを`SelectedCell`と同じ
粒度へ統一：クロスカット的クリアは無条件、プロパティ自身の変更通知`OnPropertyChanged(nameof
(CurrentSheet))`は`SetProperty`が真を返した場合のみ）が、**「index数値が変わらず、かつ削除
直前に`SelectedCell`が既に`null`だった（＝記入中でも選択中でもない通常の削除操作）」という
条件下で、`CurrentSheet`のPropertyChangedが一切発火しなくなる**という新たな後退を生んでいる。
これは`docs/ecad2-t041-increment5-review-onmitsu.md`（初回レビュー）で報告した症状1「画面には
削除されたS0の最終描画がそのまま残り続ける」の再発そのものである。

---

## 家老指定観点の検証

### (1) 所見Lが解消されているか —— **解消を確認**

`MainWindowViewModel.cs:100-126`を確認。`SelectedCell = null` と `SheetNavigation
.RefreshSelectedSheet()` が`SetProperty`呼び出しより前に無条件配置され、`OnPropertyChanged
(nameof(CurrentSheet))`のみが`if (SetProperty(...))`でガードされる形に変わった。改名
（`RenameCommand`の遅延`SelectedSheet = sheet`、同一index代入）の場合も`SelectedCell = null`
は常時実行されるため、記入中ドラフト（`ClearConnectorDraftIfAny`/`ClearFreeLineDraftIfAny`は
`SelectedCell`のsetter内、無条件実行のまま変更なし）は引き続き正しく取り消される——ただし
これは「ドラフトが内部状態としてクリアされるか」の話であり、下記(3)の「画面に反映されるか」
とは別の問題であることに注意。

### (2) 所見Mの粒度統一が意図通りか、新たな副作用を生んでいないか —— **CONFIRMED：
新たな重大な副作用を生んでいる**

**根本原因（`SelectedCell`と`CurrentSheetIndex`の非対称性）**：`SelectedCell`は「値
（`GridPos`構造体）そのもの」が画面表示対象と1:1対応するため、「値が変化しなければ再描画
不要」という前提が成立する。しかし`CurrentSheetIndex`は「`Document.Sheets`への添字（キー）」
に過ぎず、**キーの数値が同じでも、参照先の実体（`Sheets[index]`）が入れ替わっている場合が
ある**（シート削除で後続シートが繰り上がるケース）。この非対称性を無視して`SelectedCell`と
同じ粒度パターン（プロパティ自身の変更通知は値変化時のみ）を適用したこと自体が、設計上の
誤りだったと考えられる。

**実測**：`ViewModelBase.SetProperty`（`EqualityComparer<T>.Default.Equals`によるガード）と
`MainWindowViewModel.CurrentSheetIndex`/`SelectedCell`の現行ロジックを最小再現し、スクラッチ
パッド上で独立実行して検証した（`src`/`tests`には一切書き込んでいない）：

```
=== シート削除(index数値不変、SelectedCellは削除前からnull) ===
発火したPropertyChangedイベント: []
CurrentSheetの変更通知は発火したか: False
```

`CurrentSheet`のPropertyChangedが一切発火しないことを実測で確認した。`MainWindow.xaml.cs`の
`ViewModel_PropertyChanged`（34-45行目）は`CurrentSheet`/`SelectedCell`等のPropertyChanged
でのみ`RedrawCanvas()`を呼ぶため、この経路では**`RedrawCanvas()`が一切呼ばれず、削除された
旧シートの描画が画面に残留する**。

**再現条件**：
1. シート削除で`Math.Min(index, Sheets.Count-1) == index`（index数値が変化しない、＝表示中
   シートが非末尾かつ繰り上がり後もindexが一致するケース）
2. かつ、削除直前に`SelectedCell`が既に`null`（セル未選択の状態で削除する、最も基本的な
   ユースケース）

条件2は、記入中ドラフトが無く、単にセルも選択せずにシートを削除するだけの、ごく自然な操作
で満たされる。往復1周目の新規回帰テスト2件（`DeleteCommand_WhileDraftingConnector_...`/
`DeleteCommand_WhileDraftingFreeLine_...`）はいずれも削除前に`SelectedCell`を明示的にセット
しているため、`SelectedCell`自身のPropertyChanged（null→値、削除でnullに戻る際に発火）が
`RedrawCanvas()`を代替的に呼んでしまい、この再発を検出できていない。**テストのカバレッジの
隙間（「記入中ドラフトのクリア」は検証しているが「CurrentSheet自体の再描画トリガー」は検証
していない）**が、往復1周目・2周目を通じて一貫して見落とされている。

### (3) 既存82件のregression維持 —— **維持を確認（ただし上記(2)はテスト範囲外のため
テストでは検出されない）**

`dotnet test src/Ecad2.sln`実行、Core14件・App68件、計82件合格を確認。侍のregression proof
報告と一致。ただし(2)の指摘どおり、この82件には今回発見した再発を検出するテストが含まれて
いないため、「テストが全部通ること」と「バグが無いこと」はイコールではない。

---

## 修正方針（参考）

`SelectedCell`と同じ粒度パターンを機械的に適用するのではなく、`CurrentSheetIndex`固有の性質
（キーが同じでも参照先が入れ替わりうる）を踏まえ、**`OnPropertyChanged(nameof(CurrentSheet))`
は`SelectedCell`と同様に無条件配置に戻す**のが最も筋が良いと考える。所見Lが問題としたのは
あくまで「改名のような正常系でも記入中ドラフトが黙って消える」ことであり、その原因は
`SelectedCell = null`（クロスカットクリア）の無条件実行そのものではなく、`CurrentSheetIndex`
の値が変わらない改名操作で`SelectedCell = null`という強い操作が働くこと自体にあった。

つまり本来分離すべきだったのは「`SelectedCell`のクリア」と「`CurrentSheet`の変更通知」では
なく、**「改名（参照だけ入れ替わるが同一シートに留まる操作）」と「削除（参照が別シートへ
実際に切り替わる操作）」という呼び出し元側の意味の違い**だったのではないか、という所見M
自体の前提を再検討する余地がある。例えば、改名の`Dispatcher.BeginInvoke`遅延経路
（`SheetNavigationViewModel.cs:141`）が、そもそも同一シートへの改名後再選択で`CurrentSheetIndex`
の代入（＝重い操作）を経由する必要があるのか自体を見直す（`SelectedSheet`のsetterを介さず、
改名時は`RefreshSelectedSheet()`だけを呼ぶ等）方が、`CurrentSheetIndex`のsetter自体は往復
1周目の「常時無条件」のまま温存でき、二重のモグラ叩きを避けられると考える。実装判断は侍・
家老に委ねる。

---

## 出典・参照

- 対象コミット`1c23b5d`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`CurrentSheetIndex`100-126行目、
  `SelectedCell`162-200行目）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`RenameCommand`125-142行目、
  `DeleteCommand`103-117行目）
- `src/Ecad2.App/MainWindow.xaml.cs`（`ViewModel_PropertyChanged`34-45行目）
- `src/Ecad2.App/ViewModels/ViewModelBase.cs`（`SetProperty`15-24行目、等価性ガードの実装）
- `src/Ecad2.Core/Model/Element.cs:34`（`GridPos`は`readonly record struct`、値の等価性を
  持つことを確認）
- 最小再現プログラム（スクラッチパッド、`src`/`tests`は未変更）：`ViewModelBase.SetProperty`
  と現行`CurrentSheetIndex`/`SelectedCell`のロジックを再現し、削除シナリオ（index不変・
  SelectedCell既にnull）でPropertyChangedが一切発火しないことを実測
- `docs/ecad2-t041-increment5-review-onmitsu.md`（初回レビュー、観点3 CONFIRMEDの原本、症状1）
- `docs/ecad2-t041-increment5-review-onmitsu-2.md`（前回レビュー、所見L/M/N/Oの原本）
- `tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`（新規回帰テスト2件、105-164行目、
  いずれも削除前にSelectedCellを明示セットしておりこの再発を検出しない）
