# T-033 非モーダル化の技術選定がUI Automationへ与える影響 — 静的調査（隠密）

> 2026-07-07 隠密調査。家老依頼、対象は`docs/ecad2-t033-implementation-plan-samurai.md` 3.3節・6.2節の
> 論点。**静的調査のみ（T-033は実装未着手のためPoC実測ではなくWeb一次情報＋既存コード調査に基づく）。**
> 実装技術（Popup／同一Window内オーバーレイ）の選定自体は侍の技術裁量事項であり、本書はその判断材料の提示に留める。

---

## 結論（サマリ）

**同一Window内オーバーレイ（Grid上への直接重ね）の方が、IsDefault/IsCancel機構・UI Automation操作性の
両面でリスクが低い。Popupは「別HWND」であることに起因する既知の落とし穴が複数確認できた。**

| 観点 | Popup（独立HWND） | 同一Window内オーバーレイ |
|---|---|---|
| (1) UIAツリー上の現れ方 | 別HWNDだが`PopupRootAutomationPeer`経由でツリーには繋がる（ComboBoxドロップダウン等で実績あり）。**ただし確実性はやや劣る** | 既存Windowと同一ツリー内。`ecad2-ui-automation`の`Get-Ecad2Root`(`FromHandle`+`Descendants`)がそのまま機能 |
| (2) IsDefault/IsCancel | AccessKeyManagerはPresentationSource(≒HWND)単位で管理。Popup HWNDは`WS_EX_NOACTIVATE`を持ち**Win32フォーカスがPopupへ正しく渡らない既知の問題**があり、機能しない/追加対応が要る可能性 | 既存Windowと同一PresentationSourceのため無変更で機能すると推測 |
| (3) 既存UIA操作パターンへの影響 | Popup内要素へは`Descendants`到達は理論上可能だが、フォーカス委譲の不整合リスクが操作の安定性に影響しうる | 影響は軽微。既存の`Find-Ecad2Element`/`Invoke-Ecad2Element`パターンをほぼそのまま使える |

---

## 観点1: UIAツリー上の現れ方・要素特定のしやすさ

- `Popup`自体は独自のビジュアルツリーを持たないが、`IsOpen=true`になった時点で**新しいWindow（別HWND）を
  生成し、その中に`PopupRoot`を配置する**。`Popup.Child`はビジュアル上は新Windowの子だが、論理上は
  `Popup`の子のまま（[Popup - WPF | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup)）。
- `PopupRoot`は`OnCreateAutomationPeer`で`PopupRootAutomationPeer`を返す
  （[PopupRoot.cs (dotnet/wpf)](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/PopupRoot.cs)）。
  つまりPopup内容は独自のAutomationPeerツリーを持つが、別ツリーとして孤立するわけではない。
- Win32 UI Automationの一般規約として、「各HWNDは既定プロバイダを持ち、それを含む親HWNDの子として
  UIAツリー上に現れる」（検索結果より）。ComboBoxのドロップダウン（実装はPopup）がUI Automationツール
  で実務上問題なく操作できることは広く知られており、**別HWNDであること自体がUIA到達性を必ず断つわけ
  ではない**。
- ただし、これは「一般的にはツリーに繋がる」という事実確認に留まり、**この開発機の`Get-Ecad2Root`
  （`AutomationElement.FromHandle(MainWindowHandle)`起点の`Descendants`探索）がPopup内要素まで
  確実に到達できるかは、Popupが実際に開いた状態で実機検証しないと断定できない（推測の域）**。
  同一Window内オーバーレイであれば、この不確実性自体が発生しない。

## 観点2: IsDefault/IsCancelの既定ボタン機構

