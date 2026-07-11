# T-045増分B修正（セレクトSW誤分類）回帰テスト設計（隠密）

> 2026-07-09 隠密起草。対象task_id=T-045増分B修正。`テスト設計と実装の分離【MUST】`
> （`onmitsu.md`）適用案件。同値分割・状態遷移・[Theory]の技法を明示的に適用する。
> **本書はテスト設計のみ。実装（判定ロジックの具体的な書き方）は侍に委ねる。**

---

## 1. 背景

`docs/archive/ecad2-t045-increment-b-review-onmitsu.md`で報告したCONFIRMEDバグ：
`ResolveDeviceClass`（`MainWindowViewModel.cs:1323-1329`）が
`element.PartId == BasicPartTemplates.SelectSwitchId`という固定Id完全一致でセレクトSWを判定して
いるため、T-035（Explorerコピー機能）で複製されたセレクトSWは`PartFolderStore.Enumerate()`
（`PartFolderStore.cs:94-110`）に新Idへ再採番され、以後`DeviceClass.Relay`に誤分類される。

対応案は`PartEntryToGlyphGeometryConverter.cs:53-63`（T-043往復2周目、隠密レビューCONFIRMED
所見1で確立済みのパターン）に倣い、`Role`＋`IsOrEligible`等のデータフィールドで判定する方向。
固定Id判定と異なり、これらはシリアライズされたデータそのものであり、Explorerコピーで複製されて
も値が維持される（Idのみが再採番の対象）。

---

## 2. 実装詳細への非依存性についての注記【重要】（家老追加要求(1)(2)への回答）

### 2.1 実査結果：PartDefinition単体には安定な弁別フィールドは無い

`PartDefinition`（`PartDefinition.cs:36-51`）の全フィールドを検討した：

| フィールド | コピー・再採番耐性 | 自作パーツとの弁別力 |
|---|---|---|
| `Id` | **無**（T-035再採番の対象そのもの） | — |
| `Name`（文字列） | 有（シリアライズされたデータ、コピーで維持） | **無**——ユーザーが自作パーツに`"セレクトSW"`と同名を付ければ衝突する。Idの固定文字列問題と同型の脆弱性 |
| `WidthCells`/`HeightCells` | 有 | 無（1×1固定、他の基本図形と同一） |
| `Role`+`IsOrEligible` | 有 | **不十分**（4節分類Dが示すとおり、偶然同じ組み合わせを持つ自作パーツと衝突する） |
| `Ports` | 有 | 無（`TwoPorts()`で他の2端子パーツと完全一致） |
| `Primitives`（図形プリミティブ） | 有 | 理論上は可能だが、図形デザイン変更で即座に壊れる脆い比較になり「安定した弁別手段」とは言えない |

**結論：`PartDefinition`単体（`Id`/`Name`/`Role`/`IsOrEligible`/`Ports`/`Primitives`のいずれの
単独ないし組み合わせ）には、コピー再採番後も安定し、かつ自作パーツと衝突しない弁別フィールドは
存在しない。**

### 2.2 実査結果：PartPaletteViewModel.Entries経由ならCategory情報を取得できる

`PartFolderEntry`（`PartFolderStore.cs:6`）は`record PartFolderEntry(string Category, string
FilePath, PartDefinition Definition)`——`Category`を持つ。`MainWindowViewModel`は既に
`PartPalette`（`PartPaletteViewModel`）プロパティを保持しており（`MainWindowViewModel.cs`
コンストラクタ、`PartPalette = new PartPaletteViewModel(partFolderStore);`）、
`PartPaletteViewModel.Entries`（`PartPaletteViewModel.cs:16`、`public IReadOnlyList<PartFolderEntry>`）
から`element.PartId`と一致する`Definition.Id`を持つエントリを動的検索すれば、そのエントリの
`Category`を取得できる：

```csharp
var entry = PartPalette.Entries.FirstOrDefault(e => e.Definition.Id == element.PartId);
```

これは**固定文字列との比較ではなく「現在のPartIdとの動的一致」**であるため、T-035再採番後の
新Idに対しても正しくエントリを引ける（再採番されるのは`Definition.Id`のみで、`Entries`自体は
起動時の`store.Enumerate()`で再採番後の状態を反映して構築されるため——`PartPaletteViewModel.cs:40-41`）。

