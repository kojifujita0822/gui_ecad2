# ラダー編集・テストモードUI/UX参考システム調査（隠密）

> 2026-07-09 隠密調査。殿直接依頼。Webリサーチ主体（3並行エージェント：OSS／商用無償体験版／
> 国産公開資料）。スコープ＝ラダー編集（記号配置・配線・編集操作・ツールバー意匠）とテスト
> モード（シミュレーション・通電モニタ表示）のUI/UXに限定。**PLC実機通信・書込み等は対象外**
> （殿明示）。ecad2は未インストール・ソフト本体は今回いずれも操作していない（Web公開資料の
> 範囲での調査）。

**【著作権に関する線引き・冒頭明記】** 以下はすべて各社・各プロジェクトの著作物（UI・
ドキュメント・カタログ）を**独自の言葉で要約した観察結果**であり、スクリーンショット画像や
マニュアル文章そのものの転載は行っていない。UI・操作フローの「観察とアイデアの参考」は
著作権侵害に当たらないが、アイコン画像・コードそのもののコピーは不可という原則を厳守した。
特にGPL/LGPL系OSSについては「UIのアイデア参考は可、コードの流用・改変流用は不可」を
各項目冒頭で明記する。

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

---

## 2. OSS系統の詳細

### OpenPLC Editor
**ラダー編集UI**：ツールバー/命令パレットからのドラッグ&ドロップが基本、ラング追加は
Insertキーや右クリックメニューからも可能。**注目すべき事実**：現行版（Autonomy-Logic/
openplc-editor）でキーボードショートカット/ナビゲーション機能が一度実装された
（Issue #496, PR #859）が、検証不足でバグが見つかり同一バージョン内でrevertされた
（PR #894）。キーボードファーストなラダー編集UIの**実装難易度を示す実例**として、
ecad2にとって「他山の石」の価値がある（T-021の往復苦労とも通じる教訓、推測）。

**テストモードUI**：Debuggerペインあり、通電中の接点/コイルは緑色ハイライト。

**参照URL**：https://github.com/Autonomy-Logic/openplc-editor 、
https://github.com/Autonomy-Logic/openplc-editor/pull/894 、
https://github.com/thiagoralves/OpenPLC_Editor

### Beremiz
**ラダー編集UI**：PLCopenEditor（wxPython）でLD/FBD/SFC/ST/ILを共通編集。具体的な
配置操作手順・キーボードショートカットは一次資料で確認できず**不明**。

**テストモードUI**：ローカルPythonランタイムでのデバッグは可能だが、通電表示の色分け仕様は
**不明**。

**参照URL**：https://github.com/beremiz/beremiz 、https://beremiz.org/doc

### ClassicLadder
**ラダー編集UI**：GTKツールバーから記号アイコンを選びラング上のマス目へ配置するCAD的操作感。
キーボードショートカットの詳細は**不明**。

**テストモードUI**：同一ウィンドウ内でedit/simulate切替可能。色分け仕様は**不明**。

**参照URL**：https://sourceforge.net/projects/classicladder/ 、
http://linuxcnc.org/docs/2.4/html/ladder_classic_ladder.html

### LDmicro【ecad2への参考価値：高】
**ラダー編集UI**：調査した中で**最もキーボード操作の作り込みが厚い**。カーソルが命令間を
移動、Tabで選択、選択命令に対し上下左右の挿入方向を指定して新規命令を追加、行の挿入も
メニュー/ショートカットから可能。設計思想自体がキーボード操作前提。

**テストモードUI**：Ctrl+Mでシミュレーションモードへ、スペースバーで1サイクル実行、
Ctrl+Rで連続実行。**通電中の命令は明るい赤、非通電はグレー**とマニュアルに明記（色分け
仕様が明確な数少ない例）。

**参照URL**：https://github.com/LDmicro/LDmicro 、
https://github.com/LDmicro/LDmicro/blob/master/ldmicro/manual.txt

---

## 3. 商用無償版系統の詳細

### CODESYS
**入手**：IDEは無償（要アカウント登録）、実機ランタイムは有償（ライセンス無しは2時間デモ）。

**ラダー編集UI**：ツールボックスからのドラッグ&ドロップが基本、メニューコマンド
（Ctrl+K=接点挿入・Ctrl+A=コイル挿入等）中心で、Do-more/LDmicroのような「単発キー1つで
記号配置」の体系ではない。IDE全体ではF5=実行等の標準的ショートカットあり。

**テストモードUI**：Online>Simulationでログイン、ステータスバーに赤字で「Simulation」表示。
**配線色分けが明確**：TRUE＝青太線、FALSE＝黒太線。

**参照URL**：https://content.helpme-codesys.com/en/CODESYS%20Ladder/_ld_overview.html 、
https://content.helpme-codesys.com/en/CODESYS%20Development%20System/_cds_testing_in_simulation_mode.html

### Do-more Designer【ecad2への参考価値：最高】
**入手**：公式サイトから無償フル機能ダウンロード（時間/機能制限なし）。

