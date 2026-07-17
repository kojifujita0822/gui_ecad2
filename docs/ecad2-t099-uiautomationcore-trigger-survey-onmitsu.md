# T-099追加調査: UIAutomationCore発火源の特定と対処案(隠密)

調査日: 2026-07-17　調査者: 隠密（key=1784276632853）　依頼元: 家老（殿裁定、観測者効果判明後の追加調査）

## 依頼内容(DoD)

(a) 家老仮説（COM/UIA cross-apartment呼び出しがメッセージポンプを早回しする）の裏付け
(b) `DispatcherFrame.PushFrame`等、アプリ内完結でネストしたメッセージポンプを発生させる具体的実装案
(c) 他の可能性の併記

## 結論(先出し)

1. **家老仮説は一次ソースでは裏付けられず、より直接的な別経路が確認できた**。`Dispatcher.PushFrameImpl`（`WindowsBase/System/Windows/Threading/Dispatcher.cs`）は単なるWin32 `GetMessageW`ループであり、既存のDispatcherメインループと本質的に同じ処理（`TranslateAndDispatchMessage`）を行うだけで、それ自体に「保留中レイアウトを早回しする」特別な機構は見当たらなかった。よって`DispatcherFrame.PushFrame`を単体で呼ぶだけでは効果が薄いと予想する（**推測、実機未検証**）。
2. **真の発火源は`WM_SIZE`メッセージによる強制Measureと判明**。`HwndSource.cs`の`Process_WM_SIZE`（1401行〜）は、`WM_SIZE`受信時に**ルートUIElementへ同期的な`Measure()`呼び出しを強制実行**する実装になっている（一次ソースで確認）。これは侍が確認した「Win32 `MoveWindow`が効く」という実験結果と完全に一致する直接的技術的根拠である。
3. **対処案（本命）**：`DispatcherFrame.PushFrame`ではなく、アプリ内から`SetWindowPos`/`MoveWindow`相当のWin32 API（P/Invoke）を自己呼び出しし、`WM_SIZE`を意図的に発火させる。これは侍が既に効果を確認済みの操作をアプリ内で完結させる形に置き換えるものであり、確実性が高い。

---

## 1. 家老仮説の検証：`Dispatcher.PushFrame`はメッセージポンプの「早回し」を行わない

