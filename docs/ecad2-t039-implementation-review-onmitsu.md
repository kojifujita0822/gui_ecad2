# T-039 操作トレースログ基盤 実装レビュー（隠密・静的レビュー）

対象: コミット562a0ad（`src/Ecad2.App/Diagnostics/TraceLog.cs`新設・`src/Ecad2.App/ViewModels/ViewModelBase.cs`・
`src/Ecad2.App/App.xaml.cs`）。家老委任の5観点＋`code-review`スキル（xhigh相当、10観点×並列finder→sweep）を
併用。事実と推測を峻別し、出典（ファイル・行番号）を明記する。

---

## 総評

**設計方針（案B、拙者の比較書推奨形）との整合は良好**。`ViewModelBase.OnPropertyChanged`一括フック・
`EventManager.RegisterClassHandler`によるフォーカス/Click横断捕捉・既定OFF+フラグ起動・高頻度プロパティ
除外、いずれも意図通り実装されている。

**ただし「例外安全」の観点で重大な欠陥がある。** `TraceLog`内部（`File.AppendAllText`・リフレクション
`GetValue`）に例外処理が一切無く、かつこの呼び出しが**WPF仕様上、既存の本来処理より必ず先に実行される
経路**に置かれているため、ログ書き込みが何らかの理由で失敗した瞬間、T-036で修正したはずのデバイス名編集
確定・ボタンのCommand実行・PropertyChanged通知そのものが**丸ごとスキップされる**。診断・トレース機能は
「本体の動作を絶対に阻害しない」のが原則だが、本実装はその原則を満たしていない。殿より「テストツールゆえ
信頼性は担保せよ」とのご下命があり、この観点を最優先で修正されたい。

---

## 家老の5観点への回答

### 1. 貴殿の比較書推奨形との整合

**整合している（事実）。** `docs/ecad2-t039-design-comparison-onmitsu.md`で提案した(a)`OnPropertyChanged`
一括フック、(b)フォーカスのクラスハンドラ、(c)Clickのクラスハンドラが、いずれもコミット562a0adで
その通りに実装されている（`ViewModelBase.cs`18-29行目、`App.xaml.cs`32-40行目）。

### 2. 既定OFF時の完全無副作用（ログ生成なし・性能影響なし）

**ログ生成は完全無し（事実）**。`TraceLog.IsEnabled`が既定false（`TraceLog.cs`25行目）で、
`LogPropertyChanged`/`LogFocus`/`LogClick`いずれも先頭で`if (!IsEnabled) return;`する（47・55・62行目）。
`App.xaml.cs`21行目も`if (TraceLog.IsEnabled) RegisterTraceClassHandlers();`のため、無効時はクラスハンドラ
自体が登録されずゼロオーバーヘッド。

**ただし性能影響は「ほぼゼロ」であって「完全ゼロ」ではない（事実、下記finding 7参照）**。
`ViewModelBase.SetProperty`の`_pendingOldValue = (propertyName ?? "", field);`（`ViewModelBase.cs`18行目）
は`TraceLog.IsEnabled`を見ずに無条件実行され、`field`が値型（`double`/`bool`/`int`/`GridPos?`）の場合
毎回ボクシングが発生する。軽微だが、忍者の現場要望文書が明示する「既定OFF時は完全無副作用」からは
わずかに逸脱する。

### 3. フック追加による既存Binding/フォーカス挙動への回帰リスク

**重大なリスクあり（事実、finding 1参照）。** WPF公式ドキュメント（Microsoft Learn、本隠密が
WebSearchで確認済み）により「クラスハンドラはインスタンスハンドラより先に実行される」ことが確定している。
新設のフォーカス/Clickクラスハンドラ内で`TraceLog`が例外を投げると、`DeviceNameBox_LostKeyboardFocus`
（T-036修正の要）やCommand実行・コードビハインドClickハンドラが実行されなくなる。詳細はfinding 1〜3。

### 4. 高頻度イベント除外の妥当性

