# T-045 実装計画（侍起草）

> 2026-07-08 侍起草。入力＝隠密の構造調査`docs/ecad2-t045-structure-survey-onmitsu.md`。
> 本計画は**起草のみ**であり、実装はまだ行わない。起草完了後は殿裁可待ちで停止する
> （殿は現在離席中、帰還後に家老が上申する）。

---

## 0. 対象範囲の確認

T-045＝P-016（`SheetNavigationViewModel`のDispatcher直接依存分離）とP-025（配置前検証サービス
抽出、P-020/P-021/P-022/P-024を統合）の一括対応。加えて隠密の構造調査3節で示された「4種ドラッグ
状態機械の外枠共通化」（T-041往復で反復した所見Y型欠陥の再発防止）も含める。

**適用外**：隠密所見3.4節「高リスク・非推奨」とされた`UpdateDrag*`の実クランプロジック共通化、
および隠密所見4.2節「検討課題」の`SelectedCell`セッター・マウス経路（`ToGridPos`）の境界ガード化
——いずれも影響範囲が広い・費用対効果が悪いとの隠密判断を踏襲し、今回のスコープに含めない
（将来必要になれば別タスクとして再検討）。

---

## 1. 増分分割（A→B→C→D、依存の少なさ・回帰検知しやすさ順）

隠密所見4.2節の順序（独立性が高い順）をそのまま採用する。各増分は個別にコミットし、
増分間で`dotnet build && dotnet test`が通ることを都度確認してから次へ進む。

### 増分A：P-016（`SheetNavigationViewModel`のDispatcher抽象化）

**内容**：
- `IDispatcherService`インターフェースを新設（最小メンバ：
  `void BeginInvoke(DispatcherPriority priority, Action action)`）。配置場所は
  `Ecad2.App/ViewModels/`直下（既存`ViewModelBase.cs`と同階層）。
- 本番実装`WpfDispatcherService`：`System.Windows.Application.Current.Dispatcher.BeginInvoke`
  へ委譲。
- テスト用実装：`Action`を即時同期実行する`ImmediateDispatcherService`（`tests/`側に配置、
  `ViewModelTestBase.cs`と同じ階層）。
- `SheetNavigationViewModel`のコンストラクタに`IDispatcherService dispatcher`引数を追加し、
  `AddCommand`/`RenameCommand`内の直接`Application.Current.Dispatcher.BeginInvoke`呼び出しを
  `_dispatcher.BeginInvoke`へ置き換える。`MainWindow.xaml.cs:1353`（View/code-behind側）は
  隠密所見2.1で「P-016の射程外」と判断済みのため変更しない。
- `MainWindowViewModel`は`SheetNavigationViewModel`を生成する箇所（既存コンストラクタ内、
  `PartFolderStore`の2本立てパターンと同じ構造）で、本番用コンストラクタは内部で
  `new WpfDispatcherService()`を生成して渡す。T-042のP-019解決と同型の設計にするため、
  テスト注入用の追加コンストラクタ（`IDispatcherService`も引数に取る版）を新設し、
  既存の`MainWindowViewModel(PartFolderStore)`は新コンストラクタへ`WpfDispatcherService`を
  渡して委譲する形にする（後方互換、既存呼び出し元は無変更で済む）。
- 既存の4テスト（`AddCommand_MarksDirty`・`AddCommand_WithMainCircuitTrue_CreatesMainCircuitSheet`・
  `AddCommand_WithBlankName_FallsBackToAutoNumberedName`・`RenameCommand_MarksDirty`、
  `tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`）は、現状try/catchでNREを握り
  つぶし`MarkDirty`のみ検証している。`ImmediateDispatcherService`注入後はtry/catchが不要になり、
  かつ「`BeginInvoke`で渡したActionが実際に実行され`SelectedSheet`/`RefreshSelectedSheet`が
  正しく同期する」という、これまで検証できていなかった経路を新たにアサーションできる
  （隠密所見2.3で指摘された穴を埋める）。

**DoD**：
- `IDispatcherService`経由に統一され、`SheetNavigationViewModel`からWPF `Application`への
  直接依存が消える
- 既存4テストがtry/catch無しで「Dispatcher経由の同期処理が実際に動く」ことまで検証する形に
  書き直り、全部合格
- `dotnet build`0エラー0警告、既存回帰網（本増分開始時点の全件数）が減らないこと

