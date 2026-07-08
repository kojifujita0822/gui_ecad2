# T-045増分B（P-025 VM層：検証関数+P-020マッピング）静的レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`e45c2d3`（`feat(app): T-045増分B P-025 VM層(検証関数+
> P-020マッピング)`、親`4b9fa2b`）。`code-review`スキル（8角度、各角度1エージェント×1-vote検証
> エージェント、計16エージェント並行）を高effortで併用。実測検証（`dotnet test`、関連コード全体
> 読解・grep横断確認）も併用した。

---

## 結論：**要修正。クリーン確定は保留する**

DoD(1)(2)(3)(5)は出典付きで確認でき、引き継ぎメモ・計画書との食い違いはない。しかし
**DoD(4)【重点】のセレクトSW固定Id判定に、CONFIRMED判定の機能バグを発見した**。過去
T-043隠密レビュー（`docs/ecad2-t043-review-onmitsu-2.md`所見1、CONFIRMED）で既に問題視・修正
された「`PartDefinition.Id`固定文字列一致」という同型のアンチパターンを、本コミットが
`ResolveDeviceClass`で再導入している。加えてView層（`TryPlaceElement`）が増分Bの検証関数に
追随していないための実害あるUXギャップも確認したが、これは計画書上**増分Cで対処予定と明記
済み**のため増分B自体の欠陥ではない。

---

## DoD(1)(2)(3)(5) の検証結果

### (1) 計画書増分B節どおりの実装か

`docs/ecad2-t045-implementation-plan-samurai.md`増分B節と実装を突き合わせ、`ValidatePlacement`
新設（境界+占有統合）・`PlaceElementAtSelectedCell`冒頭ガード
（`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1339`）・P-021占有再チェック／P-022/P-024
境界ガードの解消、いずれも計画どおりの実装と確認した。

### (2) 境界＝殿裁定「下限0」どおりで選択経路に触れていないか

```csharp
public bool ValidatePlacement(GridPos pos, Sheet sheet)
    => pos.Row >= 0 && pos.Row < sheet.Grid.Rows
    && pos.Column >= 0 && pos.Column < sheet.Grid.Columns
    && !sheet.Elements.Any(el => el.Pos == pos);
