# T-033 増分2 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット `8fd60f6`（`feat(app): T-033増分2 - 位置・レイアウトの殿注文3点を実装`）。
> 家老指定の観点(a)〜(f)＋`code-review`スキル（high、8角度finder→verify）併用。加えてレビュー進行中に殿実機目視の
> 追加報告（バー上側表示・非決定性）とP-024（範囲外セル配置）を受け、当該観点も本文書に統合する。

---

## 結論：**要修正・重大あり。忍者の実機確認は本欠陥の修正後に回されたい**

殿注文1「選択セル直下への表示位置」が**恒常的に成立していない**。原因はコード自身の前提（コメント
「MainWorkAreaGridの原点はラッパー経由でも不変」）が誤っている構造的欠陥（座標系不整合）で、増分2で
新設した`MainContentArea`ラッパーGrid（IsEnabled一元化・改善事項）と、同じコミットで実装した
`PositionPlacementBar`（座標計算）が組み合わさって発生した。**ビルド0エラー・テスト24件合格は確認したが、
これは今回の欠陥を検出する類のテストではなく、非検出は想定通り。**

---

## 独自ビルド・テスト検証（家老報告の裏取り）

`dotnet build src/Ecad2.sln` → 0エラー、`dotnet test src/Ecad2.sln` → 12+12=24件合格を隠密独自に再実行し確認。
家老報告と一致。

---

## 手動観点別結果

### (a) 座標変換の正しさ —— **CONFIRMED（重大）**

`PositionPlacementBar`（`MainWindow.xaml.cs:639-654`）は`LadderCanvasHost.TranslatePoint(point, MainWorkAreaGrid)`
で座標を求め、その値をそのまま`ElementPlacementBar.Margin`に設定している。しかし：

- `MainWorkAreaGrid`は増分2で新設された`MainContentArea`（`Grid.Row="0" Grid.RowSpan="4"`、XAML80行目）の**内部**
  Row2に位置する（XAML269行目）。
- `ElementPlacementBar`は`MainContentArea`の**外**、ルートGridの直接の子で`Grid.Row="2"`（RowSpanなし＝1、
  XAML471行目）。
- ルートGridのRow0/Row1/Row3（Auto行）を**単独専有する子要素はもはや存在しない**（Menu/ToolBarTray/
  OutputPanelAreaは全て`MainContentArea`内部に格納された）。WPFのGrid測定アルゴリズムの既知の癖（Auto行の
  基本サイズは単一行専有の子要素からのみ決まり、RowSpanをまたぐ子要素の超過分はStar行へ優先配分される）
  により、ルートRow0/1/3の実測サイズは実質0に潰れ、ルートRow2（`ElementPlacementBar`の親セル）の画面上の
  原点はウィンドウ最上部（メニュー・ツールバーの下ではなくy=0相当）までずれる。
- 一方`MainWorkAreaGrid`の画面上の原点は、`MainContentArea`内部でメニュー・ツールバー分だけ正しく下に
  オフセットされたまま（内部Gridの計算は独立して正しい）。

**結果：`TranslatePoint`が返す「MainWorkAreaGrid基準の座標」を「ルートGrid Row2基準の座標系」である
`ElementPlacementBar.Margin`へそのまま流用しており、両者の原点は一致しない（食い違い量≒メニュー高さ＋
ツールバー高さ、概算90〜140px）。この座標系の取り違えが、バーが選択セルより恒常的に上へ表示される
直接の原因である。**

この構造的欠陥自体（原点不一致の存在）は、静的コード解析のみでCONFIRMEDと判断する（XAML構造の実読み・
WPF Gridの周知の仕様・8fd60f6の差分でラッパー導入とTranslatePoint方式化が同一コミットで行われた事実の
3点が符合するため）。

**殿実機目視4症状との整合**：
- 症状1（行1選択→バーが作業域を突き抜けツールバー直下に重なる）：ローカルY小＋恒常上方オフセットで整合。
- 症状2（行6選択→バーがセルの約2行上）：オフセット量（90〜140px）がラダー行間隔の2〜3行分に相当し整合。
- 症状3（行10・最下段選択→バーがキャンバス最上部）：恒常オフセットに加え、スクロール位置次第で
  `TranslatePoint`結果自体が小さくなる可能性があり、筋は通るが**スクロール状態を実機確認していないため
  推測（PLAUSIBLE）**。