**回帰網**：既存App/Coreテスト全件（起票時点179件）＋書き直した4テスト

**忍者検証観点**：シート追加（＋ボタン、名前・種別ダイアログ経由）・シートリネームの選択
ハイライト追従が実機で従来どおり機能すること（回帰なきことの確認、新機能ではない）

---

### 増分B：P-025 VM層（`PlaceElementAtSelectedCell`への検証関数追加、P-020含む）

隠密所見1.4「最小リスクの着手順」に従う：呼び出し元1箇所（VM層、既存テストで回帰検知可能）
から着手し、View層（UI Automation実機確認が要る）は増分Cへ分離する。

**内容**：
- 境界・占有チェックを束ねた新関数（例：`ValidatePlacement(GridPos pos, Sheet sheet)` 
  → 成否と理由を返す想定、具体形は増分着手時に確定）を`IsSelectedCellOccupied`
  （`MainWindowViewModel.cs:1284`）の隣に新設する。
- `PlaceElementAtSelectedCell`（同`:1292`）冒頭でこの関数を呼び、境界外・占有中なら早期
  returnする（P-021：占有再チェック欠如の解消、P-022/P-024：境界ガード欠如の解消）。
- P-020（種別マッピング未実装）：`ElementKind`→`DeviceClass`のマッピング関数を新設し、
  `SelectedElementDeviceName`セッター（`:1195`）と`PlaceElementAtSelectedCell`（`:1310`）の
  両箇所で`DeviceClass.Other`固定を置き換える。マッピング内容自体
  （`PartResolver.ComponentKind`が返す`ElementKind`→`ElementCatalog.IsContact`/`IsLoad`分類を
  参考に、`ContactNO/ContactNC`→`Relay`、`PushButtonNO/PushButtonNC`→`PushButton`、
  `SelectSwitch`→`SelectSwitch`、`Coil`→`Relay`、`Lamp`→`Lamp`、`Timer`系→`Timer`、
  `Counter`→`Counter`、`Terminal`→`Terminal`、未分類は`Other`、という対応が第一案）は
  UI表示に直結するため、**具体的な対応表は増分着手時に改めて家老・殿へ確認する**
  （予定範囲の厳守、UI/UX分岐は必ず殿へ諮る運用に従う）。
- `MainWindow.xaml:334`の種別列表示（enum直バインドで日本語変換なし）は隠密所見1.1の
  「副次所見」に留まり、DeviceClassの日本語表示化はP-020の主眼ではないため、本増分では
  対象外とし気づきとして`docs/proposed.md`に残すか家老判断を仰ぐ。

**DoD**：
- 新設した検証関数が境界外・占有中の両方を検知し、`PlaceElementAtSelectedCell`が該当時に
  何もしないこと
- `DeviceClass`が要素種別に応じて正しく設定されること（マッピング対応表は着手時に確定）
- `MainWindowViewModelTests.cs`（WPF起動不要、既存回帰網）で新規テストを追加し合格

**回帰網**：既存App/Coreテスト全件＋新規検証関数の単体テスト（境界値：グリッド端・負値・
占有済みセル）

**忍者検証観点**：配置操作全般（F5-F8・部品選択リスト・自作パーツ）の回帰確認、機器表の
「種別」列が要素に応じて正しく表示されること（P-020の可視化確認）

---

### 増分C：P-025 View層（`TryPlaceElement`等への同関数適用）

**内容**：
- `TryPlaceElement`（`MainWindow.xaml.cs:1320-1354`）が増分Bの検証関数を呼ぶよう変更し、
  UXの即時フィードバック（配置前に境界外・占有中を弾く）を強化する。
- `TryPlaceWireBreak`/`TryPlaceConnectionDot`（同ファイル、独自3段ゲート＋dedupを個別実装）
  への横展開は、隠密所見1.4「後続で横展開可能」との判断を踏まえ、本増分に含めるか増分外の
  将来課題とするかは着手時に家老へ確認する（範囲を広げすぎない判断）。
- **P-039残件**：`RowAtDip`（`LadderCanvas.cs:233-237`）はP-039で`ToRowBoundary`に置き換えた
  結果、呼び出し元が無くなった死にコード（grep確認済み）。View層を触る本増分で併せて除去する。
