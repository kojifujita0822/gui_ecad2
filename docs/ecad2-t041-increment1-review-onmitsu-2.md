# T-041増分1 修正往復1周目レビュー（隠密、正式再レビュー）

> 2026-07-07 隠密レビュー。対象コミット`1edf36c`（`fix(app): T-041増分1修正往復1周目 - SelectedCell
> のsetterへ選択排他クリアを集約`）。隠密指摘（`docs/ecad2-t041-increment1-review-onmitsu.md`観点2、
> CONFIRMED4件）への対応。家老指定観点＋新規テスト実行による実測検証を行った。

---

## 結論：**クリーン。CONFIRMED4件はいずれも構造的に解消。忍者実機確認へ回してよい**

侍の対処は「4箇所への個別パッチ」ではなく、`SelectedCell`のsetter自身を「選択排他を保証する唯一の
真実源」へ格上げする設計転換であり、Escape層3の模範実装が持っていた性質（両プロパティを常に揃って
クリアする）を構造として全経路に波及させたもの。個別に確認した結果、4件とも実装・テストの両面で
解消を確認した。新規テスト5件は実際に`dotnet test`を実行し合格を実測した（下記）。便乗変更なし。
1点、今回の設計転換に伴う軽微な効率上の副作用（severity低）を新たに検出したが、対応不要と判断する。

---

## 対象差分

`git show 1edf36c`で確認。`MainWindow.xaml.cs`（クリック順序入替、Escape層3簡略化）、
`MainWindowViewModel.cs`（`SelectedCell`setterへの集約、`DeleteSelectedConnector`の防御化、
`ReplaceDocument`への明示クリア追加）、`LadderCanvas.cs`（`ToMm`共通化）、新規テストファイル
`SelectedConnectorExclusivityTests.cs`（5件）。

---

## 観点別の検証

### CONFIRMED4件の解消確認 —— **いずれも構造的に解消**

新しい`SelectedCell`のsetter（`MainWindowViewModel.cs`）は次の形になった：

```csharp
public GridPos? SelectedCell
{
    get => _selectedCell;
    set
    {
        SelectedConnector = null;   // SetPropertyの早期returnより前、値変化の有無に関わらず常時
        if (SetProperty(ref _selectedCell, value)) { ... }
    }
}
```

`SelectedConnector = null`を`SetProperty`の**早期return判定より前**に置いたことが設計の要。これにより：

1. **矢印キー移動**（`MoveSelectedCell`）：`SelectedCell = newCell`の1行が内部で自動的に
   `SelectedConnector`をクリアするため、呼び出し元の追従漏れが構造的に起きなくなった。実際に
   `MoveSelectedCell`自体には一切手を入れていない（差分に含まれない）にもかかわらず解消している
   ことをコードで確認——**個別呼び出し元を直す必要が無い**という設計転換の効果を裏付ける。
2. **選択ツールボタン**（`ActivateSelectDefault`）：同様に`SelectedCell = null`の1行で自動解消
   （この関数自体も差分に含まれず、書き換え不要で直っている）。
3. **DRC同一シートジャンプ**：`CurrentSheetIndex`が同値のため`SetProperty`が早期returnしても
   （このパス自体は今回も変更なし）、後続の`OutputPanelViewModel.JumpTo`が必ず`SelectedCell = ...`
   を実行する（早期returnの有無によらず常に実行される箇所）。この代入が`SelectedConnector`を
   自動クリアするため、`CurrentSheetIndex`側の早期return問題を直接修正せずとも実質的に解消される。
   **これは「早期returnの経路も含めて`SelectedCell`のsetterを必ず通る」という保証があって初めて
   成立する解決であり、個別修正より頑健**と評価する。
4. **新規作成/開く**（`ReplaceDocument`）：`_selectedCell = null`が唯一setterをバイパスする直接
   代入のため、ここだけは`SelectedConnector = null`（setter経由）を明示的に追加している
   （491-496行目付近）。バイパス箇所は本当にこの1箇所のみか`grep '_selectedCell\s*='`で
   リポジトリ全体を確認し、他に無いことを確認した。

**実測検証**：`SelectedConnectorExclusivityTests.cs`の該当4テスト（矢印キー相当・選択解除ボタン
相当・DRC同一シートジャンプ相当・新規作成相当）を含む5件を`dotnet test --filter
FullyQualifiedName~SelectedConnectorExclusivityTests`で実行し、**5件全て合格**を確認した
（実行時間29ms、失敗0）。各テストのシナリオがレビュー時に指摘した再現手順と正確に対応している
ことも読み合わせて確認した（例：`SelectedCellAssignment_ClearsSelectedConnector_
EvenWhenCurrentSheetIndexUnchanged`は`vm.CurrentSheetIndex = vm.CurrentSheetIndex;`で自己代入して
早期returnを誘発した上で`SelectedCell`代入の効果を検証しており、DRCジャンプの実際の経路を正確に
模している）。

### Remove()戻り値チェックの妥当性 —— **正しい**

