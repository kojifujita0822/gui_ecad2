# T-047（手動配線系F9/F10のツールバーボタン作成）先行調査（隠密）

> 2026-07-09 隠密調査。起票=殿指示2026-07-09（`docs/todo.md:64`）、着手順＝T-029取り止めにより
> 繰り上げ。調査のみ・src/tests書き込みなし。目的：既存構造の把握とUI/UX分岐点の整理を
> 殿へ提示し、裁定を仰ぐ。

---

## 結論（先出し）

- 現状、手動配線4種（FreeLine横線=F9・縦線=Shift+F9・VerticalConnector=Shift+F9・
  WireBreak/ConnectionDot=F10）は**キーボード専用**で、ツールバーに対応ボタンが無い
  （`docs/archive/ecad2-t045-increment-d-ninja-verification.md`§1でも明記済み）。
- **重要な仕様事実**：Shift+F9とF10はいずれも**1つのキーで、シート種別（主回路/制御回路）に
  応じて挙動が変わる**（Shift+F9＝制御回路なら縦コネクタ・主回路なら自由線縦線。F10＝主回路なら
  接続点・制御回路なら配線分断）。ボタン化にあたり「1キー1ボタン（挙動が動的に切替わる）」か
  「1キー2ボタン（機能ごとに別ボタン、非対応シートでは無効化）」かが最大のUI/UX分岐点（4節）。
- GX Works3の参考画像（`docs/images/t040-gx-ladder-toolbar-reference.png`）を実見したところ、
  **F9/Shift+F9のボタンは既存の接点・コイル配置ボタン群と同一ツールバー行内に、区切り線1本を
  挟んで隣接配置**されている（2節）。ecad2の既存7ボタン＋T-040 GX様式意匠を延長する形での
  追加が、この前例とも整合しやすいと考える。

---

## 1. 現状把握

### 1-1. 既存ツールバーの実装構造（T-026の7ボタン＋T-040 GX様式意匠）

`src/Ecad2.App/MainWindow.xaml:173-269`の`<ToolBar Band="1" BandIndex="0">`内、7ボタン
（選択/a接点F5/ORa接点sF5/b接点F6/ORb接点sF6/コイルF7/端子台F8/自作パーツ）。各ボタンは：

```xml
<Button Style="{StaticResource PlacementToolBarButtonStyle}" ToolTip="a接点配置 (F5)"
        AutomationProperties.Name="a接点配置 (F5)"
        Tag="a接点" Click="BuiltinPlaceButton_Click"
        PreviewKeyDown="ToolButtonPreviewKeyDown"
        IsEnabled="{Binding HasProject}">
    <StackPanel>
        <Path Style="{StaticResource PlacementToolBarIconStyle}" Data="..."/>
        <TextBlock Style="{StaticResource PlacementToolBarKeyLabelStyle}" Text="F5"/>
    </StackPanel>
</Button>
```

という共通構成（Path Geometryアイコン＋キー凡例TextBlockのStackPanel、`IsEnabled="{Binding
HasProject}"`で全ボタン共通ガード）。`Tag`（"a接点"等）は`BuiltinPlaceButton_Click`
（MainWindow.xaml.cs:1074-1085）→`ActivateBuiltinTool`（1059-1066）で解釈され、
`Tool = ToolState(PlaceElement, PartId, IsOr)`をセットする単一経路。

T-040の意匠指針はXAMLコメント（174-187行目）に集約：GX Works3様式（基本キー直後にShift版を
並べる並び順、記号意匠は殿の図示指定、線の太さ`StrokeThickness=1.0`、境界ボックス統一等）。
キーボードショートカット（F5-F8、MainWindow.xaml.cs:704-727）は`TryPlaceBuiltin`（1143-1152）を
呼ぶ独立経路（ボタンとは別コードパス、`HasProject`を明示ガードする点もボタンの`IsEnabled`
連動と役割が異なる）。

