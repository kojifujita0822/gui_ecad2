# T-034 App層テストプロジェクト新設 レビュー（隠密・静的レビュー）

対象: コミット`ef787b5`（`feat(app): T-034 - App層テストプロジェクト新設+Dirty/HasProject回帰テスト`）。
変更ファイル: `src/Ecad2.sln`・`tests/Ecad2.App.Tests/{Ecad2.App.Tests.csproj, MainWindowViewModelTests.cs,
SheetNavigationViewModelTests.cs}`（新設8件のテスト）。

家老委任の3観点＋`code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）を併用。
実機（`dotnet build`/`dotnet test`）でも複数点を検証した。

---

## 総評

**土台は健全。** ビルド・テスト実行を実機確認し、二重エントリポイント等の構成問題は無し。
MainWindowViewModel側の主要なMarkDirty呼び出し3箇所（要素配置/削除/デバイス名変更）は実質的に
テストされている。

**ただし、コミットメッセージ自身が認めている通り、SheetNavigationViewModelのAddCommand/
RenameCommand（シート追加・改名という主要な変更入口）のMarkDirty呼び出しはテストの盲点として
残る。** さらに実機検証の結果、**この盲点は「本当にテスト不可能」だったのではなく「部分的な
検証（try/catchでのNRE許容）を試みれば安価に埋められた」ことが判明した**。これは家老・侍への
重要な追加情報として報告する。

---

## 家老の3観点への回答

### 観点1: テストの実質性（MarkDirty呼び忘れ等の回帰を本当に検出できるか）

**MainWindowViewModel側は実質的（事実）。** MarkDirty()の呼び出し箇所3つ全てが対応するテストで
カバーされている：

| 呼び出し箇所 | テスト |
|---|---|
| `MainWindowViewModel.cs`241行目（`SelectedElementDeviceName`setter） | `SelectedElementDeviceName_Set_MarksDirty` |
| `MainWindowViewModel.cs`261行目（`DeleteSelectedElement`） | `DeleteSelectedElement_MarksDirty` |
| `MainWindowViewModel.cs`332行目（`PlaceElementAtSelectedCell`） | `PlaceElementAtSelectedCell_MarksDirty` |

**SheetNavigationViewModel側は部分的（要注意）。**

- **[CONFIRMED] AddCommand（74行目）・RenameCommand（127行目）のMarkDirty呼び出しは
  T-034新設8件のどれからもカバーされていない。** コミットメッセージ・テストファイルの
  XMLコメントで「`Application.Current.Dispatcher`依存によりテストプロセスでNRE」と
  明記され、`docs/todo.md`のT-034行にも「P-016へ起票（改修は範囲外）」と追跡されており、
  隠蔽ではなく認識済みの制約。T-034導入の目的（MarkDirty呼び忘れの歯止め）に照らすと、
  最も使用頻度の高いシート追加・改名操作がまさに盲点として残っている。
- **[CONFIRMED・実機検証済み] この盲点は部分的に埋められた可能性がある。**
  AddCommand/RenameCommand内のMarkDirty()呼び出しは、NREの原因となる
  `Application.Current.Dispatcher.BeginInvoke`呼び出しより**前**に同期実行されている
  （`SheetNavigationViewModel.cs`74行目→88行目、127行目→130行目）。実際に
  `try { vm.SheetNavigation.AddCommand.Execute(null); } catch (NullReferenceException) { }`
  でNREを握りつぶした上で`Assert.True(vm.IsDirty)`を検証する使い捨てテストを作成し、
  `dotnet test`で**合格することを実機確認した**（検証後ファイルは削除、作業ツリーは
  クリーンに復元済み）。つまり「Dispatcher依存でテスト不能」という判断は、コマンド全体を
  実行できないという意味では正しいが、MarkDirty呼び出し自体は安価に検証可能だった。
  この部分的検証を試みなかった点は、往復に含めるかどうかは家老の裁量に委ねるが、
  P-016（Dispatcher依存の根本解消）とは別に「今すぐ埋められる穴」として認識しておく
  価値がある。
- **[CONFIRMED] `DeleteCommand_MarksDirty`テストの意図と実装が食い違っている。**
  テストは「シート2を追加してから削除する」体裁だが、`SelectedSheet`は`CurrentSheetIndex`
  （既定値0）由来のため、実際に削除されるのは`NewDocument()`が生成した「シート1」であり、
  追加した「シート2」ではない（`ResetSheets()`は`CurrentSheetIndex`を変更しないため）。
  **MarkDirty検出自体への影響はない**（どちらのシートが消えてもMarkDirtyは呼ばれる）が、
  テストコードの意図と実際の動作が乖離しており、可読性・保守性の観点で修正が望ましい。
- **[CONFIRMED] 「最後の1枚は削除不可」という重要な制約（`DeleteCommand`の
  `CanExecute: () => Sheets.Count > 1`、108行目）を検証するテストが無い。** `RelayCommand.Execute`
  は`CanExecute`を経由せず直接実行する設計のため、テストコードから`CanExecute`の値を
  明示的にアサートしない限りこの制約は検証されない。将来この条件が誤って緩められても
  検出できない。

### 観点2: テスト間の独立性

**問題なし（事実）。** 各テストは`new MainWindowViewModel()`で独立したインスタンスを生成、
一時ファイルは`Guid.NewGuid()`ベースでユニーク、`TraceLog.IsEnabled`はテストプロセスでは
常にfalse（`App.OnStartup`を経由しないため`TraceLog.Initialize`が呼ばれない）でテスト結果に
影響しないことを確認した。

### 観点3: net8.0-windows+UseWPFテスト構成の妥当性

**妥当（実機確認済み）。** `Ecad2.App.csproj`（`net8.0-windows`+`UseWPF=true`+`OutputType=WinExe`）
への`ProjectReference`がある以上、`UseWPF=true`は必須。`dotnet build`/`dotnet test`を実行し、
二重エントリポイントやビルドエラーが起きないことを確認した（`WinExe`への`ProjectReference`は
コンパイル済みアセンブリの公開面のみを取り込むため、`App.xaml.cs`の`Main`は再コンパイルされない）。
既存`Ecad2.Core.Tests.csproj`との命名規則・配置パターン（`tests/{Project}.Tests/`）とも整合。

---

## 発見事項（追加、重大度順）

### [PLAUSIBLE] `ResetDirtyViaSave`がファイルI/O経由という重い手段
`MainWindowViewModelTests.cs`76-86行目。`IsDirty`のsetterがprivateなため、Dirtyフラグを
falseに戻すためだけに`SaveToFile`（実ファイル書き込み）を経由している。意図的な設計判断
（「公開APIのみを使う」というコメント明記）であり見落としではないが、`SaveToFile`側に
問題が起きた場合、無関係なテスト（`DeleteSelectedElement_MarksDirty`等）まで巻き添えで
失敗し得る。`InternalsVisibleTo`は現状リポジトリ内に0件で、導入すれば軽量化の余地あり。

### [PLAUSIBLE] `LoadFromFile_ReplacesDocumentAndClearsDirty`がドキュメント内容自体を検証しない
`HasProject`/`IsDirty`/`CurrentFilePath`のみで、読み込んだ要素の中身（DeviceName等）を
検証していない。ただし`GcadSerializer`のシリアライズ/デシリアライズ往復一致自体は
`tests/Ecad2.Core.Tests/GcadCompatibilityTests.cs`で別途カバー済みのため、実害はほぼ
吸収されている。テスト単体の記述としては改善余地あり。

### [CONFIRMED・軽微] `CLAUDE.md`の担当パス列挙に`tests/Ecad2.App.Tests/`が未記載
`CLAUDE.md`31行目の担当パス列挙が更新されていない。T-034は殿承認済み・正規の采配であり
無断拡張ではなく、単純な記載漏れ。

---

## 結論・提案

コミット自体に「要修正」というほどの欠陥は無いが、以下は家老の裁量で判断されたい：

1. **AddCommand/RenameCommandのMarkDirty検証**: try/catchでの部分的検証が実機で可能と
   判明した。P-016（Dispatcher依存の根本解消）とは別に、往復での追加対応 or 別タスク化を
   検討する価値がある。
2. `DeleteCommand_MarksDirty`のコメント（テスト意図）と実装の食い違いは軽微な修正で足りる
   （テスト結果自体は正しい）。
3. 「最後の1枚削除不可」の検証テスト追加は低コストで価値がある。
4. `CLAUDE.md`31行目への`tests/Ecad2.App.Tests/`追記。

いずれもアプリ本体の機能・T-034の合格判定（11件合格）を覆すものではない。

---

## 出典
- コミット`ef787b5`（`git show`・`git diff ef787b5~1..ef787b5`で全文確認）
- `code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）
- 実機検証: `dotnet build`/`dotnet test`実行（二重エントリポイント無し確認）、使い捨てテストで
  AddCommand経由のtry/catch検証（合格確認後ファイル削除・作業ツリー復元済み）
- `docs/todo.md`T-034行（P-016起票の記載）
- `tests/Ecad2.Core.Tests/GcadCompatibilityTests.cs`（`GcadSerializer`往復一致テストの既存カバレッジ確認）