- 症状4（同一セルで開き直すたびに表示位置が変わる＝非決定的）：**恒常的な原点不一致だけでは説明できない
  （同一セル・同一スクロール状態なら毎回同じズレ量になるはず）**。追加要因（`Measure`呼び出しタイミングと
  実際のレイアウトパスの前後関係、スクロール位置の差等）の関与が疑われるが、静的読解だけでは断定できず
  **不明（実機での診断ログ計測が必要、後述の申し送り参照）**。

反転仮説（TranslatePointのsource/target取り違え等）については、コード上`TranslatePoint(point, MainWorkAreaGrid)`
の引数順・使用法自体はWPF標準APIとして正しく、`SymbolAutomationPeer.cs`の既存使用例とも整合しており、
**反転そのものは確認できなかった（REFUTED寄り）**。症状の見た目が「反転」のように見えるのは、原点不一致
オフセットが選択行によらず一定量である一方、行の深さによって「元の正しい位置」からの相対的なズレの
印象が変わって見えるためと考えられる（推測）。

### (b) 画面端クランプの境界挙動 —— **CONFIRMED（要修正）＋ PLAUSIBLE**

- **CONFIRMED**: `Math.Clamp`（650-651行目）は`MainWorkAreaGrid`の外周（右端・下端）との非重なりのみを
  保証し、**選択セル自体との非重なりは保証しない**。セル高は`CellMm=9.0mm × MmToDip(96/25.4)≒34DIP`
  （100%ズーム時）であり、横長化後のバー高さ（24pxアイコン2個＋Padding=6×2等から見積もり30台後半DIP）は
  同程度以上になりうる。最下行付近のセルを選択すると、クランプにより`y`がセル上端以上まで押し戻され、
  **バーが配置対象セル自体を覆い隠す可能性**が論理的に排除されていない。
- **CONFIRMED**: バー表示中でも`MainContentArea`のIsEnabled無効化スコープはウィンドウ本体（リサイズ操作の
  受け口）を含まないため、ウィンドウリサイズ自体は実行可能。しかし`PositionPlacementBar`は表示時に一度
  だけ計算され、`SizeChanged`等の再配置フックが存在しない。バー表示中にウィンドウをリサイズすると、
  クランプ基準の`MainWorkAreaGrid.ActualWidth/Height`は変わるがバーの`Margin`は再計算されず、作業域外に
  取り残される可能性がある。

なお(a)の座標系不整合が解消されない限り、クランプの基準点自体が誤っているため、(b)の指摘は(a)修正後に
改めて実機確認する必要がある。

### (c) 旧見切れの構造的解消（横長Autoサイズ化） —— **確認済み・妥当**

旧`ElementPlacementDialog`由来の固定サイズ制約（`Width=320`相当の名残）がなくなり、`ElementPlacementBar`は
`HorizontalAlignment/VerticalAlignment=Left/Top`＋内容依存のAutoサイズ（Border+Padding+横並びStackPanel）に
変わった。OK/キャンセルボタンを収める固定幅の制約が構造的に無くなっており、旧不具合の再現要因は解消されて
いると判断する。

### (d) ラッパーGrid一元化の非退行 —— **IsEnabledスコープは非退行／ただし座標系副作用が発生**

`MainContentArea`（`Grid.Row="0" Grid.RowSpan="4"`）は旧4箇所（Menu/ToolBarTray/メイン作業域Grid/
OutputPanelArea、ルートRow0-3）を過不足なく束ねており、`ElementPlacementBar`・`StatusBar`がラッパー外の
兄弟要素のままである点も維持されている。**IsEnabledによる無効化スコープそのものは旧4箇所バインドと
完全に一致し、退行はない。**

ただし、このラッパー導入が「ルートGridのAuto行（Row0/1/3）を単独専有する子要素の消失」という副作用を
生み、それが(a)で述べた座標系不整合の直接の原因になっている。**一元化そのものは正しいが、同一コミットで
座標計算（TranslatePoint方式）を導入した際に、この副作用を検証していなかったことが今回の欠陥の実質的な
原因**と考える。

### (e) 増分1設計の堅持（IsPlacementBarVisible単一情報源・ClosePlacementBarフォーカス集約・個別ガード禁止） —— **確認済み・維持されている**

差分は`IsPlacementBarVisible`を引き続き単一の真実源として使っており、個別ガードの追加や`ClosePlacementBar`
のフォーカス集約構造への介入は見られない。増分1で確立した設計は増分2でも崩れていない。