出典: [dotnet/wpf](https://github.com/dotnet/wpf) `main`ブランチ、`src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/Dispatcher.cs`（2026-07-17取得、全2876行中`PushFrameImpl`2046-2095行を精読）。

```csharp
private void PushFrameImpl(DispatcherFrame frame)
{
    ...
    while(frame.Continue)
    {
        if (!GetMessage(ref msg, IntPtr.Zero, 0, 0))
            break;
        TranslateAndDispatchMessage(ref msg);
    }
    ...
}
```

`PushFrameImpl`は文字通りWin32の`GetMessageW`（`GetMessage`ラッパー、1098行台）を呼び出す標準的なメッセージループであり、これはWPFのメインメッセージループ（アプリ起動時に`Dispatcher.Run()`が回している外側のループ）と実装上完全に同一の処理内容である。`DispatcherFrame`はこのループを「ネストして」実行できるようにする仕組みに過ぎず、キューに存在するメッセージを処理する点では外側のループと変わらない。

**結論**：もし現在のWin32メッセージキューに`WM_SIZE`等のジオメトリ関連メッセージがそもそも存在しなければ（＝それを発行する送信元がなければ）、`PushFrame`でネストしたループを作っても何も起きないと考えられる（**推測**）。つまり「メッセージポンプを回すこと自体」に効果があるのではなく、「回された結果、何が処理されるか」が本質であり、家老仮説（COM cross-apartment呼び出し一般が万能に効く）はやや過度に一般化されている可能性が高い。

## 2. 真の発火源：`HwndSource.Process_WM_SIZE`による強制Measure

出典: `src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndSource.cs`（2026-07-17取得、全2843行中`Process_WM_SIZE`1401-1470行を精読）。

```csharp
private void Process_WM_SIZE(UIElement rootUIElement, IntPtr hwnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
{
    ...
    if ((!_myOwnUpdate) && (_sizeToContent != SizeToContent.WidthAndHeight) && !_isWindowInMinimizeState)
    {
        ...
        if (_adjustSizingForNonClientArea)
        {
            rootUIElement.InvalidateMeasure();   // 明示的な無効化
        }
        ...
        rootUIElement.Measure(sz);               // ★ 同期的な強制Measure（無条件）
        ...
    }
}
```

`WM_SIZE`をWndProcで受信すると（1230-1231行、`WindowMessage.WM_SIZE`のcaseで`Process_WM_SIZE`を呼び出し）、`_myOwnUpdate`（WPF自身が能動的にリサイズ中でない、つまり外部からのサイズ変更通知である場合）かつ`SizeToContent.WidthAndHeight`でない場合、**ルートUIElement（Windowのコンテンツツリー全体）に対して同期的な`Measure()`呼び出しが無条件で実行される**。これはコメント「Invalidating layout here ensures that we do layout」が示す通り、意図的にレイアウトの強制実行を保証する実装である。

**これが侍実験結果の直接の技術的根拠**：Win32の`MoveWindow`（あるいは`SetWindowPos`）は、たとえウィンドウの実座標・サイズが変化しなくても、通常`WM_WINDOWPOSCHANGING`→`WM_SIZE`→`WM_WINDOWPOSCHANGED`のメッセージシーケンスをWindowsが発行する。この`WM_SIZE`がWPFの`HwndSource.WndProc`に届くことで、`Process_WM_SIZE`がルート要素の強制`Measure()`を引き起こし、それまで保留状態だった配置ツールバー2段目の`SelectedContent`のレイアウトも、このルートMeasureパスの一環として正しく再計算されると推定できる。

## 3. 外部UIAクライアント(`FindAll`)が効いた理由（推測）

`UIAutomationCore.dll`はクローズドソースのため一次ソースでの確認はできないが、以下の間接的な根拠から推測する。UI Automationのプロセス間ブリッジは、対象ウィンドウのオートメーションツリー取得に際して`WM_GETOBJECT`メッセージを送信するのが標準的な仕組み（MSAA/UIA共通の基盤）である。この処理自体が直接`WM_SIZE`を発行するわけではないと考えられるが、UI Automationクライアントが要素の座標・可視性を検証する過程で、内部的に`GetWindowRect`やウィンドウの再描画・再検証に関連するAPI（結果として`WM_SIZE`系メッセージを誘発しうるもの）を呼び出している可能性は排除できない（**推測、UIAutomationCore.dll内部実装は非公開のため一次ソースでの確認は不可能**）。

侍の実験で「アプリ内でAutomationPeerツリーを明示的に構築・走査（`GetChildren()`再帰）しても効果がなかった」という結果は、本調査の結論（本質はWM_SIZE経由の強制Measure）とも整合する——`AutomationPeer.GetChildren()`や`GetBoundingRectangleCore()`（`UIElementAutomationPeer.cs`172-192行で確認、既存の`_owner.RenderSize`を読むだけで明示的なレイアウト強制コードは含まない）は、いずれもマネージド層で完結する処理であり、Win32メッセージ（`WM_SIZE`等）を一切発行しないため、効果がなかったのは論理的に整合する。

## 4. 対処案

### 本命（DoD(b)の代替案）：Win32 `SetWindowPos`/`MoveWindow`のアプリ内自己呼び出し

```csharp
[DllImport("user32.dll")]
private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

[DllImport("user32.dll")]
private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

// ContentRendered等、Window表示直後のタイミングで実行
var hwnd = new WindowInteropHelper(this).Handle;
if (GetWindowRect(hwnd, out RECT rect))
{
    MoveWindow(hwnd, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, true);
}
```

現在の座標・サイズをそのまま指定して`MoveWindow`を呼ぶ（実質的な無変化リサイズ）。これは侍が既に実機で効果を確認済みの操作をアプリ内で完結させる形であり、家老提案の`DispatcherFrame.PushFrame`より確実性が高いと考える（本調査の技術的根拠に基づく判断）。呼び出しタイミングは、`ContentRendered`イベント（Visual Treeの初回レンダリング完了後）が妥当と推定する（**推測、複数タイミングでの実機比較検証を推奨**）。

### 次善：`SetWindowPos`with `SWP_FRAMECHANGED`

`MoveWindow`の代わりに`SetWindowPos`を`SWP_NOMOVE | SWP_NOZORDER`（位置・Z順を変えない）で呼び、必要なら`SWP_FRAMECHANGED`（非クライアント領域の再計算を強制）を追加する案。`MoveWindow`より意図が明確（「サイズ変更」ではなく「再計算の強制」という体裁になる）だが、実際に`WM_SIZE`が発行されるかは`SetWindowPos`のフラグの組み合わせに依存するため、まず本命案（`MoveWindow`）から試すことを推奨する。

### 参考（否定的所見）：`DispatcherFrame.PushFrame`単体

家老提案の`DispatcherFrame.PushFrame`は、1節の解析により「単なるネストしたメッセージループ」であり、それ自体が`WM_SIZE`等のジオメトリメッセージを新たに生成するわけではないため、単体では効果が薄いと予想する（**推測、実機未検証。もし試すなら本命案と比較実験することを推奨**）。

## 5. 不明点

- `MoveWindow`自己呼び出し案は隠密の調査範囲では実機検証していない（**スコープ外、侍・忍者による実装・検証が必要**）。
- 外部UIAクライアントが`WM_SIZE`を間接的に誘発する正確なメカニズムは`UIAutomationCore.dll`内部実装（非公開）に依存するため未解明（**推測に留まる**）。
- `_adjustSizingForNonClientArea`が`false`の場合（`InvalidateMeasure()`を明示的に呼ばない経路）でも1466行の`rootUIElement.Measure(sz)`は無条件に実行されるため、今回の効果発現に`_adjustSizingForNonClientArea`の値がどちらであるかは影響しないと考えられるが、ecad2の`MainWindow`（通常のオーナードローなしWindow）における実際の値は未確認（**推測、実害への影響は薄いと見るが念のため記載**）。

## 派生提案の有無

範囲外の新規作業提案なし。

---

## 出典

- [dotnet/wpf](https://github.com/dotnet/wpf) `main`ブランチ：
  - `src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/Dispatcher.cs`（2026-07-17取得、`PushFrameImpl`2046-2095行、`GetMessage`2098-2147行）
  - `src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/DispatcherFrame.cs`（2026-07-17取得、全文）
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndSource.cs`（2026-07-17取得、全2843行中`Process_WM_SIZE`1401-1470行、WndProcでのWM_SIZE呼び出し1230-1231行）
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Automation/Peers/AutomationPeer.cs`（2026-07-17取得、`GetBoundingRectangle`739-754行）
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Automation/Peers/UIElementAutomationPeer.cs`（2026-07-17取得、`GetBoundingRectangleCore`172-192行）
- `docs/todo.md` T-099節（侍実機検証結果「観測者効果」判明の経緯、94-106行）
- `docs/ecad2-t099-selectedcontent-collapse-root-cause-survey-onmitsu.md`（前回調査、自己参照）