- 隠密所見の軽微掃除（コミットメッセージの4.6系RED言及漏れ＝報告精度の指摘、対応不要）は
  コード変更を伴わないため、本計画では扱わない。

**DoD**：
- `TryPlaceElement`が増分Bの検証関数経由で境界外・占有中を弾く
- `RowAtDip`削除後もビルド・既存テストに影響がないこと
- UI Automationで配置操作の回帰確認

**回帰網**：既存App/Coreテスト全件＋UI Automation実機確認（忍者）

**忍者検証観点**：必須（View層変更のため）。境界外セル・占有セルへの配置試行がUI上でも
正しく弾かれること、通常の配置操作に回帰がないこと

---

### 増分D：ドラッグ状態機械の外枠共通化（3種＋1種、外枠のみ）

隠密所見3.4節の判断を踏襲：`UpdateDrag*`の実クランプロジック（型ごとに本質的に異なる）は
**共通化しない**。共通化するのは「外枠パターン」（`ForceCancelDrag*IfAny`の骨格）のみ。

**内容**：
- `ForceCancelDrag*IfAny()`（Connector/WireBreak/FreeLine/ConnectionDotの4箇所、いずれも
  「nullチェック→Cancel呼び出し→OnPropertyChanged」の3行が文字通り同一）の共通化。
  隠密所見3.4節1.が挙げるdelegateベース案（`ForceCancelIfAny(Func<bool> isActive, Action cancel,
  Action notify)`）か、個別に残すかは可読性とのトレードオフのため、実装着手時に具体案を家老へ
  提示してから進める（今回は「共通化の方向性」のみ計画に含め、詳細設計は増分着手時とする）。
- `ConfirmDrag*`/`CancelDrag*`の骨格共通化（隠密所見3.4節2.、中リスク）は、スナップショット
  構造体の設計を要するため、増分Dの中でもさらに慎重な進め方（PoC的な1種のみでの試行→他3種へ
  展開の可否判断）とする。

**DoD**：
- 4種のドラッグ回帰テスト（増分開始時点の全ドラッグ関連テスト）が引き続き全合格
- 外枠共通化により「所見Y型」（ForceCancelが位置復元を伴わない）のような欠陥が構造的に
  再発しにくくなっていること（新規追加時に共通ヘルパー経由で自動的に復元が効く設計）

**回帰網**：既存の全ドラッグ関連テスト（起票時点でConnector/WireBreak/FreeLine/ConnectionDot
合わせて100件超）

**忍者検証観点**：4種（VerticalConnector・WireBreak・FreeLine・ConnectionDot）のドラッグ
移動・端点リサイズ・Esc/外的要因キャンセルの一通りの実機確認（回帰なきこと）

---

## 2. 専用ブランチ運用（殿指示）

### 2.1 ブランチ名案