**方向性は妥当だが、仕組みとして狭い（事実＋所見）。** `CanvasScale`除外の判断（Ctrl+ホイールでの連続
ズーム）自体は忍者の現場意見と合致し妥当。ただし対象はハードコードされた1プロパティのみの特殊対応で、
将来別の高頻度プロパティが増えた際に同じ発見→修正サイクルを繰り返す設計になっている（finding 10、優先度低）。
また、この除外機構はファイル書き込みのみを防ぐもので、上記finding 7のボクシングコストまでは防げない。

### 5. 例外安全（ログ書込失敗がアプリを道連れにせぬか）

**道連れにする（事実、finding 1・2）。** 本レビュー最大の指摘。詳細下記。

### ログファイル名（`ecad2-trace.log`）の命名可否

**問題なし、妥当な判断と考える（所見）。** T-038の一時診断ログ（`ecad2-diag.log`、原因確定後に侍が除去する
調査専用・都度削除前提）と、T-039の常設トレース（既定OFFだが恒久機能）は、ライフサイクル（一時 vs 恒久）
・粒度（特定バグの狙い撃ち vs 全操作の横断記録）が異なる別物であり、別名で共存させる設計は理にかなって
いる。将来「特定バグの深掘り」と「全体トレース」を同一検証セッションで併用するケースを想定しても、
ファイルが分かれていることで記録が混線しない利点がある。

---

## 発見事項（重大度順）

### [CRITICAL] finding 1: TraceLog内部の例外処理欠如が、有効時に本来の処理を丸ごとブロックしうる

- **`TraceLog.cs`68行目** `Write()`: `File.AppendAllText`にtry/catchが無い。
- **`TraceLog.cs`48行目** `LogPropertyChanged()`: `source.GetType().GetProperty(propertyName)?.GetValue(source)`
  というリフレクション呼び出しにもtry/catchが無い（現状の全ViewModelのgetterはnull安全・境界チェック済み
  で「今すぐ」発火する経路は未確認だが、将来`new`修飾での同名プロパティ隠蔽が加わると`AmbiguousMatchException`
  が伝播する；本隠密の`Angle A`/`Angle E`/`Angle D`各エージェントが同一結論に到達）。
- **`ViewModelBase.cs`28-29行目**: `TraceLog.LogPropertyChanged(this, propertyName, oldValue);`が
  `PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));`の**直前**に置かれている。
  上記いずれかが例外を投げると、29行目が実行されずWPFバインディング・`MainWindow.xaml.cs`の
  `ViewModel_PropertyChanged`（`RedrawCanvas()`を呼ぶ）が発火しない。
- **`App.xaml.cs`34-37・42-47行目**: `OnTraceFocusChanged`（`GotKeyboardFocus`/`LostKeyboardFocus`の
  クラスハンドラ、`typeof(UIElement)`へ登録）。WPF公式ドキュメント（本隠密がWebSearchで確認: クラス
  ハンドラはインスタンスハンドラより先に実行される）により、`MainWindow.xaml.cs`66-70行目の
  `DeviceNameBox_LostKeyboardFocus`（T-036のFocusScope跨ぎ修正、`UpdateSource()`呼び出し）より必ず先に
  実行される。ここで例外が起きると`DeviceNameBox_LostKeyboardFocus`自体が実行されない。
- **`App.xaml.cs`38-39・49-53行目**: `OnTraceButtonClick`（`ButtonBase.ClickEvent`のクラスハンドラ）。
  `ButtonBase.OnClick()`は`RaiseEvent(ClickEvent)`の直後に同一メソッド内でCommand実行へ進む実装のため、
  ここで例外が起きるとコードビハインドClickハンドラ19件・Commandバインディング3件
  （`SheetNavigation.AddCommand`/`DeleteCommand`/`OutputPanel.RunDrcCommand`）のいずれも実行されない。
  特に代替入力経路が無いボタン（`SelectDefaultButton`・`BuiltinPlaceButton`系6件・`OpenPartSelectionButton`、
  いずれもツールバー上のみでメニュー/ショートカット代替なし）は、その1クリックが完全に無反応となる。

