# T-045増分B修正（セレクトSW誤分類バグ）再レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`3c9dd5a`（`fix(app): T-045増分B修正 セレクトSW誤分類バグ
> (隠密レビューCONFIRMED)`、親`e45c2d3`）。`code-review`スキル（8角度、各角度1エージェント×
> 1-vote検証エージェント、計12エージェント並行）をmedium effortで併用。実測検証（`dotnet test`、
> 関連コード全体読解）も併用した。

---

## 結論：**設計書どおり実装され、DoD(1)〜(5)いずれも確認できた。ただしcode-reviewでCONFIRMED
1件（往復2周目相当の新規指摘）を発見した——今回の修正が意図せず持ち込んだ新しい失敗モード。**
発生条件が稀（基本図形ファイル自体の破損）であり、旧バグ（Explorerコピーで頻発しうる）より
severityは低いと判断するが、事実として報告し対応要否は家老の判断を仰ぐ。

---

## DoD(1)〜(5) の検証結果

### (1) 設計書どおりの実装か

`docs/archive/ecad2-t045-increment-b-fix-test-design-onmitsu.md`の同値分割4分類（A:元Id／B:再採番Id
相当／C:純正ContactNO／D:自作パーツ）を`[Theory]+[MemberData]`（`SelectSwitchClassificationCases()`、
`MainWindowViewModelTests.cs`）でそのまま実装していることを確認した。既存の
`PlaceElementAtSelectedCell_WithContactPart_SetsDeviceClassRelay`（ケースC相当）・
`PlaceElementAtSelectedCell_WithSelectSwitchPart_SetsDeviceClassSelectSwitch`（ケースA相当）は
削除され新設`[Theory]`へ統合されているが、これは設計書6節で明示的に許容された統合であり、
アサーション内容（`Assert.Equal(expected, vm.Document.Devices.ByName["X001"].Class)`）に後退は
ない（むしろB/Dが新規に追加され網羅性は向上）。

### (2) 判定の妥当性

```csharp
var entry = PartPalette.Entries.FirstOrDefault(e => e.Definition.Id == element.PartId);
if (entry is { Category: "", Definition.Role: PartRole.ContactNO, Definition.IsOrEligible: false })
    return DeviceClass.SelectSwitch;
