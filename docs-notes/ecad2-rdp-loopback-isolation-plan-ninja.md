# RDPループバックによる実機確認の完全分離プラン(忍者起草)

最終更新: 2026-07-10（忍者記す）

## 0. 起草の経緯

PrintWindow改修（`90f77e2`）でボタン操作・見た目確認はフォーカス非占有化を達成した。残る
「キーボードショートカット自体の検証」「キャンバス内座標クリック」の2観点は、原理上グローバル
入力（SendKeys／SetCursorPos+mouse_event）が必須で、依然として殿の他作業と衝突する。この残り
2観点を **RDPループバックセッション（`mstsc /v:localhost`で自分自身へ接続し独立デスクトップを
作る手法）で完全にホスト非干渉化できるか**、家老采配（殿宿題）により検討した。

**結論を先に述べる: 非推奨。** Windows 11 Pro環境の構造的制約により、実現性が低いばかりか、
誤って実施すると殿の作業中セッションを強制的に終了させる致命的リスクがある。詳細と代替案は
以下のとおり。

---

## 1. 実現手順の机上整理と障壁（(1)(2)を統合して報告）

### 障壁1: mstscのループバック検知によるブロック

`mstsc /v:localhost`（または`127.0.0.1`、既定ポート3389）での自己接続は、**クライアント側の
安全機構により実際には接続パケットすら送信されずブロックされる**。エラーメッセージは
「別のコンソールセッションに接続できません。既にコンソールセッションが実行中です」という
文言で、一見シングルセッション制限のように読めるが、Wiresharkトレースでも確認されている
とおり、これはmstscが「ループバック経路への接続だ」と検知した時点で即座に出すエラーであり、
実際のTerminal Serviceとのネゴシエーションは発生しない（[IT trip](https://en.ittrip.xyz/windows/rdp/rdp-loopback-3389)、
[Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/5552299/rdp-console-session-in-progress-error-when-connect)）。

回避策としては「ローカルの待受ポートを3389以外にする」等が挙げられているが、これは主に
「踏み台経由でリモートの別ホストへ到達する」シナリオ向けの回避策であり、**真に自分自身
（同一マシンの同一TermService）へループバック接続する構成には適用しづらい**。サポートされた
公式の回避手段は無い。

### 障壁2（より本質的・致命的）: シングルセッション制限

仮にループバック検知を技術的に回避できたとしても、**Windows 11 Pro（Home含む、Server以外の
クライアントOS全般）は既定で「同時に1アクティブセッションのみ」に制限されている**。RDPで
ログインすると、**現在の物理ディスプレイ上のコンソールセッション（＝殿が今まさに使っている
セッション）が自動的にログオフまたはロックされる**（[Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/4181174/use-remote-desktop-while-also-logged-in-windows-11)）。

これは今回の目的（**殿の作業を妨げない**）と真っ向から矛盾する。「忍者専用の独立デスクトップ」
を作ろうとした瞬間、殿の作業中デスクトップの方が犠牲になる——本末転倒である。

複数セッションを技術的に許可する非公式改造（RDPWrap等、TermService本体のDLLパッチ）も存在
するが、OSの正規動作を書き換えるものでライセンス・安定性の両面でリスクが高く、殿のメイン
作業機に導入する選択肢としては採用し難い。

### 障壁3: 非アクティブセッションでの描画パイプライン変化

上記2障壁だけで実現困難と判断できるため実地検証は行っていないが、文献調査の範囲では
「非アクティブ（切断状態）のRDPセッションではWDDM関連の挙動が変わり、GPU利用からソフトウェア
レンダリングへフォールバックする既知の問題がある」との情報がある（[TechTarget](https://www.techtarget.com/searchvirtualdesktop/tip/Steps-to-fix-a-black-screen-on-a-Windows-11-remote-desktop)）。
WPF（DirectX/Direct3D経由の描画）アプリであるEcad2.Appがこの状態でPrintWindowから正しい
内容を返し続けるかは、たとえセッション分離が実現しても別途検証が必要になる不確定要素として
残る。

**実地確認を見送った理由**: 「(2)非アクティブRDPセッションでの描画抑制がPrintWindow/UI
Automationに影響せぬかの実地確認」は家老指示の観点だが、そもそも障壁1・2により「実地確認を
試みること自体が殿の現行セッションを犠牲にするリスクを伴う」ため、事前承認なしに試すのは
危険と判断し、机上調査に留めた。

---

## 2. dotnet run等の起動・ビルド競合への影響（(3)）

障壁1・2により本命プラン自体が成立しないため深追いはしないが、一般論として: 同一プロジェクト
ディレクトリ（`C:\ECAD2`）に対する`dotnet run --project src/Ecad2.App`を複数セッションから
同時実行すると、`obj`/`bin`配下のビルド成果物への書き込みロックが競合し得る。現行運用でも
「侍がビルド中は忍者の実機確認を控える」調整を都度行っている実例があり、セッションを分離した
としても、この種の排他制御自体は別途必要になる（分離の有無に関わらない既存の課題）。

---

## 3. 導入コスト対効果（(4)）

| 項目 | 内容 |
|---|---|
| コスト | 障壁1の回避策模索・障壁2による殿の現行セッション犠牲リスク・非公式改造の安定性リスク・仮に実現しても障壁3の追加検証が必要 |
| 効果 | PrintWindow改修で大半（ボタン操作・シート追加削除改名・ダイアログ・見た目確認）は既に解決済み。RDPループバックで追加に解決するのは「キーボードショートカット固有の検証」「キャンバス内座標クリック」という**限定的な残り2観点のみ** |

**コストが効果に対し著しく不釣り合い**であり、導入は推奨しない。

---

## 4. 代替案

1. **現状維持（推奨）**: PrintWindow改修後の運用をそのまま継続する。残る2観点（ショートカット
   検証・キャンバス内クリック）は発生頻度・所要時間ともに小さいため、都度「これから短時間
   フォーカスを使うが構わぬか」と殿に一声かけてから実施すれば実害は小さい。
2. **セカンダリWindowsユーザーアカウント + 高速ユーザー切り替え**: 同一PC上に検証専用の
   別アカウントを作り、`Win+L`等で切り替えて実行する。ただし同時可視化はできず（切り替え中は
   殿の画面も一時的にロック画面になる）、完全な非干渉ではない上、リポジトリの二重チェック
   アウトや.NET SDK等の環境重複整備が必要になり、導入コストの割に障壁2の代替としては
   中途半端。
3. **Hyper-V等の仮想マシン**: 完全に独立した環境で実行できる本命の分離策ではあるが、
   .NET SDK・依存パッケージの環境構築コストが高く、GPUパススルー無しの仮想環境でWPFの
   ハードウェアアクセラレーション描画が正しく行われるか（PrintWindowの正確性含め）は
   別途の検証が要る。効果に対しコストが重く、今回の残り2観点のためだけに導入するには
   見合わない。

**忍者としての結論**: 代替案1（現状維持）を推奨する。RDPループバックおよび仮想マシン導入は
「達成したい効果（残り2観点の非占有化）」に対し「支払うコスト・リスク」が大きく上回るため、
現時点では見送りが妥当と考える。

---

## 5. 参考文献

- [Fix RDP "Console Session in Progress" on 127.0.0.1:3389 (Windows 11 / Server 2025)](https://en.ittrip.xyz/windows/rdp/rdp-loopback-3389)
- [Use Remote Desktop while also logged in. Windows 11 Pro - Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/4181174/use-remote-desktop-while-also-logged-in-windows-11)
- [RDP "console session in progress" error when connecting via loopback IPs - Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/5552299/rdp-console-session-in-progress-error-when-connect)
- [6 steps to fix a black screen on a Windows 11 remote desktop | TechTarget](https://www.techtarget.com/searchvirtualdesktop/tip/Steps-to-fix-a-black-screen-on-a-Windows-11-remote-desktop)