**失敗シナリオ（統合）**: `--trace-log`有効時、`%TEMP%\ecad2-trace.log`への書き込みが何らかの理由
（複数の`--trace-log`付きインスタンス同時起動によるファイル共有違反、ウイルススキャン・エディタでの
一時ロック、ディスク容量不足等）で失敗すると、その瞬間に発生していたPropertyChanged通知・デバイス名編集
確定・ボタンクリックのいずれかが**サイレントに、または`DispatcherUnhandledException`のMessageBoxと共に**
無効化される。これはまさに検証セッション中（`--trace-log`を使う場面）にのみ発現するため、原因不明の
再現困難な不具合として観測される可能性が高い。診断ツールが診断対象の信頼性を損なうという本末転倒。

**推奨修正（所見）**: `TraceLog.Write`本体、および`LogPropertyChanged`のリフレクション取得〜`Write`呼び出し
全体をtry/catchで包み、ログ機能自体をベストエフォート化する（例外は握りつぶし、本処理には絶対に波及させない）。
この1箇所の修正で本findingの3経路（PropertyChanged/Focus/Click）すべてが解消される見込み。

### [HIGH] finding 2: 起動シーケンスの安全網空白

- **`App.xaml.cs`20-24行目**: `TraceLog.Initialize(e.Args)`・`RegisterTraceClassHandlers()`が
  `base.OnStartup(e)`（`StartupUri`によるMainWindow構築、初期フォーカス設定を含む）より前に実行される。
  `DispatcherUnhandledException`の購読（24行目）は`base.OnStartup(e)`の**後**。
- **`TraceLog.cs`38行目**: `Initialize`内で`IsEnabled`ならセッション開始行を即`Write`する。

**失敗シナリオ**: `--trace-log`有効時、起動直後（`base.OnStartup`実行中、`DispatcherUnhandledException`
未購読の区間）にログ書き込みが例外を投げると、アプリ自前のクラッシュログ（`ecad2-crash.log`）・
MessageBoxのいずれにも捕捉されず、素の未処理例外（既定のWERクラッシュダイアログ）でプロセスが落ちる。
検証セッション開始直後という最悪のタイミングで発生しうる。

**本隠密所見（Altitude・Angle C・Angle Eの3エージェントが独立に到達した結論と一致）**: finding 1の
修正（TraceLog内部の例外隔離）を行えば、本findingも同時に解消される。

### [MEDIUM-HIGH] finding 3: 主要な追跡対象プロパティで旧値が常にnullになる

- **`MainWindowViewModel.cs`199-242行目** `SelectedElementDeviceName`: `SetProperty`を経由せず238行目で
  直接`OnPropertyChanged()`を呼ぶ。T-039の主目的（`TraceLog.cs`コメントにも明記の通りT-036系デバイス名
  バグの再調査）に直結するプロパティだが、`_pendingOldValue`が設定されないため旧値は常にnull。
- **`MainWindowViewModel.cs`23-32行目** `Tool`: 同じくSetProperty意図的バイパス（record structの構造的
  等価性回避、T-016対策としてコメント明記）。`TraceLog.cs`15-18行目のコメントは「Tool/SelectedCell等は
  T-016/T-018の実バグ調査に直結した実績があるため除外しない」と最重要視しているにもかかわらず、
  Toolの旧値は捕捉できない。
- **`MainWindowViewModel.cs`398-416行目** `ReplaceDocument`: `Document`/`CurrentFilePath`/
  `_currentSheetIndex`/`_selectedCell`を直接代入し手動で`OnPropertyChanged`を発火するため、新規作成・
  開く操作時にこれら4プロパティすべてで旧値がnullになる。

**失敗シナリオ**: 忍者・隠密がツール切替やドキュメント読込直後の状態不整合バグをトレースログで追おうと
しても、まさにその変更経路の旧値情報が得られず、new値だけを見て推測するしかない（本来のT-039の価値を
半減させる）。

**所見**: 殿裁定「旧値記録は侍実装時判断に委任」の範囲内の設計選択であり、致命的ではないが、
「安価に取れる範囲」という制約の結果、最も見たいはずの3経路で取れていない点は再考の余地がある
（例えば`Tool`・`ReplaceDocument`内の代入も`SetProperty`経由に統一する、または呼び出し元で明示的に
旧値を`TraceLog`へ渡す等の追加対応）。

### [MEDIUM] finding 4: 環境変数の判定ロジックが緩い