```
（`MainWindowViewModel.cs:1293-1296`）。行0〜Rows-1・列0〜Columns-1のみ許容で殿裁定と一致。
`SelectedCell`のsetter自体（`MainWindowViewModel.cs`、`SheetNavigationViewModel`とは別）は本
コミットで変更されておらず、選択仕様（行-1・列-2まで選択可）には不干渉。

### (3) MapToDeviceClassが裁可済み案A対応表（全ElementKind20値）と一致するか

`src/Ecad2.Core/Model/Element.cs:17-30`のElementKind全20値（ContactNO, ContactNC, Coil, Lamp,
PushButtonNO, PushButtonNC, SelectSwitch, Terminal, Timer, Counter, TimerContactNO,
TimerContactNC, EmergencyStop, ThermalOverload, TimerInstantContactNO, TimerInstantContactNC,
Motor, Breaker3P, ContactorMain3P, ThermalOverload3P）を、`docs-notes/handover-next-session.md`
§2記載の殿裁可済み案A対応表と1件ずつ突き合わせ、`MapToDeviceClass`
（`MainWindowViewModel.cs:1300-1313`）の16件明示ケース＋4件default(Other)フォールバックが完全
一致することを確認した。

### (5) RED証明整合＋dotnet test実測

境界値Theory8ケース（行-1/0/9/10・列-2/0/19/20）と占有テスト1件の独立性を`ValidatePlacement`
の`&&`連結条件から論理的に確認（片方の条件を除去しても他方は独立して機能する）。
`dotnet test src/Ecad2.sln`実測：

```
成功! -失敗: 0、合格: 14、スキップ: 0、合計: 14 - Ecad2.Core.Tests.dll
成功! -失敗: 0、合格: 183、スキップ: 0、合計: 183 - Ecad2.App.Tests.dll
```

Core14+App183＝197件全合格、コミットメッセージの報告と一致。

---

## DoD(4)【重点】セレクトSW固定Id判定 — CONFIRMED: 機能バグあり

### 問題の所在

```csharp
private DeviceClass ResolveDeviceClass(ElementInstance element)
{
    if (element.PartId == Persistence.BasicPartTemplates.SelectSwitchId) return DeviceClass.SelectSwitch;
    return PartResolver.CreatesComponent(element, PartLibrary)
        ? MapToDeviceClass(PartResolver.ComponentKind(element, PartLibrary))
        : DeviceClass.Other;
}
```
（`MainWindowViewModel.cs:1323-1329`）。セレクトSWは`BasicPartTemplates.cs:127`で
`Role = PartRole.ContactNO`と定義されており（電気的にはa接点と同一、T-037往復2周目の既知制約）、
`PartResolver.ComponentKind`経由では通常の接点と区別できないため、`PartId`の固定文字列一致で
個別判定している。

### なぜ壊れるか（T-035コピー機能との相互作用）

`PartFolderStore.Enumerate()`（`src/Ecad2.Core/Persistence/PartFolderStore.cs:94-110`）は、
Id重複を検出すると後発ファイルの`Definition.Id`を`Guid.NewGuid().ToString("N")`で新Idへ
再採番する（`Role`・`IsOrEligible`等は維持、`Id`のみ変わる）。これはT-035（Explorerでの`.gcadpart`
ファイルコピー、`docs/ecad2-t035-review-onmitsu.md`で実機検証込みの正当操作として設計対象）が
生む重複を解消するための既存機構。

ユーザーがセレクトSWの`.gcadpart`をExplorerでコピーすると、コピー後のファイルは次回列挙時に
新Idへ再採番される。この再採番済みセレクトSWを部品パレット
（`entry.Definition.Id`、`MainWindow.xaml.cs:1407`が`PlaceElementAtSelectedCell`へそのまま渡す）
から配置すると、`ResolveDeviceClass`の`element.PartId == SelectSwitchId`判定は**新Idと一致せず
false**になる。結果、`CreatesComponent`→true（`Role != NonSimulated`）、`ComponentKind`→
`PartRole.ContactNO`（`PartResolver.cs:43`）→`ElementKind.ContactNO`、`MapToDeviceClass`→
**`DeviceClass.Relay`に誤分類される**（本来`DeviceClass.SelectSwitch`であるべき）。

### 既に一度問題視・修正された同型パターンの再導入

`src/Ecad2.App/Converters/PartEntryToGlyphGeometryConverter.cs:17-23`のXMLコメント：

> T-043往復2周目(隠密レビューCONFIRMED、`docs/ecad2-t043-review-onmitsu-2.md`所見1): 判定は
> Definition.Idの固定文字列完全一致ではなくCategory/Role/IsOrEligibleベース。Explorerコピー
> 由来でId再採番された基本図形(T-035、Category/Role/IsOrEligibleは維持されIdのみ変わる)でも
> 正しく個別グリフを表示できる。

`PartThumbnailRenderer.cs`も同じ理由でRole/IsOrEligibleベースへ修正済み（grep確認済み）。
本コミットの`ResolveDeviceClass`は、この**既に一度実害化しCONFIRMED指摘を受けて修正された
「固定Id完全一致」パターンをそのまま再導入**している。ドメイン内に同種バグの前例が2件
（Converter・ThumbnailRenderer）ある中で3件目が生まれた形。

### 判定

`code-review`の独立した2角度（cross-file tracer・Altitude）が同一問題を独立に発見し、verify
エージェントがCONFIRMED判定（出典：`PartFolderStore.cs:94-110`、
`PartEntryToGlyphGeometryConverter.cs:17-23`、`docs/ecad2-t043-review-onmitsu-2.md`所見1、
`docs/ecad2-t035-review-onmitsu.md`）。**DoD(4)「配置の全経路（部品選択リスト・自作パーツ経由
含む）で効くか」への答えは否——初回配置分のセレクトSWには効くが、Explorerコピー後に再配置
されたセレクトSWの経路では効かない。**

### 対応案（隠密所見、実装判断は家老・侍に委ねる）

`PartEntryToGlyphGeometryConverter.cs`同様、`element.PartId`固定判定をやめ、`PartLibrary`から
`PartDefinition`を引いて`Role == PartRole.ContactNO`かつ何らかの弁別フィールド
（例：`Category`や専用フラグ）で判定する形へ置換するのが、ドメイン内の既存解決パターンとの
一貫性が高い。

---

## code-review追加指摘

### CONFIRMED（DoD(4)以外の2件）

**所見B：`TryPlaceElement`が境界チェック未追随でサイレント失敗（増分Bの欠陥ではない）**
`GridGeometry.RowAt/ColAt`（`src/Ecad2.Core/Rendering/GridGeometry.cs:23,26`）はクランプなしの
`Math.Floor`のみ、`MainWindow.xaml.cs:521`のマウスクリックハンドラも境界チェックなしで
`SelectedCell`へ代入するため、境界外座標は実際にUI到達可能（`docs/proposed.md`のP-022/P-024で
忍者・殿本人が実機再現済み、スクリーンショットあり）。`TryPlaceElement`
（`MainWindow.xaml.cs:1327`）は`IsSelectedCellOccupied()`のみで境界を見ず、
`PlacementOkButton_Click`（同`:1398-1412`）は`PlaceElementAtSelectedCell`の結果を確認せず無条件
にバーを閉じるため、`ValidatePlacement`が境界外を理由に拒否しても、ユーザーには何のフィード
バックもない（配置バーが閉じるだけで無反応に見える）。**ただし`docs/ecad2-t045-implementation-plan-samurai.md:107-123`「増分C：P-025 View層」に、まさにこの追随（`TryPlaceElement`が増分Bの
検証関数を呼ぶよう変更しUXフィードバックを強化する）が計画済み事項として明記されている。**
増分B単体の欠陥ではなく、増分Cで解消予定の計画済みギャップ。**ただし増分Bの忍者検証観点
「配置操作全般の回帰確認」でこの状態のまま実機確認に回すと、境界外セルへの配置試行で
無反応に遭遇し「バグ」と誤認されるリスクがあるため、増分Bクローズ前に家老の判断を仰ぐ。**

**所見C：`ValidatePlacement`の不要な`public`化**
呼び出し元は`PlaceElementAtSelectedCell`内部（`MainWindowViewModel.cs:1339`）のみで、テストからの
直接呼び出しもない（grep確認済み）。`private`で十分。

### PLAUSIBLE（3件、対応は任意判断）

**所見D：占有チェックロジックの重複**
既存`IsSelectedCellOccupied()`（`MainWindowViewModel.cs:1285-1286`、View早期UI警告用）と
`ValidatePlacement`内の占有判定が同一述語`sheet.Elements.Any(el => el.Pos == pos)`を別々に実装
している。二段階チェック（TOCTOU対策）という設計自体は妥当だが、述語だけ共通ヘルパーへ
抽出すれば重複は解消できる。

**所見E：固定Id判定のスケーラビリティ**
`ResolveDeviceClass`の固定Id分岐は現状1件のみのif文だが、`PartEntryToGlyphGeometryConverter.cs`
が既にタプルswitchへデータ駆動化した前例があり、次に同種の特例が増えるとif連鎖化して可読性が
下がる素地がある（所見DoD(4)の対応と併せて解消できる可能性がある）。

**所見F：Sheet.Elements直接Add経路のモデル層不変条件欠如（スコープ外の将来課題）**
`Sheet.Elements`は素の`List<ElementInstance>`でモデル層に配置不変条件の強制がない。本番コードの
`.Elements.Add(`は`PlaceElementAtSelectedCell`（`MainWindowViewModel.cs:1348`）の1箇所のみで
今回のガードは効いているが、将来のD&D配置・貼り付け等で同様のバグが再発しうる。`Sheet.cs`は
本コミットで変更されておらず（最終変更はT-007移植コミット`6eab0ba`）、増分Bのスコープ外。

---

## 家老への確認事項

1. **DoD(4)の機能バグ（セレクトSW誤分類）は増分Bクローズ前の修正が必要と判断する。侍への
   差し戻しを采配されたい。** 対応案は上記「対応案」節参照。
2. 所見B（TryPlaceElement未追随）は増分Cで計画済みだが、増分Bを忍者実機検証に回す前に
   「境界外セルへの配置は増分C対応まで無反応になる」ことを検証観点から除外する、または
   増分Cを先送りしない判断のいずれかを仰ぎたい。
3. 所見C（CONFIRMED、public→private）は軽微だが、DoD(4)修正のための差し戻しと同時に対応
   可能。所見D・E・Fは経過観察でよいと判断する。