- `Button.IsDefault=true`は内部的に`AccessKeyManager.Register("\r", ...)`を呼ぶだけの仕組みで、
  `IsCancel=true`も同様に`"\x1b"`（Esc）を登録する
  （[IsDefault – 2,000 Things You Should Know About WPF](https://wpf.2000things.com/tag/isdefault/)、
  [Button.IsDefault Property](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.button.isdefault)）。
- `AccessKeyManager`は**PresentationSource（≒HWND）単位**でキー登録を管理し、フォーカスを持つ
  スコープのウィンドウに対して解決される（[AccessKeyManager Class](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.accesskeymanager)、
  検索結果の解説）。
- **Popup方式特有のリスク（既知の落とし穴、複数の独立情報源で確認）**:
  - Popup HWNDには`WS_EX_NOACTIVATE`スタイルが付き、WPFフレームワークがそのHWNDへフォーカスを
    当てようとしない設計になっている（[Keyboard Handling in WPF Popup Class](https://mike-ward.net/2013/09/23/keyboard-handling-in-wpf-popup-class/)ほか）。
  - 結果として「Win32レベルのフォーカスがPopupのHWNDに乗っていないのに、WPF論理フォーカスだけ
    Popup内要素に移る」という不整合が起こりうる。この状態でEnter/Escを押しても、**キー入力自体が
    Popup HWNDに届かず、AccessKeyManagerのIsDefault/IsCancel解決が機能しない**懸念がある。
  - 回避策として「`Popup`自体ではなく`Popup.Child`をFocusableにする」「`PresentationSource.FromVisual`
    経由でPopupのHwndSourceを取得し明示的に`SetFocus`する」といった追加実装が複数の情報源で
    紹介されており、**標準のFocus()呼び出しだけでは不十分になりうることを示唆**している。
- 同一Window内オーバーレイであれば、ボタン・TextBoxとも既存Windowと同一のPresentationSource上に
  あるため、**AccessKeyManagerの登録スコープが変わらず、現行`ElementPlacementDialog`と同じ挙動を
  素直に維持できる**と推測できる（Window自体の非表示化タイミング管理が変わる点はPopup/オーバーレイ
  どちらでも共通の課題であり、本観点の差ではない）。

## 観点3: `ecad2-ui-automation`スキルの既存操作パターンへの影響

- スキルの`Get-Ecad2Root`は`AutomationElement.FromHandle($proc.MainWindowHandle)`を起点に
  `TreeScope.Descendants`で探索する設計（`helpers.ps1:118-122`, `133-152`）。**現行はモーダル
  `ElementPlacementDialog`を対象にした待ち合わせパターンが無い**（`ShowDialog()`はテスト対象外、
  忍者スキルにモーダルダイアログ操作のヘルパーは見当たらない）ため、非モーダル化そのものによる
  「モーダル前提の待ち合わせが崩れる」直接の影響は限定的と見られる。
- ただし、`Invoke-Ecad2Button`が使う`InvokePattern.Invoke()`は、`ButtonAutomationPeer`実装上
  **`Dispatcher.BeginInvoke(DispatcherPriority.Input, ...)`で非同期に`AutomationButtonBaseClick()`
  を呼ぶ**設計（[ButtonAutomationPeer.cs](http://www.dotnetframework.org/default.aspx/4@0/4@0/DEVDIV_TFS/Dev10/Releases/RTMRel/wpf/src/Framework/System/Windows/Automation/Peers/ButtonAutomationPeer@cs/1305600/ButtonAutomationPeer@cs)、
  隠密WebFetch確認）。これは実質的に**Clickイベント発火の標準経路そのもの**であり、独自の別経路ではない。
  - **T-021増分(vi)の不明点2（`PreviewMouseLeftButtonUp`/`PreviewKeyDown`置き換えがUI Automation
    Invokeにどう影響するか）への回答材料**: `Invoke()`は最終的にClickイベント相当の経路を叩くため、
    もしT-033のOK/キャンセルボタンでも同様に「Click属性を外してPreviewMouseLeftButtonUp/PreviewKeyDown
    の2ハンドラへ分離」する実装を行った場合、**UI Automation経由の`Invoke()`操作はClickイベントを
    発火させるだけで、新設したPreview系ハンドラを経由しない可能性が高い**（T-021のツールバーボタンで
    現に指摘されていた懸念と同型）。現行`ElementPlacementDialog`のOK/キャンセルは`Click`属性のみで
    実装されており（T-021のような分離はされていない）、**T-033でもこの構造を維持する限りこの懸念は
    生じない**。侍の実装設計として、OK/キャンセルボタンに限ってはT-021型のマウス/キーボード分離を
    適用しない（現行のClickハンドラ一本のままにする）ことを推奨する。
- Popup採用時は、上記観点1・2のフォーカス委譲リスクにより、`Send-Ecad2Keys`（`SendKeys.SendWait`、
  フォアグラウンドウィンドウへ送られる）がPopup内`TextBox`へ届くかどうかも同根のリスクとして波及する
  （SendKeysはWin32のフォアグラウンドウィンドウ宛てであり、Popup HWNDがアクティブ化されない設計だと
  意図した宛先に届かない可能性がある）。同一Window内オーバーレイなら、既存の`Set-Ecad2Foreground`
  （メインウィンドウHWNDへの`SetForegroundWindow`）がそのまま有効であり続ける。

---

## 推奨（技術的所見、決定は侍・裁定は家老/殿）

- **同一Window内オーバーレイ（Canvas/Grid上への直接重ね）を推奨**。IsDefault/IsCancel・
  `SendKeys`・既存UIAスキルのいずれも、既存Windowと同一のHWND/PresentationSource/フォアグラウンド
  状態を前提にできるため、変更の影響範囲が最小になる。
- Popupを選ぶ場合は、実装プラン6.2節が示す通り**PoC必須**であり、特に次を実機で確認すべき
  （静的調査では断定不可）:
  1. Popup表示中に`TextBox`へWin32フォーカスが実際に渡るか（`GotKeyboardFocus`等で確認）
  2. その状態でEnter/Escキーが`IsDefault`/`IsCancel`ボタンへ届くか
  3. `ecad2-ui-automation`の`Get-Ecad2Root`(`Descendants`探索)がPopup内要素を発見できるか
  4. `Send-Ecad2Keys`（`SendKeys.SendWait`）がPopup内`TextBox`に届くか

---

## 不明点（実機検証が必要、推測に留まる部分）

1. この開発機・このアプリの実際のPopup実装で、上記の一般的な落とし穴が実際に再現するか（複数の
   独立情報源から確認した「既知の問題」ではあるが、常に発生するとは限らない。回避策併用で問題なく
   動くケースも報告されている）。
2. `ecad2-ui-automation`の`Get-Ecad2Root`がPopup内要素を実際に発見できるかは未検証（一般論として
   到達可能とされるが、この開発機での実測はない）。
3. `PopupRootAutomationPeer`が`ButtonAutomationPeer`の`Invoke()`と同様の非同期Dispatcher経由の
   実装になっているかは未確認（Popup内ボタンへの`Invoke()`操作の非同期性は同様と推測されるが、
   ソース未確認）。

---

## 出典一覧

- [Popup - WPF | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup)
- [PopupRoot.cs (dotnet/wpf, GitHub)](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/PopupRoot.cs)
- [IsDefault – 2,000 Things You Should Know About WPF](https://wpf.2000things.com/tag/isdefault/)
- [Button.IsDefault Property (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.button.isdefault)
- [AccessKeyManager Class (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.accesskeymanager)
- [Keyboard Handling in WPF Popup Class | Mike-Ward.Net](https://mike-ward.net/2013/09/23/keyboard-handling-in-wpf-popup-class/)
- [ButtonAutomationPeer.cs (dotnetframework.org)](http://www.dotnetframework.org/default.aspx/4@0/4@0/DEVDIV_TFS/Dev10/Releases/RTMRel/wpf/src/Framework/System/Windows/Automation/Peers/ButtonAutomationPeer@cs/1305600/ButtonAutomationPeer@cs)（隠密WebFetchで`Invoke()`実装を直接確認）
- `docs/ecad2-t021-focus-design-consolidation-plan-onmitsu.md`（本調査の前提・T-021の未解決の不明点との対応関係）
- `src/Ecad2.App/Views/ElementPlacementDialog.xaml(.cs)`、`.claude/skills/ecad2-ui-automation/{SKILL.md,helpers.ps1}`（現状把握のためのコード確認）
