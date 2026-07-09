# ラダー編集・テストモードUI/UX参考システム調査（隠密）

> 2026-07-09 隠密調査。殿直接依頼。Webリサーチ主体（3並行エージェント：OSS／商用無償体験版／
> 国産公開資料）。スコープ＝ラダー編集（記号配置・配線・編集操作・ツールバー意匠）とテスト
> モード（シミュレーション・通電モニタ表示）のUI/UXに限定。**PLC実機通信・書込み等は対象外**
> （殿明示）。ecad2は未インストール・ソフト本体は今回いずれも操作していない（Web公開資料の
> 範囲での調査）。
>
> **本調査書の位置づけ（殿指示2026-07-09で確定）**：T-047個別の裁定資料ではなく、**後日の
> UI/UX作成全般で繰り返し参照する恒久保存資料**。そのため通常の調査書より保存性を重視し、
> (1)参照URLは該当機能への直リンクを優先 (2)リンク切れに備え要点は本文のみで自立して読める
> よう記述 (3)ラダー編集／テストモードの機能軸で章立て、を行った。

**【著作権に関する線引き・冒頭明記】** 以下はすべて各社・各プロジェクトの著作物（UI・
ドキュメント・カタログ）を**独自の言葉で要約した観察結果**であり、スクリーンショット画像や
マニュアル文章そのものの転載は行っていない。UI・操作フローの「観察とアイデアの参考」は
著作権侵害に当たらないが、アイコン画像・コードそのもののコピーは不可という原則を厳守した。
特にGPL/LGPL系OSSについては「UIのアイデア参考は可、コードの流用・改変流用は不可」を
各項目で明記する。**画像の私的保全（docs-notes/images/）は今回実施していない**——調査は
Web公開ページのテキスト・仕様記述の確認に留まり、保全に値する具体的な画像URL（screenshot
そのものへの直リンク）までは特定していないため。個別の画像保全が必要になった場合は、
本書に記載の各URLを起点に改めて対応する（6節）。

---

## 結論（先出し・ecad2への示唆）

キーボードファースト理念（CLAUDE.md）との親和性で最も参考価値が高いと考えるのは
**Do-more Designer**と**LDmicro**の2つ（4節で詳述）。いずれも「ファンクションキー等の
単発キーで記号を配置し、方向キー系で配線する」という、ecad2が既に採用している設計
（F5〜F10の単発配置キー）と極めて近い思想を持つ。国産PLCソフト（KV STUDIO・Sysmac
Studio）も含め、**業界全体で「キーボードショートカットを一級市民として扱う」慣行が
広く見られる**ことが今回の調査で裏付けられ、ecad2の既存方針の妥当性を補強する材料になると
考える。

---

## 1. 候補システム一覧（分類・ライセンス・入手性）

| 分類 | ソフト | ライセンス/入手性 | 開発状況 |
|------|--------|------------------|---------|
| OSS | OpenPLC Editor（新版 Autonomy-Logic） | GPL-3.0、GitHub無償公開 | 活発（2026年時点でもリリース継続） |
| OSS | OpenPLC Editor（旧版 thiagoralves） | GPLv2/LGPLv2+GPLv3混在（Beremiz派生） | 事実上旧版扱い |
| OSS | Beremiz | GPLv2以降(IDE)／LGPLv2以降(Pyランタイム)／GPLv3以降(Cランタイム) | 継続中だが公式ドキュメント自体が疎ら |
| OSS | ClassicLadder | LGPL v3（LinuxCNC組込時はLGPL v2） | 単独では2020年以降停滞、LinuxCNC経由で存続 |
| OSS | LDmicro | GPLv3以降 | 緩やかに継続、日本語化フォークあり |
| 商用無償 | CODESYS | IDE無償（要アカウント登録）、ランタイムは有償（デモ2時間） | 業界標準的地位、継続 |
| 商用無償 | Do-more Designer | 完全無償・フル機能ダウンロード（時間/機能制限なし） | 継続 |
| 商用無償 | Connected Components Workbench | Standard Edition無償（要Rockwellアカウント）、Developer Editionは有償 | 継続 |
| 商用無償(参考追加) | CLICK PLC Programming Software | 完全無償・フル機能 | 継続 |
| 国産公開資料 | キーエンス KV STUDIO | 体験版無償（要ユーザー登録） | 継続、現行KV-H1J-DL系 |
| 国産公開資料 | オムロン Sysmac Studio | インストール後ライセンス登録不要で30日間全機能試用可 | 継続 |
| 国産公開資料(簡易) | パナソニック FPWIN GR7 | 体験版有無は不明 | 情報不十分 |
| 国産公開資料(簡易) | IDEC WindLDR | 入手性等は情報不十分 | 情報不十分 |

