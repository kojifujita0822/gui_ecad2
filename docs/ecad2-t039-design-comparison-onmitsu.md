# T-039 操作トレースログ基盤 実装方式比較（隠密）

家老委任事項：案A（InputManager横断フック）vs 案B（クラスハンドラ＋主要setter常設ログ）の比較検討。
比較軸には忍者の現場意見（`docs-notes/ecad2-t039-trace-log-field-feedback-ninja.md`）への適合度を含める。
コードベース調査はExploreエージェントへ委譲した事実確認結果に基づく。事実と推測は明示的に峻別する。

---

## 0. 評価軸（忍者の現場意見より）

優先度順（`docs-notes/ecad2-t039-trace-log-field-feedback-ninja.md` 23-38行目）:

1. フォーカス遷移（`LostFocus`/`LostKeyboardFocus`の発火有無とタイミング）
2. Binding/状態更新（`PropertyChanged`発火、`UpdateSource`タイミング）
3. Tool/SelectedCell等の主要状態遷移
4. コマンド実行（Clickハンドラ発火の有無）
5. 生のキー/マウス入力（どのウィンドウ/要素が受けたか）

非機能要件（同ファイル40-62行目）: 既定OFF+フラグ起動、セッション区切り必須、高頻度イベント除外、
key=value形式、`%TEMP%\ecad2-diag.log`と同じ置き場所。

---

## 1. 事実確認（コードベース）

出典: Exploreエージェントによるgrep/read調査（本隠密が委譲・査読済み）。

### 1-1. ViewModelの変更通知は単一集約点に収束している（事実）

`C:\ECAD2\src\Ecad2.App\ViewModels\ViewModelBase.cs`（全21行、本隠密が直接確認）:
```csharp
protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
{
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
}

protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
```
- `OnPropertyChanged`（18-19行目）が通知発火の唯一の集約点。`MainWindowViewModel`・`SheetNavigationViewModel`・
  `PartPaletteViewModel`・`DeviceTableViewModel`・`OutputPanelViewModel`の5クラスがこれを継承する（本隠密が
  `grep -l "ViewModelBase"`で再確認済み）。
- ただしsetterの実装パターン自体は不統一（少なくとも5種類混在: 単純`SetProperty`／`SetProperty`+カスケード
  ／`SetProperty`意図的バイパス（`Tool`, `MainWindowViewModel.cs` 23-32行目）／完全カスタム
  （`SelectedElementDeviceName`, 199-242行目）／自動プロパティ+呼び出し元手動発火（`Document`, `CurrentFilePath`,
  56・60行目））。**しかしいずれのパターンも最終的に`OnPropertyChanged`を呼ぶ点は共通**（呼ばなければ
  WPFバインディングが更新されず既存機能が壊れるため、これは設計上の不変条件）。
- 例外: `PartSelectionEntryViewModel`は`ViewModelBase`非継承（`INotifyPropertyChanged`不実装、表示専用の
  読み取り専用ラッパー）。Model層（`Ecad2.Core`）にも`INotifyPropertyChanged`実装クラスは存在しない
  （ViewModel層とModel層の通知は完全分離）。

### 1-2. フォーカス関連イベントの配線は個別・横断機構は未使用（事実）

- 配線されているのは`MainWindow.xaml`329行目`DeviceNameBox`の`LostKeyboardFocus`1箇所のみ（XAML属性による
  個別配線、ハンドラ実体は`MainWindow.xaml.cs`66-70行目）。
- `GotFocus`/`GotKeyboardFocus`/通常`LostFocus`の配線は0件。
- `EventManager.RegisterClassHandler`はプロジェクト全体で**未使用**（grep 0件）。

### 1-3. FocusScopeは`CanvasArea`の1箇所のみ（事実）

`MainWindow.xaml`265行目`<ScrollViewer x:Name="CanvasArea" ... FocusManager.IsFocusScope="True">`が唯一。
T-036観点6の根本原因（`docs/ecad2-t036-observation6-onmitsu.md`）となったFocusScope跨ぎ問題の当該箇所。

### 1-4. Clickハンドラはコードビハインド直接配線が主体（事実）

コードビハインド`Click=`ハンドラ19件（`MainWindow.xaml`17件＋ダイアログ2件）に対し、Commandバインディングは
3件のみ。`EventManager.RegisterClassHandler`は同じく未使用。

### 1-5. `System.Windows.Input.InputManager`は現状未使用（事実）

`InputManager`/`PreNotifyInput`/`PostNotifyInput`/`PreProcessInput`いずれもgrep 0件。案Aは既存パターンの
延長ではなく、新規に導入する仕組みとなる。

### 1-6. ロギング基盤は未導入、自前実装のみ（事実）

