# P-104・P-109 原因調査（隠密）

日付: 2026-07-22
契機: 忍者の再現確認完了（`docs/ecad2-p104-p109-reproduction-check-ninja.md`、両件とも再現OK）を
受け、原因調査を家老采配（殿裁可）。

## P-104：シート削除後の機器表旧機器名残存

### 結論

**原因確定**。`SheetNavigationViewModel.DeleteCommand`（シート削除処理）に、既存の削除系操作
（要素単体削除・行削除）が持つ「機器表(DeviceTable)クリーンアップ」呼び出しが欠落している。
新規パターン候補として台帳記帳を提起する（下記「パターン再発照合」参照）。

### 根拠

`Document.Devices.ByName`（機器表の実データ）へのクリーンアップ呼び出しは、既存の削除系操作
すべてに存在する：

- `MainWindowViewModel.DeleteSelectedElement()`（2357-2386行）: 単体要素削除時、
  `RemoveDeviceIfUnreferenced(deviceName)`（2369行）を呼び、他要素から参照されなくなった
  機器名を`Document.Devices.ByName`から除去する。
- `MainWindowViewModel.DeleteRowAtCommand`（3153-3177行、`RowOps.DeleteRow`で「要素ごと削除」）:
  `CleanupRemovedDeviceNames(removed)`（3171行）を呼び、削除された要素群の機器名をまとめて
  クリーンアップする。専用コメント（2485-2488行）に「`DeleteSelectedElement`（単一削除）と
  同じ規則で機器表クリーンアップを行う」と明記。

これに対し`SheetNavigationViewModel.DeleteCommand`（159-198行）は、`_owner.Document.Sheets.
RemoveAt(index)`でシート丸ごと（その`sheet.Elements`全件を含む）を削除するにもかかわらず、
`RemoveDeviceIfUnreferenced`/`CleanupRemovedDeviceNames`のいずれも呼んでいない。DRC結果
クリア（186-190行）・欠番警告（191-192行）等、他の後始末処理は行っているが、機器表への
言及は無い。

### 失敗シナリオ（忍者再現内容と一致）

1. シートAに要素を配置し機器名を設定（`Document.Devices.ByName`にエントリ登録）。
2. シートAを削除（`sheet.Elements`はシートと共に消えるが、`Document.Devices.ByName`の
   エントリは誰も除去しない）。
3. 機器表（`DeviceTableViewModel`、`Document.Devices.ByName`から構築）に、もう存在しない
   シートの機器名がゴーストとして残存する。

### パターン再発照合

`docs-notes/pattern-recurrence-log.md`と照合した。完全一致する既存パターンは無いが、
**PR-05「状態リセット処理の横展開漏れ（Document/Sheet構成変更時）」**の親戚筋にあたる
（PR-05は「文書・シート構成を差し替える処理を新設する際、既存の同種処理が担う状態クリア責務
への追従が漏れる」型で、対象責務としてUndoManager/OutputPanel/SelectedSheet通知/SelectedCell
クランプが挙げられているが、「機器表クリーンアップ」は含まれていない）。また、PR-07の説明文
自体が「機器表クリーンアップ」を複製されやすい共通ロジックの一例として挙げており、実際に
`RemoveDeviceIfUnreferenced`/`CleanupRemovedDeviceNames`という2つの薄いラッパーに分散している
構造とも符合する。

新規パターン候補として提起する：「要素をまとめて削除する操作（シート削除等）が、個別要素削除・
行削除で確立済みの機器表クリーンアップ責務を継承しない」型。家老の判断で台帳記帳の要否・
既存PR-05への統合可否を検討されたい。

## P-109：シート改名（1番目限定）で選択色消失

### 結論

**構造は特定できたが「1番目限定」の具体的機序までは静的読解で完全特定できず**。UIA固有の
事象ではなく実装（アプリロジック）に起因する可能性が高いという判断はできる。1番目限定という
非対称性の根本メカニズムは動的タイミング依存の領域と考えられ、実機での追加検証（忍者領分）が
必要と考える。

