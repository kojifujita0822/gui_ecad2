# T-047 GX Works3実プログラムのラダーアイコン資源調査（隠密）

> 2026-07-09 隠密調査。殿直接依頼（`C:\Program Files (x86)\MELSOFT`配下）。目的：T-047
> （F9/F10系ツールバーボタン作成）の意匠参考資料の収集。調査のみ・Program Files配下は
> 読み取り専用で扱い、書き込み・改変は一切行っていない。

**【著作権に関する注記・MUST遵守】** 本調査で得た情報は、三菱電機の著作物であるGX Works3の
**意匠・構図の参考・検分に限定**する。アイコンそのものの流用・改変流用は行わない。ecad2の
アイコンはT-040様式に基づき侍が自作起草する既定方針は本調査によって変わらない。

---

## 結論（先出し）

- **MELSOFT配下にGX Works3は`GPPW3`ディレクトリとして実在**（1節）。
- **ラダー記号（接点・コイル・OR・手動配線等）のツールバーアイコンに該当する、抽出可能な
  画像アセット（bmp/png/ico等）は見つからなかった**（2・3節、887ファイル中の全数確認＋
  135DLLの埋め込みリソース横断調査を実施した上での結論）。
- 見つかった画像アセットは「コピー/切取/貼付/元に戻す/コメント/検索」等の**汎用編集コマンド
  アイコン**のみで、記号配置系（F5〜F10相当）のものではない。
- **推測**：GX Works3は記号配置系のツールバーボタンをビットマップ画像としてではなく、
  **プログラムによるベクター描画（カスタム描画）**で表示していると考えられる（技術的根拠は
  4節）。これはecad2自身が採用している方式（Path Geometryによるアイコン描画、T-040）と
  同じ発想であり、意匠参考としては「抽出できる画像アセット」ではなく、既存の
  `docs/images/t040-gx-ladder-toolbar-reference.png`（スクリーンショット）が引き続き
  最も具体的な視覚参考情報となる。

---

## 1. MELSOFT配下のディレクトリ構造