`feature/t045-app-layer-refactor`（用途が一目で分かる命名、既存コミット履歴に他のfeature/*
ブランチが無いため衝突なし）。

### 2.2 切替タイミングと4役同期手順

4セッション（家老/侍/忍者/隠密）が同一の作業ツリー（`C:\ECAD2`）を共有しているため、
ブランチ切替は「誰かが作業中に足元が変わる」事故を避けるための同期が必須。

**mainからT-045ブランチへの切替時**：
1. 家老が全役（peerメッセージ）へ「これからT-045ブランチへ切り替える、作業を一時中断されたし」
   と通知する
2. 各役は`git status`で未コミットの変更が無いことを確認する。侍に変更が残っていれば、
   増分の区切りでコミットするか、区切りが悪ければ一旦`git stash`で退避する
3. 全役から「準備完了」の返信が揃ったことを家老が確認する
4. 侍が`git checkout -b feature/t045-app-layer-refactor`でブランチを作成・切替する
   （侍が実装担当のため、実際のcheckoutコマンドは侍が実行する）
5. 侍が全役へ「ブランチ切替完了、以後T-045の作業はこのブランチで」と通知する
6. 各役は自分の作業ディレクトリが同じ物理パスを共有していることを踏まえ、以後の
   `git status`確認等でブランチ名（`git branch`）を都度確認する習慣を徹底する

**増分完了・mainへのマージ時**（全増分A〜D完了後）：
1. 侍が最終増分の完了報告と共に「mainへマージしてよいか」を家老へ確認する
2. 家老が全役へ「これからmainへマージする、作業を一時中断されたし」と通知する
3. 各役から「準備完了」の返信が揃ったことを確認する
4. 侍が`git checkout main && git merge feature/t045-app-layer-refactor`
   （増分ごとの履歴を残すため、`--squash`は使わない）でマージする
5. 侍が全役へ「mainへ戻った、T-045クローズ」と通知する

**注意**：家老の指示にある「専用ブランチでの増分実施は殿裁可後」に従い、上記手順は
**殿裁可が下りてから**実行する。本計画書の起草段階ではブランチ切替は行わない。

### 2.3 途中経過のブランチ内コミット粒度

増分A〜Dそれぞれで最低1コミット（1増分1関心事、既存の「1コミット1関心事」ルールを
増分単位に適用）。増分内で往復修正が発生した場合は、T-041運用と同様に往復ごとに
コミットを分ける。

---

## 3. リスクと中断基準

T-041の教訓（修正往復が定型化しやすい——所見発見→修正→新たな所見発見→再修正、という
反復パターンが増分5・増分7で複数回発生した）を踏まえ、増分ごとに以下の中断基準を設ける。

- **各増分は独立して評価する**：増分Aで問題が起きても増分B以降の設計を変える必要はない
  （P-016とP-025は隠密所見4.1で「依存関係なし」と整理済み、外枠共通化＝増分Dも独立）
- **中断基準（いずれか1つでも該当したらmainへ戻る判断を家老へ上申する）**：
  1. 同一増分で隠密レビューの往復が3周を超えた場合（既存の`karo.md`が定める往復上限ゲートの
     考え方をT-045にも適用）
  2. 既存回帰テスト（本計画開始時点179件）が増分作業中に一時的にでも10件を超えて崩れ、
     原因特定に手間取っている場合
  3. 増分Bのマッピング対応表など、UI/UX判断を要する分岐で殿確認が取れず作業がブロックされ、
     かつ他増分への迂回でも解消しない場合
- **中断時の扱い**：当該増分のブランチ内コミットは残したまま、mainへは戻さず一旦
  `feature/t045-app-layer-refactor`ブランチ自体を凍結し、家老が殿へ状況を上申して再開・
  縮小・撤回の判断を仰ぐ（T-041のP-034往復4周目と同じ「殿上申」パターンを踏襲）

---

## 4. P-039残件・軽微指摘の割当（家老指定④）

| 項目 | 内容 | 割当先増分 | 理由 |
|---|---|---|---|
| `RowAtDip`死にコード除去 | P-039で`ToRowBoundary`に置き換え後、呼び出し元が消滅（grep確認済み） | 増分C | 同じ`LadderCanvas.cs`（View層）を触る増分でまとめて対応するのが効率的 |
| コミットメッセージの4.6系RED言及漏れ | 報告精度の指摘、コード変更を伴わない | 対応不要 | ドキュメント上の記録のみで実害なし。今回の計画には含めない |

---

## 5. 未確定事項（増分着手時に確認が必要）

- 増分Bの`ElementKind`→`DeviceClass`マッピング対応表の最終確定（本計画では第一案を提示のみ）
- 増分Cで`TryPlaceWireBreak`/`TryPlaceConnectionDot`への横展開を含めるか否か
- 増分Dの`ForceCancelDrag*IfAny`共通化の具体的な実装方式（delegateベース vs 個別維持）

いずれも「予定範囲の厳守」に従い、各増分の着手時に家老（必要なら殿）へ確認してから
実装する。

---

## 出典・参照

- `docs/ecad2-t045-structure-survey-onmitsu.md`（隠密の構造調査、本計画の一次入力）
- `docs/proposed.md`（P-016/P-020/P-021/P-022/P-024/P-025原案）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（配置系・ドラッグ系メソッド全般、コンストラクタ2本立てパターン`:1452-1457`）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（Dispatcher依存箇所）
- `tests/Ecad2.App.Tests/ViewModelTestBase.cs`（P-019/T-042の2本立てコンストラクタ先例）
- `src/Ecad2.Core/Model/PartResolver.cs`・`ElementCatalog.cs`・`Device.cs`（P-020マッピング材料）
- `src/Ecad2.App/Views/LadderCanvas.cs:233-237`（P-039残件`RowAtDip`）
- `docs-notes/roles/karo.md`（往復上限ゲート・殿上申パターンの既存運用）