**結論：`Category=="" && Role==ContactNO && !IsOrEligible`という組み合わせ（`PartEntryToGlyphGeometryConverter.cs:59`と同型）を`PartPalette.Entries`経由で評価すれば、コピー再採番耐性と
自作パーツとの弁別力を両立できる。データ形式変更（`PartDefinition`へのフィールド追加）は
不要と判断する。**

### 2.3 本設計書の方針

上記実査により「データ形式変更なしで安全な弁別が可能」と判断できたため、**要スキーマ変更の
ゲート報告（家老要求(2)後段）は不要**と結論する。ただし、`PartPalette.Entries`経由の実装は
`ResolveDeviceClass`（現在`private`、`PartLibrary`のみ参照）のシグネチャ変更を伴う可能性が
ある（`PartPalette`または`Entries`を直接参照する形へ）。これは実装詳細のため、具体的な書き方は
侍に委ねる。**本設計書は判定に使う正確なコード（`PartPalette.Entries`を使うか、`PartLibrary`を
`PartFolderEntry`のDictionaryに拡張するか等）を指定せず、各テストケースの「入力として与える
状態」と「期待される`DeviceClass`」のみを規定する。** 下記4-4（自作パーツ、Role=ContactNO・
IsOrEligible=false）のケースを誤判定させない実装が必須。

---

## 3. 同値分割

`ResolveDeviceClass`に渡される`ElementInstance`（＝その`PartId`が指す`PartDefinition`の状態）を
以下4分類に分ける。家老指定の分類に対応。

| # | 分類 | Id | Role | IsOrEligible | 期待DeviceClass | 現行実装(e45c2d3)での結果 |
|---|------|----|----|----|----|----|
| A | セレクトSW・元Id | `BasicPartTemplates.SelectSwitchId`（`"basic-select-switch"`固定） | ContactNO | false | **SelectSwitch** | 正（既存テストでカバー済み） |
| B | セレクトSW・再採番Id | 新規GUID（元Idと異なる、Role/IsOrEligible/Portsは同一） | ContactNO | false | **SelectSwitch** | **誤（Relayに誤分類、RED対象）** |
| C | 純正ContactNO(a接点) | `BasicPartTemplates.ContactNOId`（`"basic-contact-no"`固定） | ContactNO | true | **Relay** | 正（既存テストでカバー済み、退行防止として維持） |
| D | 自作パーツ(Role=ContactNO・OR対象外) | 任意のカスタムGUID | ContactNO | false | **Relay** | 正（現行実装は固定Id判定のため巻き込まれない。**対応案が`IsOrEligible`単独判定だと誤ってSelectSwitchへ分類される恐れがあり、これを検出するためのケース**） |

分類の境界線：A/Bは「Idの違いのみ」（Role・IsOrEligibleは同一）——固定Id判定の脆さを直接突く
対。C/Dは「IsOrEligibleの違いのみ」（Role・Idパターンは同系統）——`IsOrEligible`だけに頼った
場合の弁別力を試す対。B/Dは「Role=ContactNO・IsOrEligible=falseで一致するが期待結果が異なる」
——対応案が真にセレクトSW由来かどうかを弁別できているかを試す最も厳しい対。

---

## 4. 状態遷移（コピー→再採番→配置）

セレクトSWパーツのライフサイクルを状態遷移として整理する。

```
[S0: オリジナルのみ存在]
    Id=SelectSwitchId, Role=ContactNO, IsOrEligible=false
        |
        | Explorerで.gcadpartファイルをコピー(T-035が正当操作として設計対象)
        v
[S1: ファイルレベルでId重複]
    2ファイルが同じId=SelectSwitchIdを持つ
        |
        | PartFolderStore.Enumerate()実行(起動時列挙、PartFolderStore.cs:94-110)
        | 先着(最古)のIdは維持、後発ファイルのみ新Idへ再採番
        v
[S2: 再採番済み]
    後発コピー: Id=<新GUID>, Role=ContactNO, IsOrEligible=false（維持）
    元ファイル: Id=SelectSwitchId, Role=ContactNO, IsOrEligible=false（不変）
        |
        | パレットから配置(MainWindow.xaml.cs:1407→PlaceElementAtSelectedCell)
        v
[S3: 配置済み、DeviceClass確定]
    ElementInstance.PartId=<S2のId>→ResolveDeviceClass→Document.Devices登録
```

