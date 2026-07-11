# T-039 操作トレースログ基盤 修正後・再レビュー（隠密・静的レビュー）

対象: コミット`ad6b1fd`（`fix(T-039): 隠密レビュー指摘12件中8件を修正`）。
変更ファイル: `src/Ecad2.App/App.xaml.cs`・`src/Ecad2.App/Diagnostics/TraceLog.cs`・
`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`・`src/Ecad2.App/ViewModels/ViewModelBase.cs`。

家老委任の3観点＋`code-review`スキル（highレベル、8角度×並列finder→1-vote verify）を併用。
事実と推測を峻別し、出典（ファイル・行番号）を明記する。

---

## 総評

**前回指摘12件中8件対応（finding1,2,3,4,5,6,7,9）は、いずれも「方向性は正しいが適用範囲が
一部に留まっている」。** 具体的には、finding3（旧値null化）・finding4（環境変数判定）・
finding7（ボクシング回避）の3件は、**侍が対応した箇所以外の兄弟コードパスに同種の問題が
現存する**ことを実測・実装確認の両面で突き止めた。特にfinding3は「将来の再発リスク」ではなく
**既に現在のコードに実例がある**（`SaveToFile`メソッド）。

CRITICAL（finding1）は主要3経路（PropertyChanged/Focus/Click）については解消されているが、
**crash.log書込という第4の経路（finding1と同根の失敗モード）には適用されていない**ことを新たに
発見した。これは前回レビューの見落としであり、侍の対応漏れではなく隠密（前任）の指摘漏れである。

---

## 家老の3観点への回答

### 観点1: CRITICAL(finding1)のtry/catch隔離の網羅性

**主要3経路（PropertyChanged/Focus/Click）は解消（事実）。** `TraceLog.LogPropertyChanged`
（`TraceLog.cs`56-65行目）・`Write`（97-106行目）はtry/catchで隔離済み。

**ただし2つの穴が残る（下記findingsのCONFIRMED参照）**:
- **[新規CRITICAL相当] crash.log書込（`App.xaml.cs`91行目）にtry/catchが無い**。TraceLog.Writeと
  同一の失敗モード（複数インスタンス同時起動時のファイル共有違反等）に対して無防備で、finding1が
  「本来処理を道連れにしない」ために導入した思想がここには及んでいない。最後の安全網自体が
  機能しなくなるため、影響度はTraceLog側のfinding1より深刻（`code-review`のverifyでCONFIRMED）。
- **[軽微・理論上] `LogFocus`/`LogClick`（`TraceLog.cs`69-80行目）はtry/catchで保護されていない**。
  `Write(...)`の引数として評価される`Quote()`呼び出しがtry節の外側にあるため。現状`Quote(string)`
  は例外を投げない実装なので実害はゼロだが、将来`Quote()`の実装が変わった場合にFocus/Click経路
  だけ保護されなくなる（CONFIRMED、優先度は低）。

### 観点2: 8件の修正が原本指摘を実際に解消しているか