### (f) プレースホルダ小アイコンのTab順・UIAツリー汚染 —— **CONFIRMED（軽微・修正推奨）**

新規追加の2つのプレースホルダアイコンボタン（`AutomationProperties.Name="(未定アイコン1)"`/`"(未定アイコン2)"`、
XAML479-488行目）は、`Click`ハンドラを持たず、かつ`Focusable`/`KeyboardNavigation.IsTabStop`のいずれも
明示的に`False`へ設定していない（WPF既定＝両方`True`のまま）。同一ファイル内には`GridSplitter`に対して
`KeyboardNavigation.IsTabStop="False"`を明示する既存パターン（XAML300/332/357行目、「IsTabStop=Falseはタブ
オーダーからの除外のみ」というコメント付き）が既に確立されているにもかかわらず、この2ボタンには踏襲されて
いない。

キーボードファースト（本プロジェクトの主眼、`CLAUDE.md`）の観点から、Tabキーでバー内を移動すると、
押しても何も起きない2つのボタンに迷い込む「Tab迷子」が生じる。修正は容易（`Focusable="False"`または
`KeyboardNavigation.IsTabStop="False"`の追加のみ）であり、増分3（拡張表示ボタン、機能実装予定）とは異なり、
この2ボタンは恒久的に非機能のプレースホルダである点でも対応の優先度は高いと考える。

---

## `code-review`スキル併用の追加所見（high、8角度finder→verify）

| # | 判定 | 内容 |
|---|------|------|
| 1 | **CONFIRMED（重大・上記(a)と同一）** | 座標系不整合。finderが実機起動（`dotnet run`）まで踏み込み、新規ドキュメント作成→a接点配置(F5)→行1/列1セル選択で、選択セルより配置バーが明確に上に表示されることをスクリーンショットで確認済み。 |
| 2 | PLAUSIBLE | 右母線列（`Column==grid.Columns`）でカーソルが位置する状態でF5等を押すと、`CellRectDip`が返す「右母線の外側の余白矩形」をそのままバー位置計算に使ってしまい、選択セルとの視覚的対応が崩れる（既存コードのコメントで自認されている「余白矩形」概念を`PositionPlacementBar`が考慮していない）。 |
| 3 | CONFIRMED | 配置バーの種別ComboBox・デバイス名TextBoxから可視ラベル（旧TextBlock「種別:」「デバイス名:」）が削除され、`AutomationProperties.Name`のみに置換された。UIA経由でしか読み上げられず、晴眼ユーザー向けの視覚的ラベルという不変条件が失われている。構想図踏襲による意図的な意匠変更の可能性もあり、家老・殿へ確認要（要修正か仕様容認かの判断）。 |
| 4 | CONFIRMED | 種別選択ComboBoxの幅が200→90に縮小され、`DisplayMemberPath=Definition.Name`が長い部品名で見切れる懸念（TextTrimming等の対策なし）。 |
| 5 | CONFIRMED（軽微） | `MainContentArea`導入でネスト1段深くなったが、XAMLインデントが据え置きで可読性がやや低下。 |
| 6 | CONFIRMED（軽微） | 新規プレースホルダアイコン2個が既存共有Style（`ToolBarButtonStyle`等）を使わずWidth/Height/Padding等を個別ベタ書き。 |
| 7 | CONFIRMED（軽微） | 2個のプレースホルダアイコンのPath Dataバウンディングボックス高さが不揃い（11 vs 10）で、`Stretch=Uniform`適用時に見た目のスケールがわずかに不揃いになる。既存コメント（180-183行目）が明記する「対応するグリフの境界ボックスを揃える」手当てが引き継がれていない。 |
| 8 | PLAUSIBLE（評価分かれる） | `Measure`明示呼び出し後の`Margin`変更で二重レイアウトパスが技術的には発生するが、Visibility変更直後の位置決めとしてはWPFの定石であり実害は軽微。 |
| 9 | CONFIRMED（上記(b)と同一） | `SizeChanged`等の再配置フックが存在せず、バー表示中のウィンドウリサイズに追随しない。 |
| 10 | CONFIRMED（上記(b)と同一） | `Math.Clamp`は選択セル自体との非重なりを保証しない。 |

Angle C（呼び出し元/呼び出し先トレース）・Reuse・Conventionsの3角度は該当なし（空リスト）。

---

## P-024（範囲外セル配置）関連の確認

