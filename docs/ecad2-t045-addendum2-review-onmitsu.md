# T-045補遺2（Stryker棚卸し補強：notify検証+MapToDeviceClass全20値）差分レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`dc81b74`（`test(app): T-045補遺2 Stryker棚卸し補強
> (notify検証+MapToDeviceClass全20値)`）。ヘッドレスで完結（`tests/Ecad2.App.Tests/`5ファイル
> のみ、src変更なし）。実測検証（`dotnet test`全件）と独立検証エージェントによるDoD(4)技術的
> 判断の裏取りを併用した。

---

## 結論：**クリーン（実機不要）。ただしDoD(4)で侍の技術的主張に不正確な点を発見した——
機能バグではなく「代替経路の見落とし」であり、テストとしての実効性には問題ない**

DoD(1)(2)(3)(5)(6)は出典付きで確認できた。DoD(4)（技術的判断の評価）で、侍が採用した
リフレクション手法自体の妥当性は認めつつ、その根拠として述べた「7値にしか実経路で到達できない」
という主張が不正確であることを発見した。

---

## DoD(1) notify()発火検証12箇所の設計妥当性

4種（Connector/WireBreak/FreeLine/ConnectionDot）×3パターン（`SelectedXxxAssignment`/
`SheetSwitch`/`ReplaceDocument`）、計12箇所すべてで同一パターンを確認：

```csharp
bool isDraggingXxxChanged = false;
vm.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(vm.IsDraggingXxx)) isDraggingXxxChanged = true;
};
// トリガー操作（Delete/SheetSwitch/NewDocument）
Assert.False(vm.IsDraggingXxx);
Assert.True(isDraggingXxxChanged);
```

`PropertyChanged`購読が**トリガー操作より前**（`BeginDragXxx`実行後、`DeleteSelectedXxx`等の
呼び出し前）に設定されており、`notify()`（`OnPropertyChanged(nameof(IsDraggingXxx))`）が
削除されれば`isDraggingXxxChanged`は`false`のまま`Assert.True`が確実に失敗する構成。生存
ミュータント（`notify()`のStatement mutation）を確実に殺す設計と確認した。

## DoD(2) 全20値[Theory]の対応表一致

新設`MapToDeviceClass_AllApprovedMappingTableAElementKinds_MatchesExpected`の20件
`[InlineData]`を、`docs-notes/handover-next-session.md`§2の裁可済み案A対応表と1件ずつ突き
合わせ、完全一致を確認した（ContactNO/NC/Coil/ContactorMain3P→Relay、Lamp→Lamp、
PushButtonNO/NC/EmergencyStop→PushButton、SelectSwitch→SelectSwitch、Terminal→Terminal、
Timer系5値→Timer、Counter→Counter、ThermalOverload/ThermalOverload3P/Motor/Breaker3P→Other）。

## DoD(3) RED証明整合

(a) `notify()`呼び出し除去で12件全てREDになることは、DoD(1)の設計（`isDraggingXxxChanged`が
`false`のまま`Assert.True`失敗）から論理的に確実。(b) `MapToDeviceClass`の
`PushButtonNO or PushButtonNC or EmergencyStop => PushButton`アームから`EmergencyStop`を
除去すると、`EmergencyStop`は後続のいずれのケースにもマッチせず`_ => DeviceClass.Other`
へフォールバックするため、`EmergencyStop`の`[InlineData]`ケース1件のみが
`Assert.Equal(PushButton, Other)`で失敗し、他19件（独立したswitch armまたは同一armの別値）
は影響を受けない。侍報告（notify12件RED・EmergencyStopケース1件のみRED）と論理的に整合する。

## DoD(4)【技術的判断の評価】リフレクション経由private static直接呼び出し

### 侍の主張と実際の技術的検証

侍の実装理由：「`PlaceElementAtSelectedCell`経由（`PartResolver.ComponentKind`→Role起点）では
ContactNO/ContactNC/Coil/Lamp/Terminal/PushButtonNO/PushButtonNCの**7値**にしか実際には到達
できない」。

独立検証（隠密の一次分析＋検証エージェントの裏取り、2系統一致）の結果：**この主張は不正確**。

- `ElementInstance.Kind`・`Sheet.Elements`（`List<ElementInstance>`）はいずれも`public`で
  外部から直接操作可能（`Sheet.cs:11`）。
- `ResolveDeviceClass`は`PartResolver.CreatesComponent(element, PartLibrary)`が`true`の場合に
  のみ`MapToDeviceClass`を呼ぶ（`MainWindowViewModel.cs:1339-1341`）。`PartId`が未登録なら
  `ElementCatalog.CreatesComponent(e.Kind)`にフォールバックし、これは`IsContact`（11値）+
  `IsLoad`（4値）+`IsPassthrough`（1値）＝**16値**で`true`（`ElementCatalog.cs:56-72`）。
- `PartResolver.ComponentKind`も`PartId`未登録なら`return e.Kind;`でKindをそのまま通す
  （`PartResolver.cs:40`）。
- つまり、テストコードから`sheet.Elements`へ`PartId`未登録・`Kind`任意の`ElementInstance`を
  直接`Add`し、`vm.SelectedCell`をその位置に合わせて`vm.SelectedElementDeviceName`へ新規名を
  代入すれば（`SelectedElementDeviceName`セッターが`ResolveDeviceClass(el)`を呼ぶ経路、
  `MainWindowViewModel.cs:1188-1192`）、**CreatesComponent=trueとなる16値すべてで
  `MapToDeviceClass`へ実際に到達できる**（Timer系5値・Counter・EmergencyStop・SelectSwitch・
  ThermalOverloadも含む）。

**原理的に到達不可能なのは4値のみ**：`Motor`/`Breaker3P`/`ContactorMain3P`/`ThermalOverload3P`
は`CreatesComponent=false`のため、`ResolveDeviceClass`内で早期`DeviceClass.Other`リターンとなり
`MapToDeviceClass`自体が呼ばれない。どの経路を使ってもこの4値には到達できない。

### 評価

侍の主張（7値）は「`PlaceElementAtSelectedCell`経由に限定した場合」としては正確だが、
「実経路で到達できるのは7値のみ」という結論は誤り（正しくは代替経路込みで16値）。ただし
**「全20値を実経路だけで完全にカバーすることはできない」という結論自体（4値は不可能）は
正しい**ため、何らかの直接呼び出し手段（リフレクション等）が必要という判断の方向性は妥当。
これは機能バグではなく「代替経路の見落とし」——今回採用されたリフレクション一律適用の設計は
シンプルで動作もRED証明込みで正しいため、**テストとしての実効性には問題ない**。

**代替案との比較**：
- 家老提案の`InternalsVisibleTo`+`internal`化：型安全性（リネーム時にコンパイルエラーで検出）
  の面でリフレクションより優れるが、`src`側の変更を伴うため今回のスコープ（tests配下のみ、
  DoD(6)）外。
- リフレクションの脆さ：`GetMethod("MapToDeviceClass", ...)`は文字列ベースのメソッド名指定
  のため、将来`MapToDeviceClass`がリネームされると`GetMethod`が`null`を返し、
  `method!.Invoke(...)`の`!`（null-forgiving演算子）により実行時に`NullReferenceException`が
  発生する。コンパイル時には検出できないが、テスト実行時には確実に（分かりやすく）失敗する
  ため「静かに壊れる」タイプの脆さではない。

**総合判断**：今回の実装（リフレクション一律適用）は機能面で問題なく、スコープ制約（src変更
不可）の下では妥当な選択。ただし「7値」という技術的主張は不正確であり、事実として記録する。
将来`src`側の変更を伴うタスクがあれば、`InternalsVisibleTo`化や、16値については実経路
（`sheet.Elements`直接操作）でのテストへの置き換えを検討する余地がある。

## DoD(5) dotnet test実測

```
成功! -失敗: 0、合格: 14、スキップ: 0、合計: 14 - Ecad2.Core.Tests.dll
成功! -失敗: 0、合格: 214、スキップ: 0、合計: 214 - Ecad2.App.Tests.dll
```
Core14+App214＝228件全合格。コミットメッセージの報告と一致。

## DoD(6) スコープ遵守

`git show dc81b74 --stat`で変更ファイルが`tests/Ecad2.App.Tests/`配下5ファイル
（`ConnectionDotDragTests.cs`/`ConnectorDragAndResizeTests.cs`/`FreeLineDragAndResizeTests.cs`/
`MainWindowViewModelTests.cs`/`WireBreakDragTests.cs`）のみであることを確認。`src`変更なし。

---

## 家老への確認事項

1. DoD(4)所見（侍の「7値」主張の不正確性）：機能バグではなく事実誤認の指摘のため、補遺2の
   クローズ自体は妨げないと判断する。記録として残すのみでよいか、それとも将来の対応
   （`InternalsVisibleTo`化検討等）へ回すかは家老の判断に委ねる。

補遺2は機能面でクリーン、実機不要（テストのみの変更）と判断し、クローズしてよいと考える。