**あり得る遷移**：S0→S1（コピー）、S1→S2（再採番）、S2→S3（配置、元Id/再採番Idどちらの
インスタンスからでも独立に到達可能）、S0→S3（コピーなしの直接配置、分類A相当）。

**あり得ない遷移**：S2からS0への逆行（再採番は不可逆、`PartFolderStore.cs:104`で
`SaveOne`により`.gcadpart`ファイルへ書き戻される）。

**テスト方針**：S1→S2（`PartFolderStore`の重複検出・再採番ロジックそのもの）は
`MainWindowViewModelTests.cs`のスコープ外（`PartFolderStore`関連の既存/将来テストが担当領域）。
本設計は**S2の状態を直接構築**（`vm.PartLibrary.ById[<新GUID>] = new PartDefinition { ... }`、
既存の`PlaceElementAtSelectedCell_WithNonSimulatedCustomPart`テストと同じ手法）してS2→S3の
遷移（配置→DeviceClass確定）のみを検証する。S0→S1→S2の実際の再採番動作の実測は、コミット
`aba8c51`ではなく本修正のスコープ外——ただし`PartFolderStore`側に回帰がないことは既存の
`PartFolderStore`関連テスト（dotnet test実測）で担保される前提。

---

## 5. テストケース一覧（[Theory]化）

`MainWindowViewModelTests.cs`に追加する想定。パーツ定義の構築が分類ごとに異なる（固定Id利用 vs
`PartLibrary`への動的追加）ため、単純な`[InlineData]`4行の`[Theory]`ではなく、各分類ごとに
「`PartDefinition`の構築ロジック」を伴う。以下の形を提案する（実装コードは侍が書くため、ここは
アサーション内容の仕様としてのみ提示）：

```csharp
public static IEnumerable<object[]> SelectSwitchClassificationCases()
{
    // A: セレクトSW・元Id
    yield return new object[] { BasicPartTemplates.SelectSwitchId, (PartDefinition?)null, DeviceClass.SelectSwitch };

    // B: セレクトSW・再採番Id相当(RED対象)。Role/IsOrEligible/PortsはSelectSwitchと同一、Idのみ異なる
    yield return new object[] { "reassigned-select-switch-guid", new PartDefinition
    {
        Id = "reassigned-select-switch-guid", Name = "セレクトSW", Role = PartRole.ContactNO,
        IsOrEligible = false, Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
    }, DeviceClass.SelectSwitch };

    // C: 純正ContactNO(退行防止)
    yield return new object[] { BasicPartTemplates.ContactNOId, (PartDefinition?)null, DeviceClass.Relay };

    // D: 自作パーツ(Role=ContactNO・IsOrEligible=false、対応案の弁別力を試す)
    yield return new object[] { "custom-contact-no-guid", new PartDefinition
    {
        Id = "custom-contact-no-guid", Name = "自作接点", Role = PartRole.ContactNO,
        IsOrEligible = false, Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
    }, DeviceClass.Relay };
}

[Theory]
[MemberData(nameof(SelectSwitchClassificationCases))]
public void PlaceElementAtSelectedCell_ClassifiesSelectSwitchByDataFieldsNotFixedId(
    string partId, PartDefinition? customDefinition, DeviceClass expected)
{
    var vm = CreateViewModel();
    vm.NewDocument();
    if (customDefinition is not null) vm.PartLibrary.ById[partId] = customDefinition;
    vm.SelectedCell = new GridPos(0, 0);

    vm.PlaceElementAtSelectedCell(partId, "X001", isOr: false);

    Assert.Equal(expected, vm.Document.Devices.ByName["X001"].Class);
}
```

`MemberData`（`[Theory]`のデータ源をメソッド化する形）を用いるのは、ケースBとDが単純な
プリミティブ値でなく`PartDefinition`インスタンスの構築を要するため（`[InlineData]`は
コンパイル時定数のみ許容）。

