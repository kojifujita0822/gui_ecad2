# T-045クローズ前 Stryker.NET手動棚卸し（隠密）

> 2026-07-09 隠密調査。task_id=T-045クローズ前棚卸し。家老裁定（規定上は非該当＝増分B往復1周・
> 増分D裁定変更1回だが、`MainWindowViewModel.cs`を大きく触ったリファクタのmainマージ前保険と
> して保守的既定で実施）。手順は`docs/ecad2-t046-stryker-survey-onmitsu.md`のとおり、App中心。

---

## 実行結果サマリ

`tests/Ecad2.App.Tests`ディレクトリから`dotnet stryker -p "Ecad2.App.csproj" -c 4`を実行
（出力はスクラッチパッド、`src`/`tests`は未変更）。

```
2274 mutants created
324 CompileError（未割り当てローカル変数由来、WPF機構とは無関係、既知の事象）
1101 NoCoverage
248 Ignored（Removed by block already covered filter）
601 tested: Killed 377 / Survived 224
The final mutation score is 22.15 %
```

前回T-046調査時（`docs/ecad2-t046-stryker-survey-onmitsu.md`、App 19.79%）から改善している
（T-041/T-042/T-045で追加されたテストの蓄積による）。

**手順上の注意（次回棚卸し実施者への申し送り）**：`src/Ecad2.App`ディレクトリから実行すると
StrykerがEcad2.App.csproj自体をテストプロジェクトと誤認識し失敗する
（`can't be mutated because no test project references it`）。**必ず`tests/Ecad2.App.Tests`
ディレクトリから`-p "Ecad2.App.csproj"`で実行すること。**

---

## T-045変更領域のmutation score・生存ミュータント

対象範囲（`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`内、行番号は現在のHEAD時点）：
`ForceCancelIfAny`(309-314)・`ConfirmDrag<T>`(893-897)・`CancelDrag<T>`(902-906)・
`IsSelectedCellWithinGrid`(1289-1290)・`IsWithinGridBounds`(1292-1294)・
`ValidatePlacement`(1299-1300)・`MapToDeviceClass`(1307-1320)・`ResolveDeviceClass`(1333-1342)。

| | 件数 |
|---|---|
| 対象範囲内の総ミュータント数 | 49 |
| Killed | 32 |
| Survived | 6 |
| その他（NoCoverage/CompileError/Ignored） | 11 |

対象範囲に限定した実測ミュータント率（簡易）：Killed 32/49 ≈ 65%（NoCoverage等除く分母では
32/38 ≈ 84%）。App全体平均（22.15%）より大幅に高く、T-045で新規に書かれたテストは相応の
検出力を持っていると言える。

---

## 生存ミュータント6件の内訳とテストの穴としての評価

### ForceCancelIfAny（2件）

```
Line 311 [Statement mutation]: if (!isActive()) return;
Line 313 [Statement mutation]: notify();
```

既存の`SelectedXxxAssignment_ForceCancelsInProgressDrag`/`SheetSwitch_ForceCancelsInProgressDrag`/
`ReplaceDocument_ForceCancelsInProgressDrag`系テスト（4種×3パターン、`ConnectorDragAndResizeTests.cs`
等）は`Assert.False(vm.IsDraggingConnector)`のように**算出プロパティの最終値**を検証している
が、`notify()`（`OnPropertyChanged(nameof(IsDraggingXxx))`）が**実際にPropertyChangedイベントを
発火させたか自体**は検証していない。`IsDraggingXxx`は`_draggingXxx is not null`から都度算出
されるため、通知が飛ばなくても`Assert.False`は成立してしまい、ミュータントが生存する。

**評価**：`notify()`削除（Line 313）は、View側の`MainWindow.xaml.cs`の
`ViewModel_PropertyChangedがこれを受けてキャプチャ解放・Viewローカル一時フラグのリセット等の
後始末を行う」（コメント307-308行）という実際の消費者が存在するため、**テスト補強価値あり**
と判断する（PropertyChangedEventHandlerを購読し発火回数を確認するテストを追加すれば検出可能）。
`isActive()`ガード削除（Line 311）は、`CancelDrag<T>`自体が`if (dragging is not null)`という
内側ガードを持つ二重ガード構造（前回`docs/archive/ecad2-t045-increment-d-review-onmitsu.md`所見で
REFUTED判定した「ConnectionDot固有の非対称」ではなく4型共通の構造）のため、外側ガード削除でも
実害が観測されにくい――**補強優先度は低いと判断する**。

### MapToDeviceClass（4件）

```
Line 1312 [Logical mutation]: ElementKind.PushButtonNO or ElementKind.PushButtonNC or ElementKind.EmergencyStop
Line 1316 [Logical mutation]: ElementKind.Timer or ElementKind.TimerContactNO or ElementKind.TimerContactNC (×3箇所)
```

既存テスト（`MainWindowViewModelTests.cs`）は`ContactNOId`→Relay・`TerminalId`→Terminal・
`SelectSwitchId`→SelectSwitch・自作`NonSimulated`パーツ→Otherの4ケースのみを直接検証しており、
**`MapToDeviceClass`のswitch式が持つ16個の明示的ケースのうち、PushButtonNO/PushButtonNC/
EmergencyStop→PushButton、Timer/TimerContactNO/TimerContactNC/TimerInstantContactNO/
TimerInstantContactNC→Timer、Counter→Counterの各分類は一切直接テストされていない**（`or`条件の
一部を削除・変更しても検出できないためミュータントが生存する）。

**評価**：T-045 P-020（種別マッピング）の対応表は殿裁可済み案A全20値のうち、機器表分類の
主要カテゴリ（PushButton・Timer）が未検証のまま。**テスト補強価値ありと判断する**――
`PlaceElementAtSelectedCell_WithContactPart_SetsDeviceClassRelay`等の既存パターンに倣い、
`BasicPartTemplates`に無いPushButton/Timer系のElementKindを直接持つ`PartDefinition`を
`vm.PartLibrary.ById`へ追加して配置する形の`[Theory]`で網羅できる。

---

## 結論・仕分け候補

| 候補 | 補強優先度 | 理由 |
|---|---|---|
| `ForceCancelIfAny`の`notify()`発火検証 | 中 | View側の後始末処理の消費者が実在するテスト空白 |
| `ForceCancelIfAny`の`isActive()`ガード検証 | 低 | `CancelDrag<T>`内側ガードとの二重防御により実害観測が困難 |
| `MapToDeviceClass`のPushButton/Timer系マッピング | 中〜高 | P-020対応表の主要カテゴリが未検証、既存パターンで容易に補強可能 |

**位置づけ**：家老裁定どおり、本棚卸しはマージのブロッカーとしない。重大な穴（正しさに直結する
欠陥）は無かった――生存ミュータントはいずれも「検証範囲の狭さ」であり「機能バグ」ではない。
侍のテスト補強に回すか、T-045クローズ後の扱いとするかは家老の仕分けに委ねる。

---

## 出典

- 実測：`tests/Ecad2.App.Tests`から`dotnet stryker -p "Ecad2.App.csproj" -c 4`実行
  （バージョン4.16.0、出力先スクラッチパッド、`src`/`tests`は未変更）
- レポートJSON解析：`mutation-report.html`内埋め込みJSON（`app.report`）をNode.jsで抽出し、
  対象8メソッドの正確な行範囲（現在のHEAD時点、`MainWindowViewModel.cs`実読で確認済み）で
  フィルタして集計
- `docs/ecad2-t046-stryker-survey-onmitsu.md`（前回調査・手順の出典）
