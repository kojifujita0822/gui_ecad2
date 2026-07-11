# T-039 操作トレースログ基盤 再々レビュー（隠密・静的レビュー、往復2周目）

対象: コミット`beeabc2`（`fix(T-039): 隠密再レビューCONFIRMED4件を修正(往復2周目)`）。
変更ファイル: `src/Ecad2.App/App.xaml.cs`・`src/Ecad2.App/Diagnostics/TraceLog.cs`・
`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`。

前回再レビュー原本（`docs/archive/ecad2-t039-re-review-onmitsu.md`）のCONFIRMED4件を追跡表として
突合、＋`code-review`スキル（highレベル、8角度×並列finder→1-vote verify）を併用。

---

## 総評

**CONFIRMED4件は、いずれも正しく解消・退行なし（事実）。** 前回指摘した通りの修正が
的確に入っており、往復2周目の対象範囲は「クリーン」と判定してよい。

**ただし、code-reviewスキルの独立探索により、今回の対象範囲の外側で2種類の新規発見があった。**
いずれも「今回のCONFIRMED4件の再発」ではなく、**同じ問題のクラス（全角文字混入・SetProperty
バイパス時の旧値ロスト）が、今回の修正対象外の箇所に元々存在していた**というもの。3周目の
往復対象（=侍の今回修正のやり直し）には当たらないが、家老へ「気づき」として報告する。

---

## 追跡表: CONFIRMED4件の解消確認

| # | 前回CONFIRMED | 判定 | 根拠 |
|---|---|---|---|
| 1 | SaveToFileの旧値null化再発 | **解消** | `MainWindowViewModel.cs`368-374行目、`string? oldFilePath = CurrentFilePath;`を代入前にキャプチャし2引数版`OnPropertyChanged(nameof(CurrentFilePath), oldFilePath)`へ統一。`CurrentFilePath`は自動プロパティで二重発火の懸念もなし |
| 2 | crash.log書込にtry/catch無し | **解消** | `App.xaml.cs`88-101行目、`File.AppendAllText`をtry/catchで隔離。`MessageBox.Show`・`e.Handled=true`はtry/catchの外側のまま維持され、ログ書込失敗の有無に関わらず必ずユーザー通知される（前回CONFIRMEDの核心＝「MessageBox表示自体が道連れになる」問題は完全に解消） |
| 3 | 環境変数判定（全角「０」・空白トリム） | **解消** | `TraceLog.cs`42-63行目、Trim→`NormalizeFullWidthDigits`（全角数字正規化）→長さ判定→無効化リスト照合の順に是正。空白のみの値・全角「０」いずれも実装確認・旧新ロジック比較で退行なしと確認 |
| 4 | ボクシング回避が新設オーバーロード経由で無効 | **解消** | `MainWindowViewModel.cs`31・411・412行目、`TraceLog.IsEnabled ? _field : null`で短絡評価。TraceLog無効時は値型フィールドに触れずボクシングを回避 |

---

## 新規発見（今回の対象範囲外・気づき）

### [CONFIRMED] 環境変数判定: 全角ラテン文字（ｆａｌｓｅ/ｏｆｆ/ｎｏ等）が正規化されない
- **`TraceLog.cs`54-63行目** `NormalizeFullWidthDigits`は全角**数字**(U+FF10-FF19)のみを対象とし、
  全角**ラテン文字**(U+FF21-FF5A、`ｆａｌｓｅ`/`ｏｆｆ`/`ｎｏ`等)は無変換のまま通過する。
  `DisableEnvValues`（21-26行目）は半角ラテン文字のみのため、全角で無効化語を設定しても
  一致せず、意図に反して`viaEnv=true`（誤って有効化）になる。
- **失敗シナリオ**: 日本語IME入力時に`ECAD2_TRACE_LOG=ｏｆｆ`のように全角で設定してしまう
  （今回まさに全角「０」で発生した事故と同一クラスの入力ミス）と、無効化のつもりが有効化
  されたままになる。
- **実機検証済み（.NET 8）**: `"ｆａｌｓｅ"`/`"ｏｆｆ"`/`"ｎｏ"`いずれも現行`NormalizeFullWidthDigits`
  では無変換、`DisableEnvValues.Contains`はfalseのままで`viaEnv=true`と確認。
- **より根本的な解決策（所見）**: .NET標準の`string.Normalize(NormalizationForm.FormKC)`を
  使えば、全角数字・全角ラテン文字の両方を1回の呼び出しで半角化できる（実機で
  `"ｆａｌｓｅ".Normalize(FormKC)=="false"`・`"０".Normalize(FormKC)=="0"`とも確認済み）。
  自前の`NormalizeFullWidthDigits`より狭い範囲しかカバーしておらず、車輪の再発明かつ
  再発明した車輪の方が本家（標準API）より機能が狭い状態。