### 特定できた構造

`SheetNavigationViewModel.RenameCommand`（207-233行）は、シート名変更をListBoxへ反映させる
ため、意図的に以下の手順を踏む（207-206行のコメントに明記）：

```csharp
sheet.Name = trimmed;
...
Sheets.RemoveAt(index);
Sheets.Insert(index, sheet);      // 同じ位置に同じ参照を戻す
_dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => RefreshSelectedSheet(sheet));
```

理由（既存コメント）：`Sheet`モデルは永続化対象のため`INotifyPropertyChanged`を実装しておらず、
`sheet.Name`への直接代入だけではListBoxの表示（`DisplayMemberPath="Name"`）に反映されない。
同一参照での`ObservableCollection`置換も`ItemContainerGenerator`が「DataContext自体は変わって
いない」と判定し再評価しないため反映されない（T-026実機確認で発見済みの既知制約）。この制約を
`RemoveAt`+`Insert`でコンテナ自体を強制的に再生成させることで回避している。

WPF一次ソース（`dotnet/wpf` `Selector.cs`）を確認したところ、`RemoveAt`（`NotifyCollectionChanged
Action.Remove`）が発火すると`RemoveFromSelection`（1255-1281行）が呼ばれ、削除されたアイテムが
選択中であれば選択リストから除去される。選択の再設定はコンテナの`IsSelected`起点の自動処理には
依存せず、`RefreshSelectedSheet`が発火する`PropertyChanged`通知をバインディングエンジンが拾い、
`SelectedSheet`のgetterを再評価して`Selector.SelectedItem`へ反映する、という非同期な設計に
なっている——**「選択解除→(遅延した)選択再設定」という、瞬間的に選択が失われる区間を作る構造**
である点は明確に特定できた。

`AddCommand`（104-156行）にも同型の`_dispatcher.BeginInvoke(ContextIdle, ...)`パターンがあり、
コメント（138-141行）に「`ObservableCollection`へのAdd直後はListBoxがまだ新しいアイテムのUI
要素を生成し終えていないため、同期的に設定すると選択ハイライトが追従しない（T-026実機確認で
発見）」とある。つまり「コンテナ生成完了を待つための遅延実行」という設計思想自体は、T-026時点
から既知の制約への対処として確立している。

### UIA固有か否かの切り分け

`RenameCommand`本体（ボタンクリック→ダイアログ確定→上記コードパス実行）は、UI Automation
経由のクリック（`SelectionItemPattern.Select()`/`InvokePattern.Invoke()`）でも、殿の物理
クリックでも**全く同一のコードパスを通る**——`RemoveAt`+`Insert`+`BeginInvoke(ContextIdle)`
という処理自体は入力手段に依存しない。したがって、**選択消失という現象の根本原因はUIA固有の
バグではなく、アプリ側の実装構造（コンテナ再生成方式）に起因する可能性が高い**と判断できる。

ただし、UI Automationでの検証は`SelectionItemPattern.Select()`という即時同期的なAPI呼び出しで
`IsSelected`を読み取るのに対し、物理クリックでは殿の目に映るまでに（人間の知覚を含め）追加の
時間経過が生じる。もし本件の真因が「`ContextIdle`（低優先度）で遅延実行される選択再設定が、
UIAの読み取りタイミングにはまだ間に合っていないだけ」という**タイミングの問題**であれば、
物理クリック後に少し待ってから目視すれば選択色が正しく表示されている可能性も否定できない
（これは静的読解だけでは判定不能、実機での時間経過込みの確認が必要）。

### 「1番目限定」の機序が特定できなかった理由