**GX Works3**（既存調査`docs/ecad2-gxworks3-uiux-survey-onmitsu.md`・`-part2.md`）は
本調査の対象外だが、比較の基準として3・4節で随時参照する。

---

## 2. ラダー編集UI（記号配置・配線・ツールバー意匠）

システムごとに、要点を本文のみで自立して読めるよう記述する（リンク切れ対策）。

### Do-more Designer【ecad2への参考価値：最高】
**ファンクションキー1つで主要記号を直接配置**する体系——**F2=a接点／F3=b接点／
F4=接点ブラウザ／F5=コイルブラウザ／F7=ボックス（ファンクション）ブラウザ**、
Shift/Ctrl+F2・F3で微分（立上り／立下り）接点も呼び出せる。配線は**Ctrl+矢印キーで
方向に線を引き、Ctrl+Shift+矢印キーで削除、Ctrl+Wで出力列まで一気に配線、Enterで行挿入**
——マウスに頼らず回路を組み上げる体系が明確。ツールボックスからのドラッグ&ドロップや
パレットクリックによるマウス操作も選べる（両対応）。ecad2のF5〜F10単発配置キー体系と
極めて近い思想。
- キーボードショートカット一覧（直リンク）：
  https://hosteng.com/dmdhelp/content/ladder_view/Keyboard_Shortcuts.htm
- ラダーエディタ解説（直リンク）：
  https://hosteng.com/DmDHelp/Content/Ladder_View/The_Ladder_Editor.htm
- ソフト本体：https://www.automationdirect.com/do-more/brx/software

### LDmicro【ecad2への参考価値：高】
調査した中で**最もキーボード操作の作り込みが厚い**。カーソルが命令間を移動し、Tabキーで
選択状態にできる。選択命令に対し上下左右への挿入方向を指定して新規命令を追加でき、行
（rung）の挿入・削除もメニュー/ショートカットから可能。設計思想自体がキーボード操作を
前提としている（マウス操作前提のCAD的UIとは異なる作り）。
- マニュアル本文（直リンク、GitHub raw、リンク耐久性高い）：
  https://github.com/LDmicro/LDmicro/blob/master/ldmicro/manual.txt
- リポジトリ：https://github.com/LDmicro/LDmicro

### キーエンス KV STUDIO
複数の独立したユーザーブログが一致して報告——**a接点=F5／b接点=Shift+F5／コイル=F7／
縦線作成=F8＋Shift／横線作成=F9＋Shift／変換=Ctrl+F9／整列=Ctrl+Shift+F／
オンライン転送=F11**。GX Works3と同種のF-key多用慣行（情報源間で罫線編集キー割当に
バージョン差の可能性あり、推測）。左ペインにユニット構成ツリーを表示する構成。
- ショートカットキーまとめ（ユーザーブログ、直リンク）：
  https://momomo-97.com/kvstudio-shortcut-key-memo/
- 罫線編集の矢印キー操作（ユーザーブログ、直リンク）：
  https://momomo-97.com/edit-the-ruled-line-of-the-kv-studio-ladder-with-the-arrow-keys/
- 体験版ダウンロード：https://www.keyence.co.jp/support/user/controls/plc/software/trial/

### オムロン Sysmac Studio
公式サイトに独立した「**ショートカットキー一覧**」専用ページ（動画マニュアル内）が用意
されており、[H]キーでもショートカット一覧を表示できるとの言及あり。「回路部品の入力に
ショートカットキーを使用する」操作自体を公式FAQが案内している。個々のキー割当詳細は
公式ページへのWebFetchが403で拒否されたため未確認（不明、6節参照）。オプション設定で
ラダーエディタの背景色（割付先変数・ネットワーク変数用）をカスタマイズ可能との記述あり。
- ショートカットキー一覧ページ（直リンク、要ブラウザアクセス）：
  https://www.fa.omron.co.jp/product/tool/ss-video-manual/ja/ladder-programming/shortcut-keys/
- 回路部品入力のショートカットキーFAQ：
  https://faq.fa.omron.co.jp/tech/s/article/faq06026