Serilog等のライブラリは3プロジェクトいずれの`.csproj`にも存在しない。既存の自前実装は
`App.xaml.cs`20-26行目の`File.AppendAllText`によるクラッシュログのみ。T-036で使われた
`%TEMP%\ecad2-diag.log`形式の診断ログは、原因確定後に本修正へ差し替えられ**現在のソースツリーには
残っていない**（`git status`・`grep DiagLog`ともに0件、本隠密が直接確認）。運用ルール通り一時ログは
除去済みという扱いで矛盾なし。

---

## 2. 案A（InputManager横断フック）の評価

**仕組み**: `InputManager.Current.PreNotifyInput`/`PostNotifyInput`へ1箇所フックし、WPFが処理する
全マウス/キーボード入力イベントをアプリ全体で横断的に捕捉する。

**強み**:
- 真に単一の登録点で、生入力（優先度5）の「どのウィンドウ/要素が届いたか」を横断的に拾える。
  モーダル罠（`Send-Ecad2Keys`が届かない事象）の切り分けに直結する。

**弱み（事実＋推測）**:
- **事実**: ViewModelの`PropertyChanged`発火（優先度2）や`Tool`/`SelectedCell`等の業務状態遷移
  （優先度3）は、WPFの入力パイプラインの外側（ViewModel層のC#プロパティ）で起きる。`InputManager`は
  入力イベントの処理経路であり、ViewModelの状態変化を観測する仕組みを内包しない。案Aを採用しても、
  優先度2・3を拾うには結局ViewModel側に別途フックが必要になり、案Aだけでは要求の上位2項目を
  満たせない。
- **推測（要検証、断定しない）**: フォーカス遷移（優先度1）が`InputManager`経由でどこまで観測できるかは
  不明瞭。Tabキー等キーボード操作起因のフォーカス変更は入力処理の一部として間接的に伴う可能性はあるが、
  `GotKeyboardFocus`/`LostKeyboardFocus`という名前付きイベントとして明示的に得られるわけではなく、
  生の`InputEventArgs`から間接的に推定するロジックが別途必要になると考えられる。本コードベースでの
  実地検証（本隠密は静的解析のみで実機検証権限を持たない）は行っていないため、この点は「不明」として
  扱う。
- **事実**: 本コードベースでは`InputManager`の使用実績が皆無（1-5参照）であり、新規の仕組みを
  ゼロから導入することになる。T-036で実績のある「特定箇所への診断ログ差し込み」路線とは
  連続性がない。
- 高頻度の生入力（マウス移動等）をフィルタする追加ロジックが必須（忍者の(3)「あっても困るもの」に
  該当するノイズ回避のため）。

---

## 3. 案B（クラスハンドラ＋主要setter常設ログ）の評価

**仕組み**: (a) `ViewModelBase.OnPropertyChanged`（1-1参照）への1行フック、(b)
`EventManager.RegisterClassHandler`によるフォーカス関連イベント（`GotKeyboardFocus`/
`LostKeyboardFocus`）の横断捕捉、(c) 同じく`ButtonBase.ClickEvent`の横断捕捉。

**強み（事実に基づく）**:
- **優先度1（フォーカス遷移）**: `EventManager.RegisterClassHandler(typeof(UIElement), UIElement.GotKeyboardFocusEvent, ...)`
  等をApp起動時に1箇所登録すれば、`DeviceNameBox`に限らずアプリ全体のフォーカス遷移を横断的に
  捕捉できる。クラスハンドラはルーテッドイベントの配線であり、FocusScope（1-3）の境界に関わらず
  発火する（FocusScopeは論理フォーカスの記憶先を分けるだけで、ルーテッドイベント自体の発火・
  バブリングを妨げない）。現状1箇所のみの個別配線（1-2）を、横断的な仕組みへ置き換える形になる。
- **優先度2・3（Binding/状態更新、Tool/SelectedCell遷移）**: 1-1で確認した通り、setterの実装が
  5パターン混在していても最終的に必ず`OnPropertyChanged`を経由する（経由しなければバインディングが
  壊れるため）。よって`ViewModelBase.OnPropertyChanged`1箇所へのログ追加だけで、`ViewModelBase`
  継承の全クラス・全プロパティ変更を将来分も含めて自動的に捕捉できる。「主要setterへ個別に
  ログを仕込む」という文字通りの案Bよりも保守負担が小さく、新規プロパティ追加時のログ入れ忘れ
  リスクも構造的に排除できる（この実装上の工夫を案Bの推奨形として提案する）。
- **優先度4（Clickハンドラ発火）**: `ButtonBase.ClickEvent`へのクラスハンドラ1件で、コードビハインド
  直接配線19件・Commandバインディング3件のいずれも横断的に捕捉できる（`Click`ルーテッドイベントは
  配線方式によらず必ず発火するため）。個別ハンドラへ19箇所手を入れるより保守負担が小さい。
- **実績との連続性**: T-036観点6で実際に効果を上げた「特定箇所への一時診断ログ」路線
  （`docs-notes/ecad2-t038-uia-diagnostic-handoff-draft-ninja.md`、標準ログ形式は本方式Bの常設版と
  親和性が高い）の延長線上にあり、侍にとって実装イメージが掴みやすい。