家老依頼により、範囲外セル（負の行等）が選択された状態で配置操作を行った場合の`PositionPlacementBar`の
挙動を確認した。

- **REFUTED**（例外・NaN・Infinity発生説）：`GridGeometry.X/YRow`は範囲チェックのない単純算術、`CellRectDip`の
  `Rect`コンストラクタも負のX/Yを許容、`Math.Clamp`も`min=0≦max`が常に成立するため、負の入力値でも例外は
  起きない。
- **CONFIRMED**：実害は「バーが左上(0,0)近傍へ不自然にクランプ表示される」という視覚的異常に留まる。
- **PLAUSIBLE**：`LadderCanvas.ToGridPos`（`Math.Floor`ベース）も範囲チェックを持たず、グリッド左上の
  マージン領域をクリックすると負の`GridPos`が生成されうる。これがP-024の混入経路の一つである可能性が
  あるが、P-024の根本原因特定自体は本レビューの範囲外（別途の調査事項）とする。

---

## 総評・推奨

- **要修正（重大）**: (a)座標系不整合。`PositionPlacementBar`が`TranslatePoint(point, MainWorkAreaGrid)`の
  結果を、実際には異なる座標系を持つ`ElementPlacementBar`の`Margin`へそのまま適用している。修正方針の一案
  （侍の技術裁量）：`TranslatePoint`の変換先を`ElementPlacementBar`の実際の親（ルートGridまたは
  `MainWindow`自身）に揃える、または`ElementPlacementBar`自体を`MainWorkAreaGrid`（あるいは
  `MainContentArea`）の内部へ再配置し、両者が同一座標系を共有するようにする。
- **要修正（中）**: (f)プレースホルダアイコン2個への`Focusable="False"`/`IsTabStop="False"`付与。
- **経過観察／家老・殿確認**: (b)クランプがセル自体を覆い隠す可能性、所見3(可視ラベル削除、意図的か)、
  所見4(ComboBox幅縮小)、所見9(ウィンドウリサイズ非追随)。
- **忍者への申し送り（(a)修正後）**: 症状4（非決定性）の原因切り分けには、`PositionPlacementBar`呼び出し
  時点の`MainWorkAreaGrid.ActualWidth/Height`・`ElementPlacementBar.DesiredSize`・スクロールオフセットを
  診断ログへ出力し実測することを推奨する（過去プロジェクトの教訓＝コード推論のみでの往復が長引いた際は
  診断ログ注入が有効、`docs-notes/`各種教訓参照）。

**忍者の実機確認は(a)の修正後に回されたい。** (a)未修正のまま実機確認しても、家老指定観点(a)(b)(d)は
全て不合格が確実であり、手戻りになる。

---

## 出典・参照

- 対象コミット `8fd60f6`（`git show`で全差分確認、`git diff 8fd60f6~1..8fd60f6`）
- `src/Ecad2.App/MainWindow.xaml`（全文確認、80/90/269/404/437/460-498行目付近）
- `src/Ecad2.App/MainWindow.xaml.cs`（599-654行目、`TryPlaceElement`/`PositionPlacementBar`）
- `src/Ecad2.App/Converters/InverseBooleanConverter.cs`
- `src/Ecad2.App/Views/LadderCanvas.cs`（`CellRectDip`/`ToGridPos`/`MmToDip`）
- `src/Ecad2.Core/Rendering/GridGeometry.cs`・`DiagramRenderer.cs`（`CellMm=9.0`既定値）
- `docs/ecad2-t033-implementation-plan-samurai.md`5節（忍者検証観点）
- `docs/ecad2-t033-review-onmitsu-2.md`（増分1再レビュー、往復1周目クリーン）
- `docs/ecad2-t021-increment-v-review-340f53d-onmitsu.md`（クランプ・座標変換の教訓参照元）
- 独自実行: `dotnet build src/Ecad2.sln`（0エラー）・`dotnet test src/Ecad2.sln`（24件合格、12+12）
- `code-review`スキル（high、8角度finder→verify、CONFIRMED7・PLAUSIBLE3・該当なし3角度）
- finderサブエージェントによる実機起動（`dotnet run --project src/Ecad2.App`）確認・スクリーンショット
  （所在: 隠密セッションのスクラッチパッド、正式証跡化は必要なら忍者へ引き継ぎ）
- 家老経由の殿実機目視報告4件（2026-07-06 18:03〜18:09 peer message、症状1〜4）
