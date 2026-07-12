# GuiEcad仕様書：設計チェック(DRC)・出力パネル

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）のDRC・出力パネル実装をExplore委譲調査で纏め、
`docs/spec/ecad2-spec-drc-output.md`（ecad2側、T-075起票）と比較可能な形で整理する。

対応するecad2側仕様書：`docs/spec/ecad2-spec-drc-output.md`

---

## 1. DRC/接続検査の実行方法・チェック項目

GuiEcadには**メニュー「ツール」自体が存在しない**（`guiecad-spec-menu-toolbar.md`既確認）。DRCは
出力パネル内の**2個の独立ボタン**からのみ実行され、ecad2の単一「設計チェック実行」に相当する
一本化コマンドは存在しない：

| ボタン | ハンドラ | 対象範囲 | 呼び出す検査 |
|---|---|---|---|
| 「回路チェック」（回路エラータブ） | `OnRunDrc`（`MainPage.Drc.cs:30-71`） | 文書全体（全シート） | `CheckCrossReference`(XREF-001/002/003)＋`CheckDeviceTypeConsistency`(TYPE-001/002)＋シート毎`CheckSeriesCoils`(LOAD-003) |
| 「接続検査実行」（接続検査タブ） | `OnRunConnectivity`（`MainPage.Drc.cs:122-137`） | **現在表示中の1シートのみ** | `CheckVerticalCrossings`(CONN-001)＋`CheckLoadReachability`(LOAD-001/002)＋`CheckSeriesCoils`(LOAD-003、重複実行) |

`DesignRuleCheck.cs`（`GuiEcad.Core/Simulation/DesignRuleCheck.cs:1-290`）自体はecad2側とほぼ同一の
検査ロジック（クラス名・メソッド名・コード体系・母線BFSまで一致）だが、**`CheckUnresolvedPartId`
（`DRC-PART-001`）に相当する検査は存在しない**——ecad2側T-052（未解決PartIdフォールバック警告）は
GuiEcad後の追加機能であることと整合する。

**紛らわしい同名別実体**：ツールバー「接続検査」ToggleButton（`MainPage.xaml:212`、
`OnConnectivityToggle`）は`ConnectivityChecker.Check`（`Simulation/ConnectivityChecker.cs:29-46`）を
呼ぶ**別機構**で、配線色分け（青=接続／黒=未結線スタブ）専用。「接続検査実行」ボタン（診断一覧
生成）とは名前が同じでも実体が異なる——`DesignRuleCheck.cs`内に`ConnectivityChecker`参照は0件。
この「DRC診断とConnectivityCheckerが完全独立」という構造自体はecad2仕様書と一致する。

---

## 2. 検出結果の表示UI

出力パネルは`TabView`（`MainPage.xaml:680-758`）で3タブ構成：「回路エラー」（`DrcListView`）／
「検索結果」／「接続検査」（`ConnectivityListView`）。

**ecad2のDataGrid5列構成ではなく、単一列の`ListView`に整形済み文字列を流し込む方式**：

```
_lastDrcResults.Select(FormatDiagnostic).ToList();   // MainPage.Drc.cs:64
```

`FormatDiagnostic`（`MainPage.Drc.cs:164-176`）は`"[E] DRC-XXX [P1 行2]  message"`形式の1文字列に
重大度・コード・場所・メッセージを全て埋め込む。重大度は`E`/`W`/`I`の1文字略記で、**色分けはない**
（`ItemTemplate`未定義、grep確認0件）。これはecad2側のT-018裁定記録（「GuiEcad側の素朴なListView
文字列整形案（採用せず）」）と実装が完全に一致する——**ecad2はGuiEcad方式をあえて不採用にした**
という経緯がGuiEcad側の実装からも裏付けられた。

---

## 3. ジャンプ機能（「回路エラー」タブのみ、範囲限定）

- `DrcListView`の`SelectionChanged="OnDrcItemSelected"`（`MainPage.xaml:723`）で選択診断の
  **1件目のLocationのみ**（`diag.Locations[0]`、ecad2のような複数箇所リストではない）へ
  `SwitchToSheet`＋`CenterViewOnRow`でジャンプ（`MainPage.Drc.cs:73-95`）。
- **「接続検査」タブには`SelectionChanged`ハンドラ自体が無い**——クリックしても該当箇所へジャンプ
  しない。
- ハイライトはecad2の「該当セル選択（`SelectedCell`）」ではなく、**行全体を半透明オレンジで塗る帯**
  （`DrawDrcRowHighlight`、`MainPage.Drawing.cs:252-261`）。要素単位のピンポイント選択はしない。
- **キャンバス1回クリックで即座に消える**（`MainPage.Pointer.cs:145-150`）、シート切替でもリセット
  （`MainPage.Sheets.cs:103`）。ecad2の永続的な選択状態とは仕組みが異なる。

---

## 4. 自動実行のタイミング（両ボタンとも完全手動、ecad2と同じ）