### CODESYS
LDエディタはネットワーク（rung相当）単位の編集方式。要素追加は基本ツールボックスからの
ドラッグ&ドロップで、挿入可能位置は灰色四角・ひし形・上下三角のインジケータとカーソルの
「＋」で明示される。キーボードでもCtrl+K（接点挿入）、Ctrl+A（コイル挿入）等がLadder
メニュー／コンテキストメニュー経由で用意され、Tools>Customizeで再割当も可能。ただし
Do-moreのような「1ファンクションキー＝1記号配置」の単発操作体系ではなく、メニュー
コマンド寄りの設計。
- LDエディタ概要（直リンク）：
  https://content.helpme-codesys.com/en/CODESYS%20Ladder/_ld_overview.html
- 接点挿入コマンド（直リンク）：
  https://content.helpme-codesys.com/en/CODESYS%20Ladder/_ld_cmd_insert_contact.html
- コイル挿入コマンド（直リンク）：
  https://content.helpme-codesys.com/en/CODESYS%20Ladder/_ld_cmd_insert_coil.html
- IDE全体のショートカット一覧（直リンク）：
  https://content.helpme-codesys.com/en/CODESYS%20Development%20System/_cds_shortcuts.html

### CLICK PLC Programming Software（商用無償・参考追加）
右側ペインの命令一覧からラング上へドラッグ&ドロップで配置するのが基本操作。
Ctrl+矢印キーで配線描画、Ctrl+Shift+矢印キーで配線消去とDo-more系と共通の体系を持つ
（AutomationDirect社の関連製品ゆえの慣行共有と推測）。初心者はマウスのみでアドレス
ピッカー等を使い、熟練者はキーボードショートカットで高速化できる設計と公式に紹介される。
- プログラミング入門ページ（直リンク）：
  https://www.automationdirect.com/clickplcs/getting-started/programming

### Connected Components Workbench
LD用のキーボードショートカットがMicro800リファレンスマニュアルに記載されており
（F5＝デバッグモード切替との言及あり）、要素追加はツールボックス／パレットからの
ドラッグが基本と見られる。ショートカット全一覧はPDF一次資料（バイナリ形式）の抽出限界
により不明。
- Standard/Developer版の機能差（直リンク）：
  https://support.rockwellautomation.com/app/answers/answer_view/a_id/680300/

### OpenPLC Editor
ツールバー/命令パレットからの接点・コイルのドラッグ&ドロップ配置が基本。ラングは
「Add Rung」ボタンかInsertキーで追加、右クリックメニューから「Insert Contact」等も可能。
**注目すべき事実**：現行版（Autonomy-Logic/openplc-editor）でラダーエディタのキーボード
ショートカット/ナビゲーション機能が一度実装された（Issue #496, PR #859）が、検証不足で
バグが見つかり同一バージョン内でrevertされた（PR #894、理由は"merged before full
validation and bugs were found"）。マウス操作が主体で、キーボードのみでの完結は現状
困難と見られる。
- revert経緯（直リンク、公式PR）：https://github.com/Autonomy-Logic/openplc-editor/pull/894
- リポジトリ：https://github.com/Autonomy-Logic/openplc-editor

### Beremiz
PLCopenEditor（wxPython）でLD/FBD/SFC/ST/ILを共通のグラフィカルエディタで編集。ソース
コード上に`LD_Viewer`というクラス名が存在することは確認できたが、具体的な配置操作手順・
キーボードショートカット一覧は一次資料（公式ドキュメントが「まだ疎ら」と自認）で確認
できず不明。
- 概要ページ（直リンク）：https://beremiz.readthedocs.io/en/latest/overview.html

### ClassicLadder
GTK2/GTK3ベース。ツールバーに矢印(選択)・消去・a接点/b接点・立上り/立下りエッジ・結線・
タイマー・カウンタ・比較・コイル・ジャンプ・呼び出し・変数代入などのアイコンが並び、
選択してラング上のマス目に配置する方式（v0.9.7以降は右クリックメニューにも対応）。
キーボードショートカットの詳細一覧は不明、基本はマウス操作中心のCAD的操作感。
- LinuxCNC統合ドキュメント（直リンク）：
  http://linuxcnc.org/docs/2.4/html/ladder_classic_ladder.html

### パナソニック FPWIN GR7（簡易調査）
Alt+数字キーで各ドッキングウィンドウへフォーカス移動、Escキーで編集画面に復帰、
Ctrl+Tでクロスリファレンス表示、というマルチペイン（ドッキングウィンドウ）構成を
キーボードで行き来する設計思想がうかがえる断片情報のみ確認。記号配置自体の主要
ショートカットは情報不十分。
- クロスリファレンスFAQ（直リンク）：
  https://ac-faq.industrial.panasonic.com/ja/knowledge/fasys/plc/software/fpwingr7/fpwin-gr7-cross-ref