`Selector.cs`のAddイベント処理（1103-1108行）に「挿入位置(`NewStartingIndex`)が0の場合のみ
`ResetSelectedItemsAlgorithm()`を呼ぶ」という**インデックス0特有の分岐**を発見したが、この
メソッドの中身（1944-1950行）は`_selectedItems`が内部的に使うハッシュコード最適化アルゴリズム
の選定に過ぎず、選択状態そのものを変更する処理ではないと確認した。このため、この分岐だけでは
「なぜ1番目のシート改名時のみ選択色が消えるか」を直接説明できない。

`ItemContainerGenerator`によるコンテナの生成・再利用順序（1番目要素のコンテナ再利用パターンが
2番目以降と異なる可能性）や、`ContextIdle`実行タイミングとレイアウトパスの相対順序など、
更に深い一次ソース精読（`ItemContainerGenerator.cs`・`VirtualizingStackPanel.cs`等）で機序を
追うことは可能だが、これは`docs-notes/roles/onmitsu.md`が言う「動的タイミングの謎」（一次
ソース精読だけでは確定できず実測が本質的に必要な領域）に踏み込むと判断し、費用対効果を鑑み
本調査ではここで区切る。

### 派生提案

対処の方向性としては、`RenameCommand`の`RemoveAt`+`Insert`方式そのものを見直す（例：
`Sheet`に軽量な変更通知機構を持たせる、またはListBoxItemの`ContentPresenter`側で明示的に
再バインドする等）ことで、そもそも選択解除区間を作らない設計にする案が考えられるが、これは
実装判断であり侍・家老の裁量に委ねる。本調査は原因究明の範囲に留める。

## 不明点

- P-109「1番目限定」の具体的機序（`ItemContainerGenerator`のコンテナ再利用パターンとの関連は
  未検証、動的タイミング依存の可能性が高い）。
- P-109の物理クリックでの再現有無（忍者領分、時間経過込みの確認が必要）。

## 派生提案の有無

- P-104: 新規パターン候補としての台帳記帳（PR-05関連）を提起（上記参照）。対処自体
  （シート削除時に`CleanupRemovedDeviceNames`相当の呼び出しを追加）は規模が小さいと見受けるが
  実装判断は侍・家老に委ねる。
- P-109: 対処案の方向性のみ示す（上記「派生提案」参照）、断定・実装判断はしない。

## 出典

- `docs/ecad2-p104-p109-reproduction-check-ninja.md`
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`2357-2386,2469-2495,3153-3177行
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`89-233行
- `src/Ecad2.App/MainWindow.xaml`1204-1211行
- `docs-notes/pattern-recurrence-log.md`（PR-05・PR-07）
- `dotnet/wpf` `Selector.cs`（`raw.githubusercontent.com/dotnet/wpf/main/.../Selector.cs`、
  1103-1281,1944-1950行）

---

## 追記（2026-07-22、忍者追加検証を受けたP-109対処案検討）

殿裁可を受け、忍者の追加検証（`docs/ecad2-p109-additional-verify-ninja.md`）で判明した正確な
条件（「1番目限定」ではなく「改名対象がリスト末尾でない場合に発生」、入力経路non依存、時間
経過での自然回復なし）を踏まえ、対処案を検討する。

### 機序のさらなる精読（末尾/非末尾差の手がかり）

`dotnet/wpf` `ItemsControl.cs`を追加取得し、コレクション変更イベントの処理順序を確認した：

```csharp
// this is called before the generator's change handler
private void OnItemCollectionChanged1(...) => AdjustItemInfoOverride(e);
// this is called after the generator's change handler
private void OnItemCollectionChanged2(...) { ...; OnItemsChanged(e); }
```

