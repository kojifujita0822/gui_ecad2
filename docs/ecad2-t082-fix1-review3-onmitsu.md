# T-082 修正1再修正 事後レビュー(隠密・往復3周目、フル観点)

- 対象コミット: `043bc1f`(修正1再修正「実体不変の原則」、`SetCurrentSheetIndexWithoutCrossCut`新設)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: 設計書突合(a)+矛盾/後退検分(b)+新規バグ検分(c)+`code-review`スキル併用(d、effort high、10角度並列finder→集約)+テストコード網羅性点検(e、往復3周超ゆえ適用)。実測(`dotnet test`)でビルド・全合格も確認済み

## 結論サマリ

**指摘なし(要修正)で忍者実機へ進めてよいと判断する。** 設計書どおり実装され、「所見L」型再発は根治された。ただし経過観察として、`[CallerMemberName]`の落とし穴(現状実害なし)とテストの軽微な重複を報告する。

## DoD(a): 設計書との突合

`docs/ecad2-t082-fix1-test-design-onmitsu.md`の指定ケースを全て実装確認:

- **P1**(移動対象=選択中シート自身): `[Theory]`6ケース(先頭→末尾/末尾→先頭×2枚・4枚、中間隣接×2)が設計書のBV1〜6と一致。SelectedCell保持・CurrentSheetIndex追従・SelectedSheet実体維持を同一テスト内で同時アサート(設計書の「両立」要求を満たす)。記入中ドラフト保持も代表1件で実装。
- **P2**(間接シフト): 設計書が指摘した「3枚で成立する」最小構成をそのまま採用(4枚を待たず)。SelectedCell保持・追従・実体維持の3点セットを検証。
- **P3**(添字不変): 対称性点検表の空欄だった「記入中ドラフト保持」を穴埋め。
- 既存2件(P1・P2既存Fact)にSelectedCell保持アサーションを統合(設計書「既存2件にSelectedCell保持アサーションを統合」の指示どおり)。

対称性点検表は全セル埋まった(P2の記入中ドラフトのみ、設計書が許容した「単一の分岐点に処理が集約されるなら代表1件で足りる」の条件を実装が満たすため意図的に省略——妥当)。

RED証明の整合: コミットメッセージ「新規9件+既存2件拡張、10件FAIL実測(P3のみ現行PASS)」は理論値と一致することを検算済み(P1・P2は添字が必ず変化しガードが常時trueになるためFAIL、P3のみ添字不変でPASS)。

## DoD(b): 過去修正(修正1・修正3)との矛盾/後退の検分

矛盾・後退なし。`SetCurrentSheetIndexWithoutCrossCut`は`NotifyCurrentSheetDependentPropertiesChanged()`と`SelectedCell=null`を行わない一方、修正3(`RefreshSelectedSheet`のBeginInvoke)には一切手を加えておらず、SelectedSheetの通知は引き続き機能する(既存テストGREEN)。修正2(DRC結果破棄)もMoveSheetCommand内の位置関係は変わらず影響なし。

## DoD(c): 新規作り込みバグの検分

**新規の機能的バグは無し。** ただし以下1件、経過観察に値する構造的懸念を発見(6角度独立検出+実機コンパイル確認済み、CONFIRMED相当だが実害は現状ゼロ)。

### `[CallerMemberName]`によるPropertyChanged誤発火(経過観察)

**該当**: `MainWindowViewModel.cs:208`

```csharp
internal void SetCurrentSheetIndexWithoutCrossCut(int value) => SetProperty(ref _currentSheetIndex, value);
```

`ViewModelBase.SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)`は、`propertyName`省略時に**呼び出し元のメンバー名**(呼び出し連鎖を遡らない)を自動注入する。このメソッドはプロパティのsetterではなく独立したヘルパーメソッドから呼ばれているため、発火するPropertyChangedの`PropertyName`は`"CurrentSheetIndex"`ではなく`"SetCurrentSheetIndexWithoutCrossCut"`になる。