```csharp
public bool DeleteSelectedConnector()
{
    if (CurrentSheet is not Sheet sheet || SelectedConnector is not VerticalConnector connector) return false;
    if (!sheet.Connectors.Remove(connector)) return false;
    MarkDirty();
    SelectedConnector = null;
    return true;
}
```
`List<T>.Remove`の戻り値（実際に要素が見つかり削除できたか）を見て、削除できなければ`MarkDirty()`
を呼ばず`false`を返す。`DeleteSelectedElement()`（`sheet.Elements.Remove`は使わず`SelectedElement`
自体が`FirstOrDefault`由来で実在保証済み）との非対称性への対処として妥当。CONFIRMED4件が解消した
現在ではこの分岐に実際に到達する経路は無いはずだが、防御的実装として適切と判断する。
`DeleteSelectedConnector_ReturnsFalse_WhenConnectorAlreadyRemoved`テストで、シート側から独立して
削除済みのconnectorを対象にした場合に正しく`false`を返すことを実測確認した。

### 新規テスト5件の実効性 —— **確認済み（実測）**

上記の通り実行し全件合格。各テストが対応するCONFIRMED観点と1対1で対応しており、テスト名も
`docs/ecad2-t041-increment1-review-onmitsu.md`の観点番号・再現手順を踏まえた命名になっている
（可読性・トレーサビリティとも良好）。

### 便乗変更なし —— **確認済み**

差分は「選択排他の集約」「Remove戻り値チェック」「DIP→mm変換の共通化（所見B対応）」「回帰テスト」
の4点に限定されており、コミットメッセージが宣言した範囲を超える変更は無い。

### setter集約方式に新たな見落としがないか —— **1件、severity低の効率上の副作用を検出（対応不要と判断）**

`_selectedConnector`への直接フィールド代入（setterバイパス）はリポジトリ全体で0件（grep確認）——
`SelectedConnector`は常にプロパティ経由でのみ変更される、健全な状態。

**新規の軽微な副作用**：`SelectedCell`のsetter内で`SelectedConnector = null`という**別のプロパティの
setter**を呼び出す構造になったため、「連結中の縦コネクタが選択されている状態から、矢印キー等で
`SelectedCell`へ新しい値を代入する」という遷移では、`PropertyChanged`が**2回**（`SelectedConnector`
の変化で1回、`SelectedCell`の変化で1回）発火し、`MainWindow.xaml.cs`の
`ViewModel_PropertyChanged`経由で`RedrawCanvas()`が**2回連続で呼ばれる**（1回目は`SelectedCell`が
まだ更新される前の状態で、2回目が最終状態で描画される）。`RedrawCanvas`は`DiagramRenderer.Render`
（`NetlistBuilder.Build`を含む）を毎回フルで実行するため、厳密には無駄な二重計算が発生する。

**severity判断**：修正前（`592131c`時点）は正しい状態管理ができていなかった（バグ）ため二重描画も
起きていなかったが、修正後は正しさの副産物として二重描画が生じる——「正しさ」と引き換えの軽微な
効率コストであり、機能的には無害（最終的な描画結果は正しい、視認できるちらつき等の報告があれば
別途要検討）。典型的なラダー図面規模（数十〜百要素程度）でのNetlistBuilder再計算コストは軽微と
考えられ、対応不要と判断する。将来`ObservableObject`的な変更通知のバッチ化・遅延評価を検討する
機会があれば併せて解消できる程度の指摘に留める。

---

## 申し送り

- **忍者実機確認へ回してよい**。特に確認いただきたいのは、CONFIRMED4件それぞれの実機再現手順
  （矢印キー移動後のDelete、選択ツールボタン押下後の見た目、DRCジャンプ、新規作成直後の見た目）が
  実際に解消していること、および二重描画（上記）が体感できるちらつき等を伴わないこと。
- 所見A（選択プリミティブの統合設計）は増分3着手時に侍の技術判断へ委ねる方針と家老より聞き及んで
  おり、本レビューでは追跡しない。
- 差分確認の過程で、既にT-041増分2（`be9c15f`「縦コネクタ手動記入」）がコミット済みであることに
  気づいた。本レビューは家老采配通り`1edf36c`単体（増分1の修正）のみを対象としており、増分2は
  範囲外・別途レビュー采配を要すると考える。

---

## 出典・参照

- 対象コミット`1edf36c`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SelectedCell`/`SelectedConnector`setter、
  `DeleteSelectedConnector`、`ReplaceDocument`）
- `src/Ecad2.App/MainWindow.xaml.cs`（クリック順序、Escape層3）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`ToMm`共通化）
- `tests/Ecad2.App.Tests/SelectedConnectorExclusivityTests.cs`（新規5件、実行して合格を実測）
- `dotnet test`実行結果（フィルタ実行5/5合格、`src/Ecad2.sln`全体でCore14件・App37件合格、増分2
  分の追加テストを含む現HEAD時点の値のため件数は増分1単体の値と一致しない点に留意）
- `docs/ecad2-t041-increment1-review-onmitsu.md`（初回レビュー、CONFIRMED4件・所見A/B）