### 1-2. 手動配線4種のキー→ToolMode→実処理の経路

F9/Shift+F9/F10はいずれも`Window_PreviewKeyDown`（MainWindow.xaml.cs:704〜）内で処理され、
**上記のボタン共通経路（`ActivateBuiltinTool`）を通らない**：

```csharp
case Key.F9 when noModifier:
    // 主回路シート限定。制御回路シートでは当面未使用(自動横配線があるため)。
    TryBeginFreeLineDraft(horizontal: true);
    break;
case Key.F9 when shift:
    // シート種別で対象が切替わる。制御回路→縦コネクタ、主回路→自由線(縦線)。
    if (_viewModel.CurrentSheet is Sheet sf9Sheet && sf9Sheet.MainCircuit)
        TryBeginFreeLineDraft(horizontal: false);
    else
        TryBeginConnectorDraft();
    break;
case Key.System when noModifier && e.SystemKey == Key.F10:
    // F10もシート種別で対象が切替わる(制御回路→配線分断、主回路→接続点)。
    // F10はWin32のWM_SYSKEYDOWN扱い(WPF既知仕様)でe.Keyでなくe.SystemKeyに入る特殊ケース
    // (忍者実機発見バグの修正済み対処、コメント744-752行目に詳述)。
    if (_viewModel.CurrentSheet is Sheet f10Sheet && f10Sheet.MainCircuit)
        TryPlaceConnectionDot();
    else
        TryPlaceWireBreak();
    break;
```

各Try系メソッド（`TryBeginConnectorDraft`1156／`TryPlaceWireBreak`1192／
`TryBeginFreeLineDraft(bool horizontal)`1219／`TryPlaceConnectionDot`1259）はいずれも
引数なし（`TryBeginFreeLineDraft`のみ`horizontal`引数）。既存ボタンの`Tag`文字列解釈方式
（`ActivateBuiltinTool(partName, isOr)`）とはシグネチャが異なるため、**ボタン化する場合は
既存の`BuiltinPlaceButton_Click`をそのまま流用できず、新規Click ハンドラ（または
Tag経由で4メソッドへ振り分けるディスパッチャ）が要る**（技術的な実装詳細であり、実装時の
侍の技術選択事項と考える）。

キー操作フロー自体（線系＝SelectedCellから開始→矢印キーでリアルタイムプレビュー調整→
Enter確定/Esc取消。点系＝F10押下と同時に即時記入）は`docs/archive/ecad2-t041-key-flow-proposal-samurai.md`
の記述と実装が一致することを確認済み。ボタン押下でもこの後続フロー自体は変更不要
（ボタンは「モード開始のトリガー」をキーの代わりに提供するだけ）と考えられる（推測）。

### 1-3. シート種別（主回路/制御回路）によるツール割当の仕組み

`Sheet.MainCircuit`（`src/Ecad2.Core/Model/Sheet.cs:27`、bool、「主回路（動力回路）モード:
左右母線・母線名・自動横配線を描かず、自由直線で結線する」とドキュメントコメントあり）。

切替ロジックは1-2節のとおり`Window_PreviewKeyDown`内で`_viewModel.CurrentSheet.MainCircuit`を
都度判定する形（専用の切替プロパティやコマンド分岐機構は無く、各キー処理内で個別にif分岐）。

**ボタン活性制御に使える既存状態プロパティ**：`HasProject`
（`MainWindowViewModel.cs:145`、`Document.Sheets.Count>0`が実体）が全既存ボタンの
`IsEnabled`バインド先。**シート種別（`CurrentSheet.MainCircuit`）に連動する既存の
ViewModelプロパティは見当たらなかった**（事実：grep・目視確認で不在を確認）。新規ボタンで
シート種別に応じた活性制御をしたい場合、`MainWindowViewModel`に`CurrentSheet.MainCircuit`を
参照する新規バインド用プロパティ（または`CurrentSheetIndex`変更時に発火する形）を追加する
必要がある（推測、実装時の技術選択）。