**ラダー編集UI**：**ファンクションキー1つで主要記号を直接配置**——F2=a接点／F3=b接点／
F4=接点ブラウザ／F5=コイルブラウザ／F7=ボックスブラウザ、Shift/Ctrl+F2・F3で微分接点。
配線は**Ctrl+矢印キーで方向に線を引き、Ctrl+Shift+矢印で削除、Ctrl+Wで出力列まで一気に
配線、Enterで行挿入**——マウスに頼らず回路を組み上げる体系が明確。ecad2のF5〜F10単発配置
キー体系と極めて近い思想。

**テストモードUI**：内蔵シミュレータ（実機ファームウェアと同一コードで動作、精度が高いと
謳われる）。ただし通電色表示の具体仕様は一次資料で確認できず**不明**。

**参照URL**：https://www.automationdirect.com/do-more/brx/software 、
https://hosteng.com/dmdhelp/content/ladder_view/Keyboard_Shortcuts.htm

### Connected Components Workbench
**入手**：Standard Edition無償（要Rockwellアカウント）、Developer Editionは有償
（オンライン編集・フルシミュレータ等が追加）。

**ラダー編集UI**：ツールボックス/パレットからのドラッグが基本と見られる。ショートカット
全一覧は一次資料（PDF）到達限界により**不明**。

**テストモードUI**：Micro800シミュレータ同梱。Standard EditionはRUNモードが**10分で
自動的にProgramモードへ戻る**制限あり（Developer版は無制限）。true要素は赤表示との情報。

**参照URL**：https://support.rockwellautomation.com/app/answers/answer_view/a_id/634013/

### （参考追加）CLICK PLC Programming Software
**入手**：完全無償・フル機能。Do-more系と共通のCtrl+矢印配線体系を持つ。**ソフト内蔵
シミュレータは無く**、実機PLCまたは物理I/Oシミュレータモジュールが必要（他3件との対照点）。
出力コイルはtrue時に青ハイライト。

**参照URL**：https://www.automationdirect.com/clickplcs/getting-started/programming

---

## 4. 国産公開資料系統の詳細

### キーエンス KV STUDIO
**入手**：体験版無償（要ユーザー登録）。

**ラダー編集UI**：複数の独立したユーザーブログが一致して報告——**a接点=F5／b接点=Shift+F5／
コイル=F7／縦線作成=F8＋Shift／横線作成=F9＋Shift／変換=Ctrl+F9／整列=Ctrl+Shift+F／
オンライン転送=F11**。GX Works3と同種のF-key多用慣行（情報源間で罫線編集キー割当に
バージョン差の可能性あり、推測）。

**テストモードUI**：モニタ/シミュレータモードで「/」キーによる接点ON/OFF直接切替が可能。
色分けの詳細仕様は一次資料未到達で**不明**。

**参照URL**：https://www.keyence.co.jp/support/user/controls/plc/software/trial/ 、
https://momomo-97.com/kvstudio-shortcut-key-memo/

### オムロン Sysmac Studio
**入手**：インストール後**ライセンス登録不要で30日間全機能試用可能**。

**ラダー編集UI**：公式サイトに独立した「**ショートカットキー一覧**」専用ページが用意されて
おり（動画マニュアル内）、[H]キーでもショートカット一覧を表示可能との言及あり。個々の
キー割当詳細は公式ページの403アクセス拒否により**不明**だが、「ショートカット一覧を
専用ページとして公式に用意する」こと自体がキーボード操作を製品仕様として重視している
証左と考える。

**テストモードUI**：オンライン接続時の導通色分けに関する言及はあるが一次資料未到達で
詳細**不明**。3Dシミュレーションオプションによるロジック・モーション統合デバッグは
カタログタイトルより確認。

**参照URL**：https://faq.fa.omron.co.jp/tech/s/article/faq05375 、
https://www.fa.omron.co.jp/product/tool/ss-video-manual/ja/ladder-programming/shortcut-keys/

### パナソニック FPWIN GR7・IDEC WindLDR（簡易調査）
FPWIN GR7はAlt+数字キーでドッキングウィンドウへフォーカス移動・Escで編集画面復帰・
Ctrl+Tでクロスリファレンスという断片情報のみ確認、記号配置キー・テストモードUIは
**情報不十分**。WindLDRは「組込エディタ・ショートカットキー・モニタ機能を備える」旨の
紹介のみで詳細は**情報不十分**。

---

## 5. ecad2への参考として有望な順の所見

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

## 6. 気づき（範囲外・タスク化しない）

- Sysmac Studio・Omronの一部公式ページはbot向けアクセスで403を返し、詳細確認に至らな
  かった（ブラウザ経由の再アクセスであれば確認できる可能性がある。要否は殿判断）。
- Do-more Designer・LDmicroはいずれも無償入手可能なため、より具体的なUI観察が必要な
  段階になれば実際にダウンロードしての直接確認が有効と考える（本調査はWeb公開資料の
  範囲に留めた、殿明示のスコープ境界どおり）。
