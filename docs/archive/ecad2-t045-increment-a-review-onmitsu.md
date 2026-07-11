# T-045増分A（P-016 Dispatcher直接依存分離）静的レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`eb3e9b0`（`feat(app): T-045増分A P-016 Dispatcher直接依存
> 分離`、差分範囲`a982b32..eb3e9b0`）。前回セッションで実施した最終レビューは口頭確認のみで
> docs収蔵前に全セッション再起動によりコンテキストが失われたため（引き継ぎメモ
> `docs-notes/handover-next-session.md`§2参照）、家老裁定によりコミットeb3e9b0を再調査し
> 出典を揃えて本文書として起草した。`code-review`スキル（8角度：line-by-line diff scan・
> removed-behavior auditor・cross-file tracer・Reuse・Simplification・Efficiency・Altitude・
> Conventions、各角度1エージェント×1-vote検証エージェント、計18エージェント並行）を高effortで
> 併用。実測検証（`dotnet test`、T-042パターンとの実コード比較）も併用した。

---

## 結論：**クリーン確定を維持（機能バグなし）。ただしテスト設計面でCONFIRMED4件・PLAUSIBLE6件の
所見あり、対応要否は家老の仕分けを仰ぐ**

5観点（①設計妥当性②挙動保存③セマンティクス差④テスト書き直し⑤179件regression）はいずれも
実測・出典付きで再確認でき、引き継ぎメモの結論要約との食い違いはない（＝「クリーン確定」保留
の閾値には該当しない）。`code-review`8角度18エージェントの独立調査でも**機能バグ（correctness
の実害）は1件も発見されなかった**（cross-file tracer・Reuse・Efficiency・Conventions角度は
いずれも0件）。

一方で、④「テスト書き直し＝後退なし」は文字どおり正しい（旧テストより検証範囲は広がった）が、
**新テストでも依然として検証できていない2つの盲点**が`code-review`のremoved-behavior角度で
CONFIRMED判定された（下記所見1・2）。これは「後退」ではなく「新規テストにも残る限界」である。
軽微指摘（テストコメント未記載）はこの盲点と直接関連するため、対応すれば副次的にリスク低減が
見込める。

---

## 5観点の検証結果

### ①設計妥当性＝T-042(P-019)と完全同型

T-042実装コミット`075b4bc`（`fix(app): T-042 - App層テストの実環境副作用解消`）で確立した
「無引数コンストラクタ（本番用、`CreateDefault()`委譲）→ 引数版コンストラクタ（テスト注入用）」
の2本立て委譲チェーンパターンを実測比較した。

- T-042時点（`075b4bc`差分）：
  ```csharp
  public MainWindowViewModel() : this(PartFolderStore.CreateDefault()) { }
  public MainWindowViewModel(PartFolderStore partFolderStore) { ... }
  ```
- T-045現在（`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1452-1472`）：
  ```csharp
  public MainWindowViewModel() : this(PartFolderStore.CreateDefault()) { }
  public MainWindowViewModel(PartFolderStore partFolderStore) : this(partFolderStore, new WpfDispatcherService()) { }
  public MainWindowViewModel(PartFolderStore partFolderStore, IDispatcherService dispatcherService) { ... }
  ```

T-042の2本立て委譲チェーンの末尾に、同じ形式でもう1段（IDispatcherService版）を追加した構造で
あり、既存パターンの単純な拡張として妥当。出典：`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1452-1472`、コミット`075b4bc`。

### ②挙動保存＝完全等価

本番実装`WpfDispatcherService.BeginInvoke`は`Application.Current.Dispatcher.BeginInvoke`への
単純委譲（`src/Ecad2.App/ViewModels/WpfDispatcherService.cs:11`）。呼び出し側
（`SheetNavigationViewModel.cs:100-102`,`151-153`）は旧実装

```csharp
System.Windows.Application.Current.Dispatcher.BeginInvoke(
    DispatcherPriority.ContextIdle, new Action(() => SelectedSheet = sheet));
