# T-064 往復1周目修正 再レビュー(隠密)

- 対象コミット: `5a2568a`(侍、隠密静的レビュー`docs/ecad2-t064-image-insert-review-onmitsu.md`の要修正5件対応)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: 台帳DoD整合確認(元指摘5件の解消確認、重点=修正1)+`code-review`スキル(軽量既定、3系統に絞って実施)
- スコープ境界: レビューのみ、書き込みなし。経過観察3件(SelectedImageIsTracingOnly通知漏れ・Undo実復元テスト無し・境界値5.0テスト無し)は家老指示により対象外。

## 結論サマリ

元指摘5件(修正1〜5)はいずれも正しく実装されており、**解消確認OK**。回帰テスト2件(修正1)・1件(修正4)は隠密指摘の具体例を数値レベルで再現し、旧実装でRED・新実装でGREENに遷移することを手計算でも独立に確認した。

一方、`code-review`スキル併用の3系統独立検分で**新規2件**を発見した——1件は修正5自体の対称性回復が一部未達成(CONFIRMED)、もう1件はUI/UX判断を要する仕様変更の疑い(PLAUSIBLE、殿確認要)。

## 元指摘5件の解消確認

| # | 内容 | 確認結果 |
|---|------|---------|
| 修正1 | リサイズの最小サイズ制約とページ境界クランプの順序 | `ClampResizeTarget`新設により1軸ずつ両制約を同時に満たす方式へ変更。4ハンドル全ての符号方向を手計算で追い、新設テスト2件の期待値と整合を確認。境界外はみ出しは解消。 |
| 修正2 | 矢印キーにPlaceImage分岐が無くドラフト無警告消失 | 専用分岐(無視のみ)を追加、Escapeとの対称性を回復。 |
| 修正3 | EscキーでSelectedImageが解除できない | 条件リストへ`SelectedImage is not null`を追加。 |
| 修正4 | ConfirmImageInsertDraftでSelectedCellがnullにならず削除対象を取り違える | `SelectedCell = null` → `SelectedImage = image`の順に修正。`SelectedCell`のsetter(263行目、無条件で`SelectedImage`をクリアする副作用)との相互作用も確認、順序が正しく機能する。回帰テストがOR連鎖の誤動作を正しく再現・是正。 |
| 修正5 | 右クリックがHitTestImageを呼ばず行操作メニューへフォールバック | `HitTestImage`分岐を追加。ただし**下記「新規要修正1」のとおり対称性回復は一部未達成**。 |

## 新規要修正(CONFIRMED)

### 1. 右クリックの行範囲外ガードにより、上部余白帯の画像は右クリックメニューが一切出ない

**該当**: `MainWindow.xaml.cs:867`(既存ガード、本コミット差分外)と`:901`(修正5で新設した画像ヒットテスト分岐)

```csharp
var pos = LadderCanvasHost.ToGridPos(position);
if (pos.Row < 0 || pos.Row >= sheet.Grid.Rows) return;   // 867行目、既存ガード
...
else if (LadderCanvasHost.HitTestImage(position, sheet) is Ecad2.Model.ImageInsert hitImage)   // 901行目、修正5で新設
```

`GridGeometry.RowAt(yMm) = floor((yMm - MarginMm) / CellMm)`(`MarginMm`既定15.0mm)。画像はグリッド非依存の自由配置要素で、Y座標は0からページ境界まで自由に配置・リサイズ可能(境界クランプの下限が0)。**画像がY<15mm(上部余白帯)に位置する場合、`pos.Row`が負になり867行目で即returnし、901行目の新設分岐に到達しないため右クリックメニューが一切表示されない**。

**発火条件→誤った結果**: 画像をY=0〜14mm付近(ページ上端の余白帯、トレース用下絵を用紙全体に配置するようなケースで起こりうる)に配置し、その画像上で右クリックする。メニュー自体が開かず、削除も選択もできない。左クリック(827行目)には行範囲チェックが無く画像はページ全域で選択できるため、**修正5が意図した「左クリックとの対称性回復」がこの領域では未達成のまま残る**。

**検出経路**: code-reviewスキル角度A+C(独立agent)が発見、隠密がコードを読解し`GridGeometry.cs`の数式で裏取り済み(事実)。

## 新規要確認(UI/UX判断要、PLAUSIBLE)

### 2. リサイズでハンドルをアンカーの反対側へ超えてドラッグした際の「反転追従」が失われた

**該当**: `MainWindowViewModel.cs:1298-1326`(`UpdateResizeImage`/`ClampResizeTarget`)

旧実装は`rawWidth = clampedCurrentX - anchorX`の符号で反転を許容し、ハンドルをアンカーの反対側まで引きずると矩形がその軸だけ反転してマウスに追従し続けていた。新実装は`growsRight`/`growsDown`をハンドル種別のみで固定するため、この反転追従が失われている。

**具体例**(手計算で確認、事実): BottomRightハンドルでアンカー(10,20)の画像を`UpdateResizeImage(currentXMm: 5, currentYMm: 25)`(X方向だけアンカーを超えて左へ)で呼ぶと:
- 旧実装: `XMm=5, WidthMm=5`(X軸が反転し、マウスのX=5に追従)
- 新実装: `ClampResizeTarget(5, anchor=10, growsPositive=true, maxMm=大)`→`lowerBound=15`→`Math.Clamp(5,15,maxMm)=15`→`XMm=10, WidthMm=5`(マウス位置を無視、アンカー直近の5mmに固定)

**この現象は境界の有無に関わらず、ページ中央でのリサイズ操作でも発生する**(`growsPositive`の判定はハンドル種別のみで決まり、`maxMm`の値には依存しないため)。

**推測との峻別**: これが「境界外はみ出し修正に伴う意図的な仕様変更(反転よりクランプ優先が安全側)」なのか「気づかれずに失われた退行」なのかは、コミットメッセージ・元指摘書のいずれにも記載が無く**不明**。`task-implementation`スキルの原則(UI/UX・操作方式に関わる分岐は殿へ選択肢提示)に照らし、殿/家老の確認を要する事項と考える。

**検出経路**: code-reviewスキル角度B+D(独立agent)が発見、隠密が数式を再計算し裏取り済み。

## 参考(範囲外・気づき、本コミットの不具合ではない)

- **`maxXMm`/`maxYMm`の算出がページ端より`MarginMm`分手前で頭打ちになっている可能性**(`MainWindow.xaml.cs:573-574`、既存コード・本コミット差分外): `imgHandleSheet.Grid.Columns * CellMm`で算出されており、`GridGeometry.X(boundary) = MarginMm + boundary*CellMm`という実際のページ座標系とはズレがある(`MarginMm`分小さい)。`ClampResizeTarget`自体は受け取った`maxMm`に対しては正しく動作するため、修正1のバグではない。事実として申し添える(派生提案としてdocs/proposed.md行きの要否は家老の判断に委ねる)。

## code-reviewスキル併用の検分結果(3系統)

1周目軽量ゆえ、フル10角度でなく3系統(角度A+C/角度B+D/既知トラップ3点=SetProperty早期return・表示実行不一致・横展開漏れ)に絞って実施。上記2件以外に確信を持てる指摘は無し。SetProperty早期returnトラップ・T-069型表示実行不一致・横展開チェックリスト6点目(CommitDeviceNameEdit呼び忘れ)はいずれも該当なしと確認。

## 派生提案の有無

範囲外の気づき1件(上記「参考」節)。自らは着手せず家老へ報告のみ。