```
（`MainWindowViewModel.cs:1332-1334`付近）。`PartFolderEntry`は`sealed record`（参照型）のため
`entry`が`null`の場合、プロパティパターンは安全に偽と評価される（code-review Angle A確認済み）。
`Category==""&&Role==ContactNO&&!IsOrEligible`の組み合わせは、基本図形5種のうちセレクトSWのみが
該当し（`ContactNO`は`IsOrEligible=true`のため除外、他のRoleは条件不一致で除外）、
`PartEntryToGlyphGeometryConverter.cs:53-63`の確立済みパターンと同型。他の部品種を巻き込まない。
`element.PartId`は`PartFolderStore.Enumerate()`が再採番後の状態を反映して`Entries`を構築するため
（`PartPaletteViewModel.cs:40-48`）、動的一致による再採番耐性は成立している。

### (3) RED証明整合

修正前（`e45c2d3`、固定Id判定）で新設`[Theory]`4ケースを論理的にトレースした：
- ケースA（元Id）：`element.PartId == SelectSwitchId`が真→SelectSwitch。期待値もSelectSwitch。GREEN。
- ケースB（再採番Id相当）：Id不一致→`CreatesComponent`→`ComponentKind`（Role=ContactNO）→
  `MapToDeviceClass`→Relay。期待値SelectSwitchと不一致→**RED**。
- ケースC（純正ContactNO）：Id不一致→同経路→Relay。期待値もRelay。GREEN。
- ケースD（自作パーツ）：Id不一致→同経路→Relay。期待値もRelay。GREEN。

設計書・コミットメッセージの「修正前でケースBのみRED、A/C/D GREEN維持」という報告と完全に一致。

### (4) テスト実環境副作用の原則（T-042）に反しないか

新設テストは`Path.GetTempPath()`＋`Guid.NewGuid():N`でユニークな一時ディレクトリを生成し
（実ユーザープロファイル・実`MyDocuments`は不使用）、`try/finally`で`Directory.Delete(tempDir,
recursive: true)`により確実に後始末している（`MainWindowViewModelTests.cs:250-274`）。並列実行
時もディレクトリ名がGUIDベースのため衝突しない。T-042の原則（実環境副作用の解消）には反して
いない。ただしcode-reviewで、基底`ViewModelTestBase`が別途生成する一時フォルダが本テストでは
未使用のまま毎回破棄される、という簡潔性の指摘があった（後述PLAUSIBLE所見）。

### (5) dotnet test実測

```
成功! -失敗: 0、合格: 14、スキップ: 0、合計: 14 - Ecad2.Core.Tests.dll
成功! -失敗: 0、合格: 185、スキップ: 0、合計: 185 - Ecad2.App.Tests.dll
```
Core14+App185＝199件全合格。コミットメッセージの報告と一致。

---

## code-review追加指摘

### CONFIRMED（1件、往復2周目相当）

**所見G：基本図形ファイル読込失敗時にセレクトSWがRelayへ静かに退行する（新規リスク）**

`PartFolderStore.Enumerate()`は読込失敗ファイルを`catch { continue; }`でスキップする
（`PartFolderStore.cs:74-75`、T-039の教訓＝起動を止めないためのベストエフォート方針）。
`PartPaletteViewModel`は`Entries`と`Library`を同一`Enumerate()`結果から構築するため
（`PartPaletteViewModel.cs:40-48`）、セレクトSWの基本図形ファイル（`図形/セレクトSW.gcadpart`）
自体がOneDrive同期競合・破損等で読込失敗すると、`Entries`からそのエントリが消える。この状態で
既存配置済みのセレクトSW要素（`PartId==SelectSwitchId`のまま）を新規デバイス名でリネームすると、
`ResolveDeviceClass`は`entry`が見つからずフォールバック（`PartResolver.CreatesComponent`→
`ComponentKind`）へ進み、`ElementInstance.Kind`が既定値`ElementKind.ContactNO`（enumの0番目）の
ままのため（`PlaceElementAtSelectedCell`が`Kind`を明示設定しない、`MainWindowViewModel.cs:1352-1357`）、
`MapToDeviceClass`で`DeviceClass.Relay`へ誤分類される。

**旧実装（固定Id判定）は`Entries`/`Library`の状態に一切依存しないため、この失敗モードを持たな
かった。** 今回の修正は「Explorerコピー再採番による誤分類」（頻度：中〜高、ユーザー操作で容易に
再現）を解消する代わりに、「基本図形ファイル読込失敗による誤分類」（頻度：極めて稀、通常運用
では起きない異常事態）という新しい失敗モードを持ち込んだ形。トレードオフとして妥当な範囲と
判断するが、事実として報告する。

### PLAUSIBLE（3件、対応は任意判断）

**所見H：テストの一時フォルダ管理が`ViewModelTestBase`と重複**
新設`[Theory]`は独自に`tempDir`/`PartFolderStore`を生成・削除しているが、基底`ViewModelTestBase`
（コンストラクタ/`Dispose()`で別の一時フォルダを管理）は本テストでは一切使われず、無駄に生成・
破棄される。`CreateViewModel()`に「事前に`.gcadpart`を書き出せるオーバーロード」を追加すれば
重複は解消できた可能性があるが、現行`CreateViewModel()`はストア単体へのアクセスを返さない設計
のため、技術的必然というより設計改善の余地。

**所見I：部品種別弁別ロジックの4箇所重複**
`Category==""&&Role==X&&IsOrEligible==Y`という同型の判定ガードが、
`PartEntryToGlyphGeometryConverter.cs`・`PartThumbnailRenderer.cs`・`PartPaletteViewModel.cs`
（`IsOrEligible`単体版）に続き、`ResolveDeviceClass`で4箇所目の独立実装となった。各ファイルの
目的（アイコン選択・サムネイル描画・OR論理エントリ生成・BOM分類）は異なり直ちに統合すべきでは
ないが、T-037でIsOrEligible導入時に複数往復を要した実績があり、将来条件変更時の同期漏れリスク
は現実的。ただし今回は単発バグ修正（往復1周目）でスコープ外のため、経過観察が妥当。

**所見J：Id基準マッチングの将来リスク**
`PartPalette.Entries.FirstOrDefault(e => e.Definition.Id == element.PartId)`は依然Id基準の
マッチング。現状`Entries`の唯一の構築元は`PartPaletteViewModel.cs:40-41`で、`PartFolderStore.
Enumerate()`のId重複防止機構（T-035）に守られているため、現時点でIdが重複することはない。将来
`Enumerate()`を経由しない`Entries`相当の追加経路が新設された場合の理論的リスクだが、本コミット
が悪化させたものではなく経過観察でよい。

---

## 家老への確認事項

1. **所見G（CONFIRMED、新規リスク）の対応要否**：発生条件が極めて稀（基本図形ファイル自体の
   破損）なため経過観察で足りると判断するが、往復2周目に相当する指摘のため家老の裁定を仰ぐ。
2. 所見H・I・Jは経過観察でよいと判断する（所見Iは将来の共通化候補として`docs/proposed.md`
   送りの余地はあるが、隠密からの新規タスク化はしない）。