```

から新実装

```csharp
_dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => SelectedSheet = sheet);
```

へ変わったが、本番経路では`_dispatcher`が`WpfDispatcherService`インスタンスのため、実行される
コードパスは`Application.Current.Dispatcher.BeginInvoke(priority, action)`で完全に同一。
出典：`src/Ecad2.App/ViewModels/WpfDispatcherService.cs:11`、`SheetNavigationViewModel.cs:100-102,151-153`。

### ③セマンティクス差＝機能バグなし（finder独立検証で確認）

`code-review`のcross-file tracer角度（呼び出し元・呼び出し先の追跡）・Reuse角度（重複実装の
有無）・Efficiency角度・Conventions角度はいずれも0件（本セッションのAgent実行結果、8角度18
エージェント並行実施）。line-by-line scan角度（Angle A）唯一の指摘（後述所見5）も
「新規バグ」ではなく既存T-042パターンの踏襲と判定された。BeginInvoke後続処理
（`SelectedSheet`設定・`RefreshSelectedSheet()`）はタイミング非依存で最終状態が同一という
従来結論を、新規実測でも覆す証拠は出なかった。

### ④テスト書き直し＝後退ではないが新テストにも2件の盲点が残る（要注意）

旧テスト（`git show eb3e9b0^:tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`）は
`try { ... } catch (NullReferenceException) { }`でDispatcher呼び出し自体を握りつぶし、
`IsDirty`のみ検証（`SelectedSheet`の値は一切未検証）。新テストは`ImmediateDispatcherService`
注入によりNREが起きなくなり、`SelectedSheet`の値・`PropertyChanged`発火まで検証範囲が拡大した
（`tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs:75,211-217`）。**この意味で「後退」は
していない**。

ただし`code-review`のremoved-behavior角度で、新テストにも次の2点がCONFIRMED判定された（詳細は
下記所見1・2）：
- `DispatcherPriority`の値そのものは検証されない（`ImmediateDispatcherService`がpriority引数を
  無視するため、意図的に選んだ`ContextIdle`が別の値に変わっても新テストは検出できない）
- `MarkDirty()`呼び出しが`BeginInvoke`の前（同期部分）にあるか中（非同期lambda内）にあるかを
  新テストは区別できない

これらは「T-045が退化させた」のではなく「単体テストの原理的限界＋テスト設計の詰めの甘さ」で
あり、旧テストでも同様に検出不能だった（旧テストはさらに検証範囲が狭かった）。

### ⑤179件regression維持＝実測確認

本セッションで`dotnet test src/Ecad2.sln`を実行し実測：

```
成功!   -失敗:     0、合格:    14、スキップ:     0、合計:    14 - Ecad2.Core.Tests.dll (net8.0)
成功!   -失敗:     0、合格:   165、スキップ:     0、合計:   165 - Ecad2.App.Tests.dll (net8.0)
```

Core14件＋App165件＝179件全合格。引き継ぎメモ・コミットメッセージの報告と一致。

なお、RED証明（`ViewModelTestBase.CreateViewModel()`を一時的に`WpfDispatcherService`注入へ戻し
書き直した4テストがNREでREDになることの実測）はコミット`eb3e9b0`メッセージに記載された侍の
実測報告であり、隠密は`src/tests`書き込み不可のため再実演はしていない（スコープ境界）。

---

## `code-review`スキル追加指摘（CONFIRMED4件／PLAUSIBLE6件／REFUTED0件）

いずれも**correctnessの実害（機能バグ）ではなく**、テストカバレッジの盲点・コードの簡潔性に
関する指摘。

### CONFIRMED（4件）

**所見1：`DispatcherPriority`が新テストで検証されない**
`tests/Ecad2.App.Tests/ImmediateDispatcherService.cs:11`が`priority`引数を完全に無視して
`action()`を即時同期実行するため、`SheetNavigationViewModel.cs:100-102,151-153`で意図的に選ばれた
`DispatcherPriority.ContextIdle`（T-026実機確認由来、ListBoxのUIコンテナ生成完了を待つための選択、
`SheetNavigationViewModel.cs:95-98`のコメント参照）が将来別の値に変更されても、
`AddCommand_MarksDirty`/`RenameCommand_MarksDirty`は通り続け検出できない。

**所見2：`MarkDirty()`が`BeginInvoke`前か中かを新テストで区別できない**
`SheetNavigationViewModel.cs:139-153`で`MarkDirty()`は`BeginInvoke`呼び出しより前（同期部分）に
あるが、`ImmediateDispatcherService`が即時同期実行するため、
`SheetNavigationViewModelTests.cs:213-217`の`Assert.True(vm.IsDirty)`は最終状態しか見ておらず、
将来`MarkDirty()`が`BeginInvoke`のlambda内へ移動されても検出できない。本番では`IsDirty`更新が
次のContextIdleまで遅延することになり、`MainWindow.xaml.cs:261`の未保存確認ダイアログ等
同期的に`IsDirty`を読む経路が影響を受けうる。

**所見3：`DispatcherPriority`引数のYAGNI**
`IDispatcherService.BeginInvoke`（`IDispatcherService.cs:8`）はpriority引数を取るが、実呼び出しは
`SheetNavigationViewModel.cs:101,152`の2箇所とも`ContextIdle`固定（他の呼び出し箇所は皆無、grep
確認済み）。選択の余地がない導出可能な引数。

**所見4：コメントの一字一句重複**
`SheetNavigationViewModel.cs:99`と`:150`に「T-045(P-016対応): IDispatcherService経由にし、
WPF Applicationへの直接依存を除去。」という同一コメントが複製されている。

### PLAUSIBLE（6件、対応は任意判断）

**所見5：`dispatcherService`のnullチェックなし**（`MainWindowViewModel.cs:1463`）
T-042の`PartFolderStore`引数版コンストラクタ（`075b4bc`）も同様に無検証で委譲しており、App層
全体でコンストラクタ引数へのnullガードは1件もない（grep確認済み）。新規バグではなく既存慣習の
踏襲。

**所見6：`BeginInvoke`ラッパー削除リファクタを新テストで検出できない**（`SheetNavigationViewModelTests.cs:75`）
旧テストはこの経路を一切検証していなかったため「後退」ではなく新規カバレッジ追加（④参照）。

**所見7：本番`WpfDispatcherService`パスがテストから一度も実行されない**（`WpfDispatcherService.cs:11`）
`docs/archive/ecad2-t045-implementation-plan-samurai.md`で忍者実機検証がこの1行アダプタの担保手段として
明示されており、意図的な設計判断（WPF Application起動前提のためユニットテスト不可）。

**所見8：`MainWindowViewModel`コンストラクタ3本立ての委譲チェーンの複雑さ**（`MainWindowViewModel.cs:1452-1463`）
オプショナル引数1本への集約も技術的に可能（呼び出し元は無引数版・フル引数版の2箇所のみ、
grep確認済み）だが、コメント（`:1460-1462`）でT-042パターンとの一貫性を意図した設計判断が
明記されている。

**所見9：テストメソッド内コメントの重複**（`SheetNavigationViewModelTests.cs:73-74,214-216`）
クラス冒頭のXMLコメント（`:6-13`）と同趣旨の説明が各テストメソッド内にも繰り返されている。

**所見10：直接Dispatcher依存の再発防止機構（アーキテクチャテスト等）が未整備**（`IDispatcherService.cs:1`）
今回のコミットのスコープ（SheetNavigationViewModelの既知バグ解消）を超える将来課題。

---

## 軽微指摘（引き継ぎメモ記載の1件）

`tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs:6-13`のクラスXMLコメントに、
「本番（`WpfDispatcherService`＝非同期`BeginInvoke`）とテスト（`ImmediateDispatcherService`＝
同期即時実行）でタイミング特性が異なる」旨の明記がない。**この軽微指摘への対応（コメント追記）
は、上記CONFIRMED所見1・2が示す盲点を将来の保守者が認識する助けになるため、対応する場合は
所見1・2と併せて記載すると効果的**。

---

## 家老への確認事項

1. CONFIRMED4件・PLAUSIBLE6件のうち、増分Aクローズ前に対応すべきものはあるか、増分B以降へ
   先送りしてよいか（いずれもcorrectnessの実害ではなくテスト設計・簡潔性の指摘のため、増分A
   自体のクリーン確定は維持してよいと判断）
2. 軽微指摘（テストコメント）の扱い
3. 所見10（アーキテクチャテスト整備）はT-045のスコープ外の将来課題として`docs/proposed.md`
   経由で提案してよいか