### IDEC WindLDR（簡易調査）
公式サイトに「組み込みエディタ・ショートカットキー・モニタ機能を備える」旨の紹介はある
が、具体的なキー割当は公開資料から確認できず情報不十分。
- 製品ページ：https://www.idec.com/ja-jp/automation/programmable-logic-controller/software/windldr-plc-software

---

## 3. テストモードUI（シミュレーション・通電モニタ表示）

### LDmicro
Ctrl+Mでシミュレーションモードへ移行、スペースバーで1サイクル実行、Ctrl+Rでリアルタイム
連続実行。**通電中の命令は明るい赤、非通電の命令はグレーで表示**、とマニュアルに明記
（色分け仕様が明確に文書化された数少ない例）。ADC入力等のダイアログはUp/Down、PgUp/PgDn
キーで操作可能。
- マニュアル本文（直リンク）：
  https://github.com/LDmicro/LDmicro/blob/master/ldmicro/manual.txt

### CODESYS
実機なしでOnlineメニュー→Simulationでログインしテスト可能（プログラムがエラーフリー
であることが条件）。シミュレーション中は**ステータスバーに赤字で「Simulation」と表示**
され、デバイスツリーはイタリック表記になる。オンライン監視時は**配線がTRUE＝青の太線、
FALSE＝黒の太線**で色分け表示され、通電状態が視覚的に把握しやすい。ライセンス未購入の
SoftPLCは2時間で自動終了するデモ動作。
- シミュレーションモードでのテスト（直リンク）：
  https://content.helpme-codesys.com/en/CODESYS%20Development%20System/_cds_testing_in_simulation_mode.html

### Do-more Designer
内蔵シミュレータで実機なしにテスト可能。「CPUファームウェアと同一コードで動くため精度が
高い」と謳われ、仮想I/O・タイマ／カウンタ・PID（Trend View併用）まで再現できる。ただし
ラング上の通電色表示の具体的なビジュアル仕様（色分け・アニメーションの有無）は一次資料で
確認できず不明。
- シミュレータページ（直リンク）：
  https://www.automationdirect.com/do-more/brx/software/simulator

### Connected Components Workbench
CCW v12以降、Micro800シミュレータが同梱。手順は「コントローラをMicro850 SIMに設定→
シミュレータ起動→モジュール構成を同期→プロジェクトをダウンロード」の4段階。シミュレータ
ウィンドウのLED表示で入出力状態を確認でき、モニタ中は通電（true）状態の要素が赤で表示
されるとの情報あり。**重要な制約**：Standard Editionのシミュレータは実行（RUN）モードが
**10分で自動的にProgramモードへ戻る**（再度手動でRUNへ戻せば継続可、Developer版は無制限）。
- Micro800シミュレータ解説（直リンク）：
  https://support.rockwellautomation.com/app/answers/answer_view/a_id/634013/

### CLICK PLC Programming Software
**ソフトウェア内蔵シミュレータは無く**、テストには実機PLCまたは物理的な入力シミュレータ
モジュール（C0-08SIMなど、トグルスイッチとLEDでI/Oを模擬するハードウェア）が必要という点が
CODESYS/Do-more/CCWと対照的。ソフト単体の「Data View」機能でビット・レジスタ値をモニタ
でき、出力コイル等はtrue時に青くハイライト表示される。
- シミュレーション代替手段の解説（直リンク）：
  https://industrialmonitordirect.com/blogs/knowledgebase/click-plc-simulation-without-hardware-options-explained

### OpenPLC Editor
Debuggerペインを備え、Tickカウンタの増加を表示。シミュレーション中は**通電中の接点/コイル
が緑色にハイライト**される。デバッガのポーリング速度向上・時間軸表示（最大10分）等の
改善も継続的に行われている。
- リポジトリ（変更履歴等の一次情報源）：https://github.com/Autonomy-Logic/openplc-editor

### キーエンス KV STUDIO
モニタ/シミュレータモードで**「/」キーによる接点ON/OFF直接切替**が可能（複数の独立
ユーザーブログで一致）。モニタ/シミュレータ時にラダー左側に何らかの表示が出る様子だが
（公式FAQタイトルに「赤ライン」の語あり）、詳細な色分け仕様は一次資料に到達できず不明。
リアルタイムチャートモニタ機能もあり。
- 関連FAQ（タイトルのみ確認、直リンク）：
  https://www.keyence.co.jp/support/user/controls/faq/answer.jsp?faq_id=93030