- **`TraceLog.cs`35行目**: `env != "0"`。`"false"`/`"off"`/`"no"`等、無効化のつもりで設定した値が
  すべて有効化として扱われる。加えて全角の「０」（U+FF10）は半角"0"と一致しないため、全角文字混入時も
  誤って有効化される（`CLAUDE.md`が①②等の環境依存文字混入に注意を促す通り、実際に起こりうる事故）。

**失敗シナリオ**: 「無効化したつもりが実は有効なまま」という気付きにくい事故が起き、finding 1・2の
リスクに意図せず晒される。

### [LOW-MEDIUM] finding 5〜7（品質・ログ完全性）

5. **`TraceLog.cs`66行目** `Quote()`: ダブルクォート・改行のエスケープなし。値に`"`が入ると
   `key=value`形式が破綻し機械的パースが壊れる。値に改行が入ると「1行=1イベント」という設計前提
   （`Write`68行目、`\n`区切り）が崩れる。null と空文字列も区別できない（T-036の「空文字確定」ケースは
   まさにこの区別が必要な場面）。
6. **`App.xaml.cs`34-39行目**: `EventManager.RegisterClassHandler`の`handledEventsToo`省略（既定false）。
   将来何らかの経路でイベントが`Handled=true`にされると、その操作はトレースから静かに漏れる
   （「全操作を横断捕捉する」という触れ込みと矛盾しうる）。
7. **`ViewModelBase.cs`18行目**: `_pendingOldValue`代入のボクシングコストが`TraceLog.IsEnabled`に関わらず
   常に発生（前掲、家老観点2参照）。

### [LOW] finding 8〜12（cleanup・設計の細目、優先度低）

8. `TraceLog.Write`と既存`App.xaml.cs`の`OnDispatcherUnhandledException`（70-71行目）が同種の
   `%TEMP%`ログ書き込みを別々に再実装しており、タイムスタンプ書式（`O` vs `HH:mm:ss.fff`）・
   改行数（`\n\n` vs `\n`）が食い違う。共通ヘルパーへの統合余地。
9. `LogFocus`/`LogClick`がほぼ同一実装、`OnTraceFocusChanged`/`OnTraceButtonClick`の
   `ReferenceEquals(sender, e.OriginalSource)`ガードが重複。
10. `HighFrequencyProperties`（`TraceLog.cs`19行目）が`CanvasScale`のみのハードコードで、将来別の
    高頻度プロパティが増えた際に同じ発見→修正サイクルを繰り返す設計。
11. リフレクション`PropertyInfo`のキャッシュ無し、`Write`が毎回ファイルをopen/close（有効時限定の
    コストだが、`ReplaceDocument`の連鎖通知等バースト時は無視できない回数になりうる）。
12. `Ecad2.App`向けのテストプロジェクトが存在せず（`grep InternalsVisibleTo`も0件）、`TraceLog`の
    純粋ロジック（環境変数パース等）が原理的に単体テスト不可能。今後同種の分岐バグが混入しても
    CIでは検出できない。

---

## 総括・推奨アクション

最優先はfinding 1（例外安全性の欠如）。`TraceLog.Write`と`LogPropertyChanged`のリフレクション取得を
try/catchで隔離するという1点の修正で、finding 1・2の大半が構造的に解消される。次点でfinding 3
（主要プロパティの旧値null化）・finding 4（環境変数判定）は、殿の「信頼性担保」というご下命に照らし
修正を推奨する。finding 5〜7は品質改善として、finding 8〜12は任意の改善余地として報告する。

---

## 出典

- コミット562a0ad（`git show`で全文確認）
- `docs/ecad2-t039-design-comparison-onmitsu.md`（拙者の推奨形）
- `docs-notes/ecad2-t039-trace-log-field-feedback-ninja.md`（忍者現場意見）
- WPF公式ドキュメント（Microsoft Learn、WebSearchで確認: "Class handlers are invoked before instance
  handlers"）
- `code-review`スキル（10観点×並列finder + sweep、本隠密がAgent toolで直接orchestrate）
- `MainWindow.xaml.cs`66-70行目（T-036修正箇所、本隠密が直接確認）