**実害の判定**: XAML側で`CurrentSheetIndex`に直接バインドしている箇所は無く(確認済み)、`PropertyName == nameof(CurrentSheetIndex)`で購読しているコードもコードベース中に存在しない(確認済み)。**現状の画面表示・機能への実害はゼロ**。ただし:
- 既存の`SetCurrentSheetIndexCore`(192-197行目)も同型の欠陥を本コミット以前から抱えており、`ReplaceDocument`(1741行目)だけが`nameof(CurrentSheetIndex)`を明示して回避している——**修正の一貫性が崩れた非対称な状態**が、今回の新設により1箇所から2箇所に増えた。
- このプロジェクトが多用する「診断ログ注入デバッグ」(TraceLog)はプロパティ名文字列に依存するため、誤ったプロパティ名で記録され、将来の調査を誤誘導しうる。
- 将来`CurrentSheetIndex`への直接バインディングや`PropertyChanged`購読が追加された瞬間、静かに機能しなくなる(通知が来ずUIが追従しない)。

**修正は必須ではない**(T-082のDoD=SelectedCell/ドラフト保持・CurrentSheetIndexの値自体の追従には影響しない)が、`SetProperty(ref _currentSheetIndex, value, nameof(CurrentSheetIndex))`と明示する軽微な修正を推奨する(ついでに既存`SetCurrentSheetIndexCore`も同様に直せば非対称も解消する。ただし後者はT-082の範囲外、P-XXX起票が妥当)。

## DoD(d): code-reviewスキル併用(10角度、集約)

上記の`[CallerMemberName]`懸念が最多検出(Angle A/B/C/D/E/I、6角度独立)。他の指摘は全て経過観察レベル:

- **設計上の懸念**(Angle I): `SetCurrentSheetIndexCore`/`WithoutCrossCut`のどちらを呼ぶかが呼び出し側の自己申告のみで決まり、「実体不変」の前提を実行時検証する仕組みが無い。将来の誤用(実体が変わる操作からWithoutCrossCutを誤って呼ぶ)を防げない。命名も前提条件(実体不変)を表現できていない。
- **BeginInvoke遅延による選択表示の一時的不整合窓**(Angle E、PLAUSIBLE): `SetCurrentSheetIndexWithoutCrossCut`が同期的にCurrentSheetIndexを更新する一方、`RefreshSelectedSheet`はBeginInvoke(ContextIdle)で非同期。この間、ListBoxのSelectedItemが一時的に無選択表示になりうる(WPFのTwoWayバインディング特性、理論的懸念)。テストはImmediateDispatcherServiceで即時同期実行するためこの窓を検出できない。**忍者実機確認で連続Alt+下操作時の選択ハイライトの瞬間的な消失有無を観察されたい**。
- **テストの重複**(Angle G): Theoryの`expectedNewIndex`パラメータが常に`toIndex`と同値で冗長。P1 Theory6ケースの一部(SetCurrentSheetIndexWithoutCrossCutは単純代入のみで位置による分岐が無い)は実質同一命題の重複。既存Fact(`WhenMovingSelectedSheet_CurrentSheetIndexFollows`)と新設P1 Theoryが同一命題を重複検証。P2(新設3枚)と既存(4枚)もほぼ同一構造。
- **再利用機会**(Angle F): `WithoutCrossCut`と`Core`の共通行(`SetProperty(ref _currentSheetIndex, value)`)を1行ヘルパーへ切り出す余地。新規テストのコネクタドラフトセットアップが既存`ConnectorDraftTests.cs`のパターンと重複(共通化余地)。
- CLAUDE.md規約違反なし(Angle J)。

## DoD(e): テストコード網羅性点検(往復3周超ゆえ適用)

設計書の対称性点検表は全セル充足(上記(a)参照)。実測で`dotnet test`を実行し、Ecad2.App.Tests 489件+Ecad2.Core.Tests 89件=**578件、0失敗を自ら確認**(侍申告と完全一致)。

網羅性の観点で新規に発見した穴は無し(前回report済みの穴は全て埋まった)。上記(d)のテスト重複は「穴」ではなく「過剰」の指摘であり、DoD達成上の問題ではない。

## 派生提案の有無

- `[CallerMemberName]`問題(`SetCurrentSheetIndexCore`/`WithoutCrossCut`両方)の是正はT-082範囲外。P-XXX起票を検討されたい(実害は現状ゼロだが、診断ログ運用や将来のバインディング追加時に静かな不具合の温床になる)。
- Angle E指摘の「BeginInvoke遅延による選択表示の一時窓」は、忍者実機確認の観察項目に加えることを推奨(要修正ではなく観察のみ)。
