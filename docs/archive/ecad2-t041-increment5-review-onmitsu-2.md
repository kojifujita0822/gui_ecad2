# T-041増分5 再レビュー（隠密、往復2周目）

> 2026-07-08 隠密レビュー。対象コミット`4264220`（`fix(app): T-041増分5修正往復1周目 -
> CurrentSheetIndexのsetter自体の穴を解消`）。家老指定観点(1)(2)＋`code-review`スキル
> （line-by-line/removed-behavior角度＋cross-file/品質角度、2エージェント並行）併用。
> 実測検証（`dotnet test`）も併用した。

---

## 結論：**観点(1)(2)自体はクリア。ただし`code-review`併用により、この修正が新たな
重大な副作用（記入中ドラフトの黙示的破棄）を生んでいることをCONFIRMED。侍へ追加修正を要する**

`docs/archive/ecad2-t041-increment5-review-onmitsu.md`（前回レビュー）で指摘した「シート削除でindex数値が
偶然一致するケース」自体は正しく解消されている。しかし、その解消手段（`CurrentSheetIndex`の
setterから早期returnを除去し、値変化の有無に関わらず後続処理を常時実行する方式）が、
シート削除以外の**既存の正常系（改名）**にも波及し、従来は早期returnで抑えられていた副作用
（記入中ドラフトの黙示的破棄）を新たに顕在化させた。

---

## 家老指定観点の検証

### (1) `CurrentSheetIndex`のsetterが、早期returnの前に無条件でクリア処理を実行する設計へ
正しく改まっているか —— **設計は正しく改まっている**

`MainWindowViewModel.cs:100-126`を確認。`if (!SetProperty(...)) return;`を`SetProperty(...)`
（戻り値無視）へ変更し、後続の`OnPropertyChanged(nameof(CurrentSheet))`・`SelectedCell = null`・
`SheetNavigation.RefreshSelectedSheet()`が値変化の有無に関わらず常時実行されるようになった。
`SelectedCell`のsetter（同ファイル162-200行目）も無条件でクロスカット的クリア
（`SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`/
`ClearConnectorDraftIfAny()`/`ClearFreeLineDraftIfAny()`）を実行する既存設計であり、両者が連鎖
することで、シート削除時にindex数値が変化しないケースでも選択状態・記入中状態が正しくクリア
されることを確認した。

### (2) 新規テスト2件が意図した経路（シート削除でindex数値が維持されるケース）を再現できて
いるか —— **正しく再現できている**

`tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`の新規2件を確認：

- `DeleteCommand_WhileDraftingConnector_WhenIndexNumberStaysSame_...`：2シート中の先頭
  （表示中、`CurrentSheetIndex`既定値0）を削除。`SheetNavigationViewModel.DeleteCommand`
  （`Math.Min(0, Sheets.Count-1=0)=0`）によりindex数値が0→0で変化しないケースを正しく踏む
  （`SelectedSheet`のgetterが`_owner.CurrentSheetIndex`経由でSheets[0]を返す実装のため、削除対象
  ＝表示中シートであることをコードトレースで確認）。
- 同上、自由線版も同型の経路を再現。

いずれも`ConnectorDraftPreview`/`FreeLineDraftPreview`が`null`になること、`Tool.Mode`が`Select`に
戻ること、新シートへ誤って確定されないこと（`Connectors`/`FreeLines`が空）を検証しており、
前回レビューで発見した実害（クロスリーク）を直接的に潰す内容になっている。

**実測**：`dotnet test --filter SheetNavigationViewModelTests`で9件（新規2件含む）合格、
`dotnet test src/Ecad2.sln`全体でCore14件・App68件、計82件合格を確認（侍のregression proof
報告「68件」＝App側件数と一致）。

---

## `code-review`スキル併用で判明した新規の副作用