**弱み**:
- `PropertyChanged`イベントは変更後の値の通知のみで、旧値は引数に含まれない（`PropertyChangedEventArgs`は
  プロパティ名のみ保持、事実）。「old=... new=...」形式で旧値も残したい場合、ログ側でリフレクション
  経由の現在値取得（新値のみ）か、各setter呼び出し元での明示的な旧値記録が別途必要になる（優先度2の
  要件「変化前後の値」を完全に満たすには追加の一工夫が要る）。
- `ViewModelBase`非継承のクラス（`PartSelectionEntryViewModel`等）やModel層（`Ecad2.Core`）の状態変化は
  この一括フックの対象外（ただし現状これらは表示専用データであり、忍者の現場意見が挙げた優先度1-4の
  対象とは重ならないと考えられる＝推測）。
- 優先度5（生入力）は本方式の対象外。ただし忍者自身が「あっても困るもの」として最優先度が低いことを
  明言しており、対象外で問題ない（むしろ望ましい）。

---

## 4. 比較表

| 評価軸 | 案A（InputManager横断フック） | 案B（クラスハンドラ＋OnPropertyChanged一括フック） |
|---|---|---|
| 優先度1: フォーカス遷移 | △（推測混じり、間接推定が必要な可能性） | ◎（クラスハンドラで直接・明示的に捕捉、FocusScope跨ぎも影響なし） |
| 優先度2: Binding/状態更新 | ×（入力パイプラインの外側、別途フック必須） | ◎（`OnPropertyChanged`1箇所で全ViewModel横断捕捉） |
| 優先度3: Tool/SelectedCell遷移 | ×（同上） | ◎（同上、優先度2と同一機構） |
| 優先度4: Clickハンドラ発火 | △（入力イベントとしては拾えるが、どのハンドラに到達したかの紐付けは別途必要） | ◎（`ButtonBase.ClickEvent`クラスハンドラで一括捕捉） |
| 優先度5: 生入力 | ◎（本来の得意領域） | ×（対象外、ただし忍者要望的には対象外が望ましい） |
| 既存アーキテクチャとの親和性 | 低（新規導入、使用実績0件） | 高（既存の集約点・T-036実績の延長） |
| 保守負担（将来のプロパティ/ボタン追加時） | 中〜高（優先度2-4は別途対応が要り複雑化） | 低（集約点+クラスハンドラのため自動追従） |
| ノイズ制御のしやすさ | 要フィルタリング実装（生入力全般を捌く必要） | 対象範囲が元々絞られており容易 |

---

## 5. 推奨

**案B（クラスハンドラ＋主要setter常設ログ）を推奨する。** ただし「主要setter常設ログ」の実装は
文字通り個々のsetter本体へログを分散させるのではなく、**`ViewModelBase.OnPropertyChanged`
（`ViewModelBase.cs`18-19行目）への一括フック**として実装することを併せて提案する。この工夫により
案Bが元来抱えうる「setter実装パターンの不統一による記録漏れリスク」（Exploreエージェント調査の
懸念点）を構造的に回避できる。

根拠:
1. 忍者の現場意見（優先度1「フォーカス遷移」＞優先度2「Binding/状態更新」＞優先度3「Tool/
   SelectedCell遷移」）という最重要3項目に対し、案Aは構造的に対応できない（ViewModel層は入力
   パイプラインの外側にあるという事実）。案Bはこの3項目を低コストで直接満たす。
2. 案Aの唯一の強み（生入力の横断捕捉）は、忍者自身が最も優先度が低く「あっても困る」とまで
   述べている領域である。
3. 本コードベースには`InputManager`の使用実績が皆無で新規導入コストが伴う一方、案Bの構成要素
   （`ViewModelBase`の単一集約点、T-036での一時診断ログの実績）はすでに存在・実証済みであり、
   延長線上の実装として侍の実装負担・レビュー負担が小さい。

**残る検討事項（殿裁定または侍実装時の判断に委ねる）**:
- 旧値の記録要否（`PropertyChanged`は新値名のみ。忍者要望の「old=... new=...」形式を完全に満たすには
  追加実装が必要）。
- 既定OFF・フラグ起動・セッション区切り・key=value形式などの非機能要件は、案A/B いずれを採っても
  共通の設計事項であり、本比較の結論に影響しない。

---

## 出典

- `docs-notes/ecad2-t039-trace-log-field-feedback-ninja.md`（忍者現場意見、評価軸）
- `docs/ecad2-t036-observation6-onmitsu.md`（FocusScope構造・T-036実績、前回隠密調査）
- `docs-notes/ecad2-t038-uia-diagnostic-handoff-draft-ninja.md`（診断ログ標準形の実績）
- コードベース事実確認: Exploreエージェント委譲調査（`ViewModelBase.cs`は本隠密が直接再確認）