### オムロン Sysmac Studio
オンライン接続時、接点の導通表示が色分けされる旨の言及を検索結果上で確認したが、一次
資料（カタログPDF）はテキスト抽出できず推測の域を出ない。上位機能として3Dシミュレーション
オプションによるロジック・モーション統合デバッグがカタログで謳われている（タイトルより
確認）。
- 3Dシミュレーションカタログ（直リンク、PDF・テキスト抽出不可）：
  https://www.marutsu.co.jp/contents/shop/marutsu/datasheet/sysmac_studio_sbca-122e.pdf

### Beremiz
プロジェクトURLを「LOCAL://」に設定するとIDEが一時的にローカルのBeremiz Python
ランタイムを起動し、実PLCなしでデバッグ可能。通電状態の色分け仕様（色・アニメーション）
の具体的な記述は一次資料で確認できず不明。

### ClassicLadder
編集とシミュレーションを同一GTKウィンドウ内で切替可能（「edit/simulate」）。通電状態の
色分けの具体的仕様は一次資料で確認できず不明。LinuxCNC組込時はXenomai等でリアルタイム
実行可能。

---

## 4. ecad2への参考として有望な順の所見

キーボードファースト理念（マウス操作に頼らない操作性）との親和性を最優先観点として順位付け：

1. **Do-more Designer**（最有望）：F-key単発配置＋Ctrl+矢印配線という体系が、ecad2の
   F5〜F10設計と最も近い実例。完全無償フル機能ゆえ、必要であれば実際にダウンロードして
   直接UI観察することも可能（実施可否は家老・殿判断、本調査はWeb公開資料の範囲に留めた）。
2. **LDmicro**：OSS中で最もキーボード操作前提の設計思想を持つ。通電色分け仕様
   （明るい赤=通電／グレー=非通電）も明記されており、テストモードUIの参考にもなる。
3. **KV STUDIO・Sysmac Studio**（国産）：GX Works3で確認済みのF-key多用・キーボード
   ショートカット重視という慣行が、国産PLCソフト全般の業界標準であることを裏付ける。
   ecad2の既存設計方針の妥当性を補強する材料。
4. **CODESYS**：通電状態の配線色分け（TRUE=青太線/FALSE=黒太線）とステータスバーでの
   モード表示（赤字「Simulation」）は、ecad2の既存StatusMessage方式との親和性が高い
   テストモードUI参考例。
5. **OpenPLC Editor**：キーボードショートカット機能を実装したがバグでrevertした実例は、
   実装難易度・検証の重要性を示す教訓として参考になる（ポジティブなUI参考ではなく
   リスク認識の参考）。
6. その他（Beremiz・ClassicLadder・CCW・CLICK・FPWIN GR7・WindLDR）：一次資料が薄く
   詳細不明な点が多いため、補助的参考に留まる。

**テストモードUIの通底パターン**：通電状態を色・太さの変化で示す点は各社共通だが、
配色自体（赤/青/緑等）は統一規格ではなくソフトごとに異なる。ecad2独自の配色は自由に
決めてよいと考える（推測）。「シミュレーション中」であることをステータスバー等で
明示する（CODESYS方式）は、ecad2の既存UI慣行（StatusMessage）と自然に統合できる
パターンと考える。

---

## 5. 未確認・要フォローアップ事項（不明点の集約）

後日この調査書を再訪する際の起点として、一次資料に到達できず「不明」とした主な項目を
まとめる：

- Sysmac Studioのショートカットキー一覧ページ・製品特長ページ（`fa.omron.co.jp`）は
  botアクセスに対し403を返し内容未確認。ブラウザ経由（chrome-devtools等）での再アクセス
  であれば確認できる可能性がある。
- Connected Components WorkbenchのLDショートカット全一覧（Micro800リファレンス
  マニュアルPDF）、Sysmac Studioの3Dシミュレーションカタログ（PDF）はいずれもテキスト
  抽出不可の形式で内容未確認。
- Beremiz・ClassicLadderは公式ドキュメント自体が薄く、具体的なキー割当・色分け仕様は
  情報不十分のまま。
- パナソニックFPWIN GR7・IDEC WindLDRは簡易調査に留め、体験版入手性含め情報不十分。

---

## 6. 気づき（範囲外・タスク化しない）

- Do-more Designer・LDmicroはいずれも無償入手可能なため、より具体的なUI観察が必要な
  段階になれば実際にダウンロードしての直接確認が有効と考える（本調査はWeb公開資料の
  範囲に留めた、殿明示のスコープ境界どおり）。
- 画像の私的保全（docs-notes/images/）は今回実施していない。上記5節の未確認事項が
  解消され、特に参考価値の高い画像URLが具体的に特定できた段階で、出典・ライセンスを
  併記のうえ改めて保全することを推奨する。