`C:\Program Files (x86)\MELSOFT\`直下に14製品ディレクトリ（DNaviZero／Easysocket／
e-Manual Viewer／GNavi／**GPPW3**／MNavi／MNCforEIP／MNCforEIP(F)／MRC2／MSF／NNavi／
PMCNF／SNavi）。このうち**`GPPW3`がGX Works3本体**（実行ファイル`GXW3.exe`、
`Melco.GXW3.*`名前空間の各種DLL群から確認）。

`GPPW3`配下は機能別プラグイン構成（`WorkWindowPlugin/Ladder/`＝ラダーエディタ本体、
`DialogPlugin/`＝各種ダイアログ、`CommandPlugin/`＝拡張コマンド、`Service/Managed/`＝
比較・変換等のサービス層）。ラダーエディタの中核は
`WorkWindowPlugin/Ladder/LadderEditor.dll`（.NET/Monoアセンブリ、`file`コマンドで
"Mono/.Net assembly"と確認）。本体`GXW3.exe`はPE32ネイティブ実行ファイル（.NETではない、
おそらくブートストラップ/ローダー）。

## 2. 画像リソースの直接ファイル探索

`GPPW3`配下の画像ファイル（bmp/png/ico/svg）は**887件**。「ladder」「toolbar」「contact」
「coil」「circuit」「wire」「edit」でファイル名を絞り込んだところ、ヒットしたのは
CC-Link IEフィールド関連のユニット状態アイコン（`4B_Wireless_module*.bmp`等）や
レシピ機能のアイコン（`ConditionDataEditor.bmp`等）のみで、**ラダー記号系アイコンとの
関連は無い**。

`Icon`/`Image`という名のディレクトリも探索したが（`Recipe/Online/ConnectionSetting/Bmp/
SysImage`・`Recipe/ProfileData/Icon`・`SampleData/Image`）、いずれもCC-Link設定・レシピ・
サンプルデータ用で、ラダーエディタのツールバーとは無関係と判断した。

## 3. EXE/DLL埋め込みリソースの調査

### 3-1. LadderEditor.dll（ラダーエディタ本体）

.NETリフレクション（`Assembly.LoadFile`→`GetManifestResourceNames()`）で埋め込みリソースを
列挙。全66件のうち画像系（bmp/png/ico拡張子）は28件：

```
CoilSearch.bmp, Comment.bmp, Convert.bmp, Copy.bmp, Cut.bmp, DevSearch.bmp,
DisplayDevice.bmp, InlineSTBox.bmp, InstSearch.bmp, LadderExport.bmp, LadderImport.bmp,
MonitorDeviceAll.bmp, MonitorStop.bmp, NopTmpApply.bmp, NopTmpExec.bmp, NopTmpReset.bmp,
NopTmpStatementView.bmp, OffMonFind.bmp, Paste.bmp, Redo.bmp, RegistLabel.bmp,
Template.bmp, TemplateLeft.bmp, TemplateRight.bmp, UnComment.bmp, Undo.bmp,
CellRenderer.Collapse.bmp, CellRenderer.Expand.bmp
```

いずれも「コピー/切取/貼付/元に戻す/やり直し/コメント/検索/セル展開・収納」等の**編集操作系
アイコン**であり、接点・コイル・OR・手動配線（横線/縦線/接続点）等の**記号配置系アイコンは
1件も含まれていない**（`CoilSearch.bmp`は「コイル検索」機能＝虫眼鏡的な検索アイコンであり、
コイル記号の配置ボタンとは別物と判断）。

他の残り38件の非画像リソース（`.resources`拡張子＝ダイアログ・コントロールのローカライズ
文字列等）についても、代表的な2件（`LDEditorControl.resources`・`LDEditorGridControl.resources`
＝ラダーグリッド本体のコントロール）を`System.Resources.ResourceReader`で内部エントリまで
展開したが、いずれも**内部エントリ0件**（文字列・画像とも埋め込みなし）だった。

### 3-2. 横断調査（WorkWindowPlugin＋DialogPlugin/CommandToolDlg配下135DLL）

「Contact/Coil/Wire/Connect/Instruction/Symbol/LDElement/ToolButton」等のキーワードで
135件のDLLの埋め込みリソース名を一括検索した。ヒットしたのは`InstructionEditForm.resources`・
`Parts.InstructionControl.resources`（命令の引数編集ダイアログ、文字列命令の入力欄であり
記号配置とは無関係）と、3-1節で既出の`CoilSearch.bmp`のみ。**新たな画像アセットの発見は
なかった**。

### 3-3. コマンドバー基盤（Infragistics）の確認

`Melco.GXW3.Mainframe.Managed.CommandBarsAdapter.dll`（32bit、32bit版PowerShellで
ロード成功）は埋め込みリソース**0件**。ツールバー基盤自体は`Infragistics4.Win.
UltraWinToolbars.v13.1.dll`（商用UIライブラリ、T-014調査済みのInfragistics採用の裏付け）
だが、これはツールバーの「箱」を提供する汎用ライブラリであり、GX Works3固有のラダー記号
アイコンはここには含まれない（アイコンはコマンド定義側から個別に渡される設計と推測される）。

## 4. 考察：記号配置アイコンはビットマップ資産ではない可能性（推測）

上記の網羅的探索（画像ファイル887件の全数確認＋関連DLL135件の埋め込みリソース横断調査）を
経てなお、接点・コイル・OR・手動配線の配置ボタンに対応する画像アセットが1件も見つからな
かったことから、**GX Works3はこれらのアイコンをビットマップとしてではなく、ボタンの
描画イベント内でプログラム的に線・円弧等を描く「カスタム描画（ベクター）」方式で表示している
可能性が高い**と考える（推測、断定はできない。理由：コード自体の解析＝ILデコンパイルは
「意匠の参考・構図の検分」の範囲を超え、アルゴリズムの抽出に踏み込むおそれがあるため実施
していない）。

この推測が正しければ、ecad2が既に採用している方式（`PlacementToolBarIconStyle`によるPath
Geometryの線分・円弧描画、T-040で確立）と設計思想が一致しており、**「アイコン画像を抽出して
流用する」という選択肢自体が両者ともに存在しない**（ecad2もGX Works3も、記号アイコンは
コードで描くものという前提）と考えられる。

## 5. 参考画像の保全について

DoD(4)「特定できたら`docs-notes/images/`配下へ保全」に該当する新規の画像アセットは
**見つからなかったため、保全は実施していない**。既存の`docs/images/t040-gx-ladder-toolbar-
reference.png`（T-040当時に取得済みのラダーツールバー参考スクリーンショット、`docs/
ecad2-t047-presurvey-onmitsu.md`2-1節で既に意匠参考として分析済み）が、現時点で利用可能な
最良の視覚参考情報のままである。

**気づき（範囲外・タスク化しない）**：本調査は静的ファイル解析（バイナリの埋め込みリソース
列挙）に限定した。GX Works3実機を起動し、ラダー編集画面を開いて高解像度のスクリーンショットを
新たに取得すれば、既存の低解像度参考画像より詳細な意匠検分ができる可能性がある。ただし
これはアプリケーションの起動・画面操作を伴うため、隠密の役儀（ファイル解析）ではなく忍者の
役儀（実機確認）に近い性質の作業と考える。要否は家老・殿の判断に委ねる。

---

## まとめ

- GX Works3（`GPPW3`）の実インストールを調査したが、記号配置系ツールバーアイコンの
  画像アセット抽出は**不可**（該当ファイルが存在しないため）。
- 抽出できたのは無関係な編集コマンド系アイコン（コピー等）のみで、意匠参考としての価値は
  低い。
- 意匠検討の材料は、既存のスクリーンショット参考画像（`docs/images/t040-gx-ladder-toolbar-
  reference.png`、T-047先行調査で分析済み）と、本調査で得た「GX Works3も恐らくベクター
  描画方式」という考察に留まる。侍のアイコン起草（T-040様式踏襲）は、この考察により
  従来方針（自作ベクター描画）の妥当性がむしろ補強されたと考える。