**注記（2.3節と関連）**：上記コード例は`vm.PartLibrary.ById[...]`への直接追加でケースB/Dを
構築する前提で書いたが、これは実装が`PartLibrary`のみを参照する場合にのみ有効。もし侍の実装が
2.2節の`PartPalette.Entries`（`Category`含む）を参照する形になった場合、`PartLibrary`への直接
追加だけでは`Entries`に反映されず、ケースB（`Category==""`のセレクトSW再採番後エントリ）を
正しくシミュレートできない。その場合のテスト構築方法（例：一時フォルダへ実際に`.gcadpart`を
複製し`PartFolderStore.Enumerate()`の重複検出・再採番を実際に発生させる、または
`PartPaletteViewModel`にテスト注入用コンストラクタを設ける等）は侍の実装方式に従って選択して
よい——**規定するのは各ケースの「入力として表現したい状態」（4節の同値分割）と「期待される
最終`DeviceClass`」のみであり、その状態をコードでどう構築するかは実装方式に追随する。**

### 各ケースの期待値とアサーション

| ケース | `vm.Document.Devices.ByName["X001"].Class`の期待値 | 現行実装(e45c2d3)での実測結果 | 修正後 |
|---|---|---|---|
| A（元Id） | `DeviceClass.SelectSwitch` | GREEN（正） | GREEN維持 |
| B（再採番Id） | `DeviceClass.SelectSwitch` | **RED**（実際は`Relay`が返り`Assert.Equal`失敗） | GREEN化 |
| C（純正ContactNO） | `DeviceClass.Relay` | GREEN（正） | GREEN維持 |
| D（自作パーツ） | `DeviceClass.Relay` | GREEN（正、固定Id判定のため巻き込まれない） | **GREEN維持が必須**（対応案が`IsOrEligible`単独判定だと、ここが新規にREDへ転落する。転落したら対応案の弁別力不足＝バグ修正が別のバグを生んだことになる） |

---

## 6. RED証明の実施要領

1. 修正前（コミット`e45c2d3`時点のコード）で上記`[Theory]`を実行し、**ケースBのみ**REDになる
   ことを実測する（A・C・Dは修正前でもGREENのはず——もしA・C・Dのいずれかが修正前から
   REDなら、テストコード側の誤りを疑うこと）。
2. 侍が対応案を実装した後、全4ケースがGREENになることを実測する。
3. **ケースDが実装後もGREENであることに特に注意する**（3節参照）。もし対応案の実装中に
   ケースDがREDに転落した場合、これはバグ修正が新しい誤分類（自作の接点パーツをセレクトSW
   として誤登録する）を生んだことを意味し、実装をやり直す必要がある。
4. 併せて既存の`PlaceElementAtSelectedCell_WithSelectSwitchPart_SetsDeviceClassSelectSwitch`
   （ケースA相当、`MainWindowViewModelTests.cs`既存）・
   `PlaceElementAtSelectedCell_WithContactPart_SetsDeviceClassRelay`（ケースC相当、既存）が
   引き続きGREenであることを回帰確認する（新設`[Theory]`と内容が重複するため、侍の判断で
   統合・削除してよい）。

---

## 7. 対応案（前提として参照するのみ、実装は侍判断）

2.2節の実査結果により、`PartEntryToGlyphGeometryConverter.cs:53-63`のパターン（`Category`＋
`Role`＋`IsOrEligible`の組み合わせ判定）は、`PartPalette.Entries`（`PartFolderEntry.Category`）
経由でVM層からも再現可能と判断する。`ResolveDeviceClass`は`element.PartId`固定判定をやめ、
`PartPalette.Entries`から`element.PartId`と一致する`Definition.Id`のエントリを動的検索し、
`Category==""`かつ`Role==ContactNO`かつ`!IsOrEligible`の組み合わせでセレクトSWと判定する形が
候補になる。データ形式変更（`PartDefinition`へのフィールド追加、スキーマ変更）は不要と判断
する——家老要求(2)の「要スキーマ変更」ゲート報告には該当しない。ただし具体的な実装（VM内で
`PartPalette.Entries`をどう参照するか、命名・配置）は侍の判断に委ねる（本節はテスト設計の
前提説明であり、実装方針の指示ではない）。