---

## 2. GX Works3の参考情報

### 2-1. 参考画像の直接確認（新規知見）

`docs/images/t040-gx-ladder-toolbar-reference.png`を実見した。「ラダー」ラベル付きの
ツールバーに、接点・コイル配置系アイコン（F5/sF5/F6/F7とキー凡例が付されたアイコン群）が
並び、**区切り線（separator）を1本挟んだ直後にF9・sF9とキー凡例が付された配線系アイコンが
同一ツールバー行内に連続配置**されている。さらにもう一段の区切り線の先に、検索・PLC書込等の
配線と無関係な機能アイコン、および末尾に色味の異なるアイコン（`docs/archive/ecad2-t041-manual-wiring-survey-onmitsu.md:126-128`が「削除系と推測されるが未検証」と既に指摘している箇所）が続く。

**画像解像度の制約により、各アイコンの図柄そのもの（線の引き方の細部）までは断定できない**が、
「F9/sF9が既存の配置系ボタンと同じツールバー行に、区切り線のみを挟んで並ぶ」という
**配置構成（レイアウト上の位置づけ）は明確に確認できた**。F10相当・接続点相当のアイコンの
有無は、この画像内では確証が持てなかった（不明）。

### 2-2. 既存調査書の記述

- `docs/ecad2-gxworks3-uiux-survey-onmitsu-part2.md:18,27-28`：ラダー編集時はツールバーが
  2段構成（1段目=汎用操作、2段目=ラダー専用コマンド）、各アイコン下にファンクションキー番号
  （F5,F6,F7...Shift+F5等）が直接明記される、という「新発見」の記述。ecad2のT-040意匠
  （アイコン下にキー凡例テキストを直接添える構成）はこの慣行を踏襲している。
- `docs/archive/ecad2-t040-wire-survey-onmitsu.md`（3-4行目付近）：「GXツールバーF9(横線)/Shift+F9(縦線)
  相当」という殿要望の説明として触れるのみで、GX Works3実機UIそのものの検証記述はない
  （同ファイル本体はecad2/GuiEcad側コード調査が主）。
- GX Works3の手動配線ボタンの図柄・アイコンデザインそのものに関する文章記述は、指定調査書群には
  見つからなかった（2-1節の画像直接確認が現時点で最も具体的な情報源）。
- T-040自体の意匠決定事項（実際の見た目・構成の詳細）は`docs/todo.md:57`に完了記録のみで、
  個別の意匠仕様は主にXAMLコメント（1-1節参照）に残っている。

---

## 3. 既存の「消去」機能との関係（範囲確認）

T-041増分1（`docs/todo.md:133`「選択モデル+Delete統合」）で、配線プリミティブの選択
＋Deleteキーによる削除は**既に実装済み**。GX Works3参考画像末尾の推測「削除系アイコン」
（2-1節）はGX Works3側が独自に持つ専用消去ツールの可能性を示すのみで、**ecad2は既に
異なる方式（選択+Delete）を確立済み**のため、T-047はF9/Shift+F9/F10の「記入モード開始」
ボタンの新設に限定してよく、別途消去専用ボタンを新設する必要はないと考える（推測、
T-041の既存合意事項との整合）。

---

## 4. UI/UX分岐点（殿への選択肢提示用）

### 分岐点A：ボタン数と構成【最重要】

Shift+F9とF10はそれぞれ1キーでシート種別により挙動が変わる（1-2節）。ボタン化の粒度は
2案が考えられる：

- **案A-1（1キー1ボタン・動的切替）**：F9／Shift+F9／F10の**3ボタン**を新設。各ボタンは
  現在のシート種別に応じてツールチップ・アイコン・呼び出し先メソッドが動的に変わる
  （キーの挙動をそのままボタンに投影）。ボタン数は最小だが、「押すと何が起きるか」が
  シートによって変わる点はF9キー自体の既存仕様と同じ体験になる。