`OnRunDrc`/`OnRunConnectivity`の呼び出し箇所はボタンの`Click`バインディング1箇所ずつのみ。保存時・
要素編集時・シート追加削除時・Undo/Redo時のいずれにも自動実行トリガーは存在しない。

出力パネル自体は**既定で折りたたみ状態**（`_outputPanelCollapsed=true`、`MainPage.xaml.cs:229`）、
DRC/検索/接続検査の実行時のみ固定130px高さへ自動展開（`ExpandOutputPanel()`、`MainPage.Drc.cs:69,135`）
——これは「DRC自体の自動実行」ではなく「パネル展開の自動化」。

---

## 5. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | 出典 | 備考 |
|---|---|---|
| 「回路チェック」/「接続検査実行」2ボタン分割（対象範囲も異なる） | `MainPage.Drc.cs:30,122` | ecad2は単一`RunDrcCommand`で全8種を毎回一括実行 |
| 出力パネルの既定折りたたみ＋実行時自動展開(固定130px) | `MainPage.xaml.cs:229-230` | ecad2はGridSplitterで常時表示・手動ドラッグ調整（T-059） |
| ツールバー「接続検査」ToggleButton（配線色分け、`ConnectivityChecker`使用） | `MainPage.xaml:212` | 「接続検査実行」ボタンと同名だが実体が異なる紛らわしさあり |
| DRCハイライトがキャンバスクリック1回で即消える | `MainPage.Pointer.cs:145-150` | ecad2の選択状態はより永続的 |

### (2) ecad2のみにある機能

| 機能 | ecad2側出典 | GuiEcad側の状況 |
|---|---|---|
| `DRC-PART-001`（未解決PartIdフォールバック警告） | T-052 | `DesignRuleCheck.cs`に該当なし（GuiEcad後の追加機能） |
| メニュー「ツール」→「設計チェック実行」の単一エントリポイント | ecad2仕様書 | GuiEcadに「ツール」メニュー自体が存在しない |
| DataGrid5列構成＋重大度色分け(Error赤/Warning橙/Info灰、太字) | ecad2仕様書 | 単一列ListView、色分けなし |
| 診断行クリックでの機器名一致要素・`SelectedCell`精密ジャンプ | ecad2仕様書 | 行全体の帯ハイライトのみ、要素単位選択なし |
| 全8種検査の単一パネル集約 | ecad2仕様書 | 「回路エラー」/「接続検査」タブに分離、後者は現在シートのみ |
| GridSplitterによる出力パネル高さドラッグ調整（T-059） | ecad2仕様書 | 固定130pxへのアニメーション展開のみ |

### (3) 両方にあるが挙動が異なる点

| 項目 | ecad2 | GuiEcad |
|---|---|---|
| 実行トリガー | メニュー1箇所 | 出力パネル内ボタン2個 |
| 検査範囲・粒度 | 1回で全8種・全シート一括 | 「回路チェック」=文書全体だがXREF/TYPE/SeriesCoilsのみ、「接続検査実行」=現在シートのみ |
| 結果表示 | DataGrid5列、色分け | ListView単一列、略記文字列、色分けなし |
| ジャンプ機能 | 複数`Locations`対応、`SelectedCell`同期 | 「回路エラー」のみ`Locations[0]`限定、「接続検査」はジャンプ不可 |
| ハイライト永続性 | 次操作まで保持 | クリック1回・シート切替で即消滅 |
| 結果クリアタイミング | `ReplaceDocument`/Undo/Redo時に明示`ClearResults()` | 明示クリア箇所なし（次回ボタンクリックまで前文書の古い診断が残存する可能性） |
| 出力パネル表示状態 | 常時表示 | 既定折りたたみ、実行時のみ自動展開 |

---

## 出典

- GuiEcad: `GuiEcad.Core/Simulation/DesignRuleCheck.cs:1-290`、`GuiEcad.Core/Simulation/ConnectivityChecker.cs:29-46`、
  `GuiEcad.App/MainPage.xaml:212,680-758`、`MainPage.Drc.cs:30-176`、`MainPage.Drawing.cs:252-261`、
  `MainPage.Pointer.cs:145-150`、`MainPage.Sheets.cs:103`、`MainPage.xaml.cs:229-230,715-719`
  （Explore委譲調査、行番号は本文各所参照）
- ecad2: `docs/spec/ecad2-spec-drc-output.md`（比較対象）

## 不明点

- 新規作成／開く操作時に診断結果一覧が明示クリアされる処理はgrep0件だったが、実機での残存挙動は
  静的コード確認のみで未検証。
- DRC実行系のショートカット割当有無（`MainPage.KeyBindings.cs`19コマンド一覧に含まれない点は
  前回調査記録で確認済みだが、本調査では再確認していない）。
- `ConnectivityChecker`と`DesignRuleCheck`の判定ロジックの境界条件一致/不一致は未検証（両者は目的が
  異なる意図的な別実装と見られるが確証なし、推測）。