（286,291行のコメントより）処理順序は **(1)ItemInfoのインデックス調整 → (2)ItemContainer
Generatorによる実コンテナ生成・破棄 → (3)`Selector.OnItemsChanged`（選択解除処理含む）** の3段階。
`AdjustItemInfos`（`ItemsControl.cs`3682-3757行）のRemove処理は「削除位置と一致する
ItemInfoのIndexを-1にする」というロジックで、これ自体は末尾・非末尾で分岐しない。末尾/非末尾の
非対称性は、(2)の`ItemContainerGenerator`内部でのコンテナ生成・再利用パターンの違いに起因すると
推測されるが、完全な機序特定には`ItemContainerGenerator.cs`（数千行規模）のさらなる精読が必要で
あり、これは「動的な内部実装の深追い」の域に入ると判断し、本調査ではここで区切る（機序の完全
解明よりも対処案提示を優先、殿裁定の依頼内容に照らした判断）。

### 対処の方向性（3案）

いずれも根本原因＝「`Sheets.RemoveAt(index); Sheets.Insert(index, sheet);`という、コンテナ
強制再生成のための手法が、WPFの選択状態管理（`Selector`内部の`_selectedItems`/`ItemInfo`）と
衝突する」という診断に基づく。

**案A（根本対処、規模中、隠密推奨）**：`Sheet`モデル自体、または`SheetNavigationViewModel.
Sheets`の要素を、軽量な`INotifyPropertyChanged`実装を持つラッパー（例：`Name`プロパティの
getter/setterで`Sheet.Name`を仲介する専用ViewModelアイテム）に置き換える。これにより
`RemoveAt`+`Insert`という手法自体が不要になり、`Name`変更時に`OnPropertyChanged(nameof(Name))`
を発火するだけでListBoxの表示が更新される——選択状態には一切触れないため、末尾/非末尾を問わず
問題が構造的に発生しなくなる。既存コメント「`Sheet`は永続化対象のため`INotifyPropertyChanged`
を実装しない」という設計方針（`Device`等、他のCore層モデルも同様の方針）へは踏み込まず、
`SheetNavigationViewModel`層に閉じたラッパーを追加する形であれば、Core層のPOCO設計方針とも
衝突しない。ただし`ListBox.ItemsSource`のバインディング先変更に伴い、`SelectedSheet`の
型（`Sheet`のままか、ラッパー型経由にするか）等、関連コードへの影響範囲の精査が要る。

**案B（対症療法、規模小、確実性は実装後の実機確認が必要）**：`RemoveAt`+`Insert`は維持しつつ、
選択再設定を現状の「`RefreshSelectedSheet`経由のバインディング（間接的）」から、View側
コードビハインド（`MainWindow.xaml.cs`）で`SheetNavList.SelectedItem`を直接設定する（間接的
でない）方式へ変更する。バインディングエンジンを介さない分、`ItemInfo`不整合の影響を受けにくく
なる可能性があるが、これは推測であり、機序が完全特定できていない以上、実装後に末尾/非末尾両方の
実機確認が必須。

**案C（回避策、規模最小、対症療法）**：`RemoveAt`+`Insert`をそのまま維持し、改名確定直後に
選択中だったシートを一旦明示的に選択解除→再選択する、という「症状を追認したうえでの強制
再同期」。ただし`SelectedSheet`のsetter（28-45行）に`if (value is null) return;`という早期
returnがあり、null経由での強制再評価は現状の設計では通らないため、setter側の見直しも合わせて
必要になる。案A・Bと比べ、根本原因を放置したまま挙動を帳尻合わせする色合いが強い。

### 隠密としての所見

案Aが最も確実（選択状態管理という複雑なWPF内部機構と手を切る）だが、規模はB/Cより大きい。
案B/Cは規模が小さい分、実装後に末尾/非末尾双方の実機確認をもって初めて有効性を確認できる
（機序が完全特定できていないため）。いずれを採るかは実装規模とのバランスで家老・侍・殿の
判断に委ねる。

## 出典（追記分）

- `docs/ecad2-p109-additional-verify-ninja.md`
- `dotnet/wpf` `ItemsControl.cs`（286,291,3682-3757行）