- **案A-2（1機能1ボタン・シート限定）**：FreeLine横線(F9)／FreeLine縦線(主回路限定)／
  VerticalConnector(制御回路限定)／ConnectionDot(主回路限定)／WireBreak(制御回路限定)の
  **最大5ボタン**を新設し、非対応シートでは分岐点Bの方式で無効化/非表示にする。各ボタンの
  意味が固定でツールチップも常に同じになる分わかりやすいが、ボタン数が増え、Shift+F9・F10
  それぞれ2ボタンが同じキー凡例（sF9/F10）を共有する形になる。

### 分岐点B：非対応シート種別のボタンの扱い

案A-2を採る場合、あるいは案A-1でも「押しても無効」を明示したい場合に関係：
- **非活性化（グレーアウト）**：`HasProject`と同じ`IsEnabled`パターンの延長。ボタンは
  常に見えるが押せない。既存の実装パターンをそのまま延長でき、実装コストは低い。
- **非表示**：シート切替のたびにツールバーの構成が変わる。GX Works3の参考画像（2-1節）は
  F9/sF9を常時表示する構成に見え、非表示方式の前例は確認できなかった（不明）。
- **切替表示**：分岐点A案A-1を採る場合に対応。1ボタンの見た目自体がシート種別で変わる
  （アイコン・ツールチップが動的に変化）。

### 分岐点C：アイコン意匠

- 新規アイコン（横線・縦線・縦分岐・接続点・配線分断）の図柄をGX Works3参考画像
  （2-1節、ただし解像度により細部不明）に寄せるか、ecad2独自の意匠（T-040で確立した
  `StrokeThickness=1.0`・境界ボックス統一等の様式）で新規に起こすかの選択。
- 接続点（●）・配線分断のように「線ではなく点や記号」を象徴するアイコンは、既存7ボタンの
  「配置する記号そのものを模したアイコン」という慣行（例：コイル配置ボタン＝実際のコイル
  記号に似た円弧）にどこまで寄せるか。

### 分岐点D：キーバインド表示の一貫性

- 既存の`PlacementToolBarKeyLabelStyle`（アイコン下にF5等の短縮キー表記）をそのまま踏襲するか。
- 分岐点Aで案A-1（動的切替）を採る場合、キー凡例表示（"sF9"等）は固定のままでよいか
  （挙動は変わるがキー自体は変わらないため、表示上の矛盾は生じない、と考える＝推測）。

### 分岐点E：ツールバー内の配置位置

- 既存7ボタンの末尾（自作パーツボタンの後）に区切り線を挟んで追加するか、独立した新規
  ツールバー行（Band）として分離するか。GX Works3参考画像（2-1節）は「同一行内に区切り線
  のみで隣接」という構成であり、これに倣うなら既存`<ToolBar Band="1" BandIndex="0">`の
  延長（自作パーツボタンの後に区切り線＋新規ボタン群）が最も前例と整合する（推測）。

---

## まとめ

- 既存基盤：ボタン共通スタイル（`PlacementToolBarButtonStyle`等）・`HasProject`ガード
  パターンはそのまま流用可能。ただし手動配線4種のTry系メソッドは既存の`ActivateBuiltinTool`
  経路と別系統のため、新規Click ハンドラの追加が実装時に必要（技術選択事項）。
- シート種別（`Sheet.MainCircuit`）に連動するボタン活性制御プロパティは現状ViewModelに
  存在せず、新設が必要。
- GX Works3参考画像を直接確認し、「F9/sF9は既存配置ボタン群と同一ツールバー行に区切り線を
  挟んで隣接配置」という前例を新たに得た（2-1節）。
- UI/UX分岐点は5件、うち**分岐点A（ボタン数と構成＝1キー1ボタン動的切替 vs 1機能1ボタン
  シート限定）が最重要**で、分岐点B（非対応時の扱い）と密接に連動する。殿の裁定を仰ぎたい。