家老指示に基づき`code-review`スキルを併用（line-by-line/removed-behavior角度＋cross-file/品質
角度、2エージェント並行、effort=medium相当に絞り込み）。侍の修正アプローチ自体
（`SetProperty`の戻り値を無視して常時後続処理を実行）が、シート削除以外の経路にも波及すること
を発見した。

### 所見L（CONFIRMED・重大）: シート「改名」が記入中の縦コネクタ/自由線ドラフトを黙って破棄する

`SheetNavigationViewModel.RenameCommand`（125-142行目）は、リネーム確定後に
`Dispatcher.BeginInvoke(ContextIdle, () => SelectedSheet = sheet)`（141行目）を呼ぶ。この
`sheet`は同一`index`のまま参照だけが入れ替わる（`RemoveAt+Insert`）ため、`SelectedSheet`の
setter（32-38行目）経由で`_owner.CurrentSheetIndex = index`が**数値上変化しない値**で呼ばれる。

- **修正前**（早期return版）：`SetProperty`が`false`を返し後続処理が丸ごとスキップされていた
  ＝リネームは選択状態・記入中状態に無害だった。
- **修正後**：同じ経路が無条件実行されるため、リネームするだけで`SelectedCell`・全選択状態
  （`SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`）が
  クリアされ、**記入中の縦コネクタ・自由線ドラフト（`ClearConnectorDraftIfAny()`/
  `ClearFreeLineDraftIfAny()`経由）も警告なく取り消される**。

`MainWindow.xaml.cs`の`RenameSheetButton_Click`（96-103行目）には`Tool.Mode`のガードが無く、
実際に到達可能であることをコードトレースで確認した：

```
sF9で縦コネクタ記入開始(_connectorDraft設定, Tool.Mode=PlaceLine)
  → 改名ボタン押下・ダイアログOK
  → RenameCommand.Execute（sheet.Name変更、Sheets.RemoveAt+Insert）
  → 次のUIアイドルフレームでSelectedSheet=sheet(同一index)実行
  → CurrentSheetIndexのsetter(数値不変でも常時後続処理実行)
  → SelectedCell = null
  → ClearConnectorDraftIfAny()が無条件発火、記入中ドラフト消失
```

**単体テストでは検証不可**：既存の`RenameCommand_MarksDirty`テスト（167-176行目）が
`Application.Current.Dispatcher`依存の`NullReferenceException`をtry/catchで握りつぶし
「`P-016まで既知の制約`」としているとおり、xUnit環境では`Dispatcher.BeginInvoke`自体が例外化
するため、この遅延経路は単体テストに現れない。**実機での確認が必須**。

対応要否・実装方針は侍・家老の判断に委ねるが、参考として：`RenameSheetButton_Click`または
`RenameCommand`に「記入中（`Tool.Mode != Select`）なら改名前にドラフトを確定/警告する」ガードを
追加するか、根本的には所見M（下記）のとおり`CurrentSheetIndex`のsetter自体の粒度を見直すのが
筋が良いと考える。

### 所見M（Altitude、severity中）: `CurrentSheetIndex`のsetterが「クロスカット的クリア」と
「自身の変更通知」を区別せず一括で無条件化している

手本にした`SelectedCell`のsetter（162-200行目）は、クロスカット的クリア（175-190行目、
`SelectedConnector`等）を`SetProperty`呼び出しより**前**に無条件配置する一方、このプロパティ
自身の派生通知（193-198行目、`SelectedCellDisplay`等）は`if (SetProperty(...))`で**値変化時のみ**
発火するという粒度分けをしている。

`CurrentSheetIndex`の修正はこの区別を踏襲せず、`OnPropertyChanged(nameof(CurrentSheet))`という
「このプロパティ自身の変更通知」までクロスカット処理と一緒くたに無条件化した。これが所見Lの
根本原因であり、かつ以下の副次的な冗長発火（実害は軽微、Efficiency）も生んでいる。

- `OutputPanelViewModel.JumpTo`（90行目）の同一シートDRCジャンプで、`RedrawCanvas()`が最大3回
  発火する（従来1回）。