| finding | 判定 | 根拠 |
|---|---|---|
| finding1(CRITICAL) | **部分解消** | 主要3経路は解消。crash.log・LogFocus/LogClickに穴（上記） |
| finding2(起動安全網) | 解消 | DispatcherUnhandledException購読をTraceLog初期化より前に移動。副作用として懸念した「MainWindow構築中の例外捕捉可否が変化する」という仮説はWPF公式ソース確認の結果REFUTED（`Application.OnStartup`と実際のMainWindow構築`DoStartup()`は別メソッドで、購読位置の前後に関わらず捕捉可否は変わらない） |
| finding3(旧値null) | **部分解消（要修正）** | Tool/SelectedElementDeviceName/ReplaceDocumentは解消。**`MainWindowViewModel.cs`362-371行目`SaveToFile`が未修正のまま残存**：`CurrentFilePath = path;`と直接代入した直後、1引数版`OnPropertyChanged(nameof(CurrentFilePath))`を呼んでおり、`_pendingOldValue`はSetProperty経由でしかセットされないため旧値は常にnullになる。`ReplaceDocument`（同ファイル399-422行目）は2引数版で正しく修正済みなのと対照的（CONFIRMED、実装確認済み） |
| finding4(環境変数) | **部分解消（要修正）** | "false"/"off"/"no"は解消。**全角「０」(U+FF10)は依然半角"0"と不一致のため無効化されない**（前回指摘そのままの残存）。加えて**新たな穴**：`env.Trim()`により空白のみの値（例:半角スペース1文字）が空文字列になり無効化リストに一致しなくなるため、意図せず有効化される（いずれも実機で数値検証済み・CONFIRMED） |
| finding5(エスケープ) | 解消 | null/空文字列区別・`\`/`"`/`\r`/`\n`エスケープとも実装確認 |
| finding6(handledEventsToo) | 解消（副作用あり） | 追加自体は正しい。ただし`e.Handled=true`にされた操作もログに`event=Click ...`として記録されるようになり、「ログに記録=業務ロジックへ到達した」という誤読リスクを新たに生む（現状具体的な発現例は無いが、忍者の既存の警戒感「UIA迂回でClick自体が発火しない」パターンとは別種の罠としてPLAUSIBLE） |
| finding7(ボクシング回避) | **部分解消** | `SetProperty`経由（`ViewModelBase.cs`20行目）は`TraceLog.IsEnabled`でガード済み。**しかし新設の`OnPropertyChanged(string, object?)`オーバーロード（同37-41行目）にはガードが無く**、`Tool`セッター（`ToolState`はrecord struct、高頻度操作）・`ReplaceDocument`内の`CurrentSheetIndex`(int)/`SelectedCell`(GridPos?)で、TraceLog無効時（既定・本番運用）でも無条件にボクシングが発生し続ける。`SelectedElementDeviceName`(string)・`Document`(class)・`CurrentFilePath`(string)は参照型のためボクシング対象外（CONFIRMED、型定義まで確認済み） |
| finding9(重複ガード統合) | 解消 | `IsOriginalSourceTarget`ヘルパーへの統合を確認。ただしnull-forgiving(`element = null!`)の使用により、元のパターンマッチが持っていたコンパイラ検証済みnull安全性を手放している。現状の呼び出し元は必ず戻り値チェック済みで安全だが、将来レビューを経ずに誤用されても警告が出ない設計（PLAUSIBLE、優先度低） |

### 観点3: 見送り4件（finding8/10/11/12）の妥当性

**妥当（所見）。** ただし**finding8（TraceLog.WriteとOnDispatcherUnhandledExceptionの重複実装）は、
今回発見したcrash.log例外未処理問題と表裏一体**である。cleanup観点として見送るのは構わないが、
crash.log側のtry/catch追加自体は「重複実装の統合」を待たずとも1行で対応可能な独立した安全対策
であり、finding1の一部として扱うべきだったとも言える（所見）。finding10/11/12は従来通り妥当
（拡張性・T-034範囲）。

---

## 発見事項（重大度順、CONFIRMED優先）

### [重要] finding3-再発: `SaveToFile`でCurrentFilePathの旧値がnullになる
- **`MainWindowViewModel.cs`362-371行目**。`SaveToFile`メソッド内でSetPropertyを経由せず
  `CurrentFilePath = path;`と直接代入し、直後に1引数版`OnPropertyChanged(nameof(CurrentFilePath))`
  を呼んでいる。`ReplaceDocument`（399-422行目）は同じプロパティを2引数版で正しく修正済みなのに、
  `SaveToFile`側は前回・今回どちらのレビューでも見落とされ未修正のまま残った。
- **失敗シナリオ**: 「名前を付けて保存」操作をトレースログで追跡しようとしても、
  `CurrentFilePath`の旧パスが常にnullとして記録され、T-039の主目的（正確な操作トレース）が
  この経路でだけ果たせない。
- **verdict: CONFIRMED**（実装確認済み）

### [重要] finding1-穴: crash.log書込に例外処理が無い
- **`App.xaml.cs`91行目** `OnDispatcherUnhandledException`内`File.AppendAllText(logPath, ...)`。
  TraceLog.Writeと同一の失敗モード（複数インスタンス同時起動時のファイル共有違反等）に対し
  無防備。WPF公式情報（"DispatcherUnhandledExceptionハンドラ自身が例外を投げた場合、アプリは
  回復不能でクラッシュする"）を踏まえると、crash.log書込失敗時はMessageBox表示（最後の安全網）
  すら行われず、ユーザーに何の通知もなくアプリがサイレントに落ちる。TraceLog側の欠陥（一部の
  トレース情報欠落）より実害の程度が大きい。
- **verdict: CONFIRMED**

### [中] finding4-残存: 環境変数判定の穴2種
- (a) **`TraceLog.cs`21-26・42-43行目**。`DisableEnvValues`は半角文字列のみ。全角「０」
  (U+FF10)は`StringComparer.OrdinalIgnoreCase`でも半角"0"と一致せず、前回指摘のまま未解消。
- (b) **新規**: `env.Trim()`により、値が空白のみ（例:半角スペース1文字）の場合`Length: > 0`は
  真だがTrim後は空文字列になり、`DisableEnvValues`のいずれにも一致せず意図せず有効化される。
  旧コード（Trim無し）には存在しなかった、今回のTrim導入が生んだ新しい穴。