- **verdict: CONFIRMED**（実装確認・実機検証の両方で裏付け）

### [CONFIRMED] 兄弟ファイルにfinding3と同型の旧値ロストが2件現存
今回の対象（`MainWindowViewModel.cs`）内部は全31箇所の`OnPropertyChanged`呼び出しを突合し
退行なしと確認したが、**同じ`ViewModelBase`を継承する別ファイル**に、前回finding3
（SetProperty非経由の直接代入＋1引数版呼び出しで旧値が常にnullになる）と全く同型のパターンが
未修正のまま残っていた。

- **`DeviceTableViewModel.cs`26-30行目** `Refresh()`が`Devices`（参照型、`IReadOnlyList<Device>`）
  へ直接代入した後、1引数版`OnPropertyChanged(nameof(Devices))`で通知。旧一覧を安価に
  キャプチャできるにもかかわらず捨てている。
- **`SheetNavigationViewModel.cs`32-38行目** `SelectedSheet`のsetterが`_owner.CurrentSheetIndex`
  書き換え後、1引数版`OnPropertyChanged()`で通知。書き換え前にgetter経由で安価に取れる
  旧選択シートを捨てている。
- **失敗シナリオ**: TraceLog有効時、機器表の更新やシート切替をトレースログで追跡しようとしても
  `old="null"`しか記録されず、変更前後の比較ができない。T-036系（機器表の孤立残存）・
  T-018/T-026系（シート切替回帰）の再調査時にまさに欲しい情報が欠落する。
- **スコープについて（所見）**: これは今回のコミット（beeabc2）が生んだ退行ではなく、
  T-039の初回実装（562a0ad）時点から一度も対象に入っていなかった箇所。前回・前々回どちらの
  隠密レビューも`App.xaml.cs`/`TraceLog.cs`/`MainWindowViewModel.cs`のみを対象としており、
  この2ファイルは調査範囲に含めていなかった（隠密の見落としというより、レビュー対象の
  スコープ外だった）。
- **verdict: CONFIRMED**（実装確認済み、両ファイルとも`ViewModelBase`継承で2引数版
  オーバーロードは利用可能）

---

## 所見（PLAUSIBLE、優先度低、今回の対象外）

- **crash.log書込失敗時の開発者向け代替診断経路が無い**: 今回のCONFIRMED采配（MessageBox表示が
  道連れになる問題）は完全に解消済み。ただし`File.AppendAllText`失敗時、Debug.WriteLine等の
  代替記録が無いため、`%TEMP%`書込不可環境では開発者向けの永続ログが一切残らない。前回
  TraceLog側で議論済みのトレードオフ（例外握りつぶし＝観測可能性の喪失）と同種・同レベル。
- **ボクシング回避ガードが4箇所に分散**: `ViewModelBase.SetProperty`・`Tool`・`ReplaceDocument`
  内2箇所で同一ルールが個別実装。ジェネリック版`OnPropertyChanged<T>(string, T) where T : struct`
  という一般化案を実機検証したが、`GridPos?`のようなnullable structは`where T : struct`制約に
  束縛できず`(string, object?)`版へ静かにフォールバックする（C#の既知の制約）ため、「1つ
  用意すれば解決」は不正確と判明。現状の三項演算子ガードで許容範囲。

---

## 結論・家老への提案

**往復2周目の対象（CONFIRMED4件）はクリーン。3周目（殿上申）は不要と考える。**

新規発見の2件（全角ラテン文字・兄弟ファイル2件）は、今回のT-039スコープの延長で直すか、
別タスク化するかは家老の裁量に委ねる。忍者実機確認へ進めることを妨げる性質の欠陥ではない
（既定OFFのオプトイン診断機能であり、影響は「トレース情報の一部欠落」「誤って有効化」に
留まり、アプリ本体の機能には波及しない）。

---

## 出典
- コミット`beeabc2`（`git show`・`git diff beeabc2~1..beeabc2`で全文確認）
- 前回再レビュー`docs/archive/ecad2-t039-re-review-onmitsu.md`（CONFIRMED4件原本）
- `code-review`スキル（highレベル、8角度×並列finder→1-vote verify）
- .NET 8実機での`NormalizeFullWidthDigits`・`string.Normalize(FormKC)`比較検証（verify時に
  該当エージェントが使い捨てプロジェクトで検証）
- `DeviceTableViewModel.cs`26-30行目・`SheetNavigationViewModel.cs`32-38行目（本隠密が
  直接確認・verifyエージェントが実装読解で裏付け）