- `SheetNavigationViewModel.AddCommand`のwasEmpty経路（既存の`NotifyCurrentSheetChanged()`
  ワークアラウンドと合わせ二重発火）。
- `SheetNavigationViewModel.DeleteCommand`115行目の`OnPropertyChanged(nameof(SelectedSheet))`が、
  110行目の`CurrentSheetIndex`セッター内の`RefreshSelectedSheet()`（同じく`nameof(SelectedSheet)`
  通知）と完全に重複（Reuse/Simplification、掃除し忘れ）。

`SelectedCell`と同じ粒度（クリア処理は無条件・プロパティ自身の変更通知は条件付き）へ揃えれば、
所見Lと上記冗長発火はまとめて解消できると考える。

### 所見N（軽微、ドキュメンテーション）: テストコードのコメントが4264220で無効化された前提を
記述したまま残っている

`tests/Ecad2.App.Tests/SelectedConnectorExclusivityTests.cs:54`
（`SelectedCellAssignment_ClearsSelectedConnector_EvenWhenCurrentSheetIndexUnchanged`）の
コメントは「`vm.CurrentSheetIndex = vm.CurrentSheetIndex;`はSetPropertyの早期returnでシート内部
のクリア処理はスキップされる」と明記しているが、4264220でその早期return自体が除去されたため
事実と矛盾する。テスト自体は次行の明示的`SelectedCell`再代入により結果的にパスし続けるため
実害はないが、将来の保守者が「同値再代入は安全」と誤解する種になる。

### 所見O（参考、対応不要）: `OutputPanelViewModel.SelectedDiagnostic`は依然として早期return
ガードを残しており、同種の問題への設計方針がコードベース内で二重化している

`SelectedDiagnostic`のsetter（27-36行目）は`if (!SetProperty(...)) return;`のまま、
「同じ行の再クリック」問題は別経路の`JumpToDiagnostic`（View側から直接呼ぶバイパス）で解決
している。`CurrentSheetIndex`/`SelectedCell`は対照的にsetter自体を無条件化する方式を採った。
どちらが正しい流儀か明文化されていないため、将来同種のバグが再発する余地がある（対応必須では
ない、気づきとして記録）。

---

## severity整理

| 所見 | 種別 | severity | 対応要否 |
|---|---|---|---|
| L | 正しさ（regression） | **重大** | 侍修正推奨、忍者実機確認前に解消すべき |
| M | Altitude | 中 | Lの根本原因、Lと合わせて対応するのが効率的 |
| N | ドキュメンテーション | 軽微 | 任意（コメント修正のみ） |
| O | 設計論・参考 | 低 | 対応不要、気づきとして記録 |

---

## 出典・参照

- 対象コミット`4264220`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`CurrentSheetIndex`100-126行目、
  `SelectedCell`162-200行目、`_connectorDraft`/`ClearConnectorDraftIfAny`342-426行目）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`SelectedSheet`25-39行目、
  `AddCommand`66-100行目、`DeleteCommand`103-117行目、`RenameCommand`125-142行目）
- `src/Ecad2.App/ViewModels/OutputPanelViewModel.cs`（`SelectedDiagnostic`27-36行目、
  `JumpTo`86-100行目）
- `src/Ecad2.App/MainWindow.xaml.cs`（`ViewModel_PropertyChanged`34-45行目、
  `RenameSheetButton_Click`96-103行目）
- `tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`（新規2件、105-164行目）
- `tests/Ecad2.App.Tests/SelectedConnectorExclusivityTests.cs`（54行目コメント）
- `docs/archive/ecad2-t041-increment5-review-onmitsu.md`（前回レビュー、観点3 CONFIRMEDの原本）
- `docs-notes/handover-next-session.md`（次回セッションへの申し送り）
- `code-review`スキル（line-by-line/removed-behavior角度＋cross-file/品質角度、2エージェント
  並行、CONFIRMED1件・所見4件）