- **失敗シナリオ**: 運用者が意図して無効化設定をしても、全角混入や空白のみの誤設定で気付かず
  有効化されたままになり、finding1系のリスクに意図せず晒される。
- **verdict: CONFIRMED**（.NET 8実機で`DisableEnvValues.Contains("０")==false`
  ・`" ".Trim()==""`を実測確認）

### [中] finding7-穴: ボクシング回避が新設オーバーロード経由では無効
- **`ViewModelBase.cs`37-41行目**の`OnPropertyChanged(string, object?)`に`TraceLog.IsEnabled`
  ガードが無く、**`MainWindowViewModel.cs`28-30行目(Tool)・414/417行目(ReplaceDocument内
  CurrentSheetIndex/SelectedCell)**で、TraceLog無効時（既定・本番運用）でも値型の無条件
  ボクシングが発生する。C#の言語仕様上、メソッド呼び出し引数の評価（ボクシング含む）は呼び出し
  前に完了するため、メソッド内部でのガードでは防げない。
- **失敗シナリオ**: Toolはツール切替のたびに呼ばれる高頻度プロパティで、finding7が除去した
  はずのGCコストがTraceLog OFF時にも常に再発する。
- **verdict: CONFIRMED**

### [低〜中] handledEventsToo:trueによるログ意味論変化
- **`App.xaml.cs`41-46行目**。`e.Handled=true`にされた操作もログに記録されるようになり、
  「ログに記録=業務ロジックへ到達した」という誤読リスクを生む。現状具体的な発現箇所は
  未確認だが、将来同種の罠になりうる。
- **verdict: PLAUSIBLE**

### [低] 例外の完全な握りつぶしによる観測可能性の喪失
- **`TraceLog.cs`61-65・101-105行目**。catchが完全に無言で、失敗の事実自体が一切残らない。
  ただしこれは前回隠密レビューの推奨（「例外は握りつぶし、本処理には絶対に波及させない」）
  通りの意図した実装であり、逸脱ではない。「トレース失敗が無痕跡になる」という観点は前回
  レビューの射程外だった新しい論点として言及するに留める。
- **verdict: PLAUSIBLE（新規欠陥ではないが、無視できない懸念として言及）**

### [低] IsOriginalSourceTargetのnull-forgiving使用
- **`App.xaml.cs`51-60行目**。`element = null!`によりコンパイラのnull安全性検証を手放して
  いる。現状の呼び出し元は安全だが、将来の誤用を防ぐ静的な仕組みが無い。
- **verdict: PLAUSIBLE**

### [軽微] LogFocus/LogClickのtry/catch非対称性
- **`TraceLog.cs`69-80行目**。`Quote()`呼び出しがWrite()のtry/catch外側で評価される。現状
  `Quote(string)`は例外を投げないため実害ゼロだが、finding1の思想が字義通りには及んでいない。
- **verdict: CONFIRMED（構造的事実として）だが優先度は最低**

### REFUTED（検証の結果、成立しないと判断したもの）
- `_pendingOldValue`のスレッド競合: `src/`全体に`async`/`Task.Run`/`Thread`等の使用がゼロ件
  （Grep確認済み）で、ViewModelはUIスレッド単一実行のみ。現行コードでは起こり得ない。
- `RegisterTraceClassHandlers`の多重登録ガード欠如: `OnStartup`の呼び出し元は1箇所のみで、
  複数回実行される経路が実装・テストコードのいずれにも存在しない。
- DispatcherUnhandledException購読順序変更によるウィンドウ非表示残留リスク: WPF公式ソース
  （`Application.OnStartup`と実際のMainWindow構築`DoStartup()`は別メソッド）を確認した結果、
  購読位置が`base.OnStartup(e)`の前後どちらでも、`DoStartup()`実行時点の捕捉可否は変わらない
  ことが判明。前提が誤りのため退行なし。

---

## 出典
- コミット`ad6b1fd`（`git show`・`git diff ad6b1fd~1..ad6b1fd`で全文確認）
- 前回レビュー`docs/archive/ecad2-t039-implementation-review-onmitsu.md`（finding1〜12原本）
- `code-review`スキル（highレベル、8角度×並列finder→1-vote verify、本隠密がAgent toolで
  直接orchestrate）
- WPF公式ソース（dotnet/wpfリポジトリ、`Application.OnStartup`/`DoStartup`の実装、verify時に
  該当エージェントがWeb確認）
- `MainWindowViewModel.cs`362-371行目（`SaveToFile`、本隠密が直接確認）
- .NET 8実機での文字列比較・Trim挙動の実測（verify時に該当エージェントが使い捨てプロジェクトで検証）
