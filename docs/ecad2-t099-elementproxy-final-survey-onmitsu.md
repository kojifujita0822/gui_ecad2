# T-099最終ラウンド調査: ElementProxy/UIAutomationCoreコールバック機構の深掘り(隠密)

調査日: 2026-07-17　調査者: 隠密（key=1784276632853）　依頼元: 家老（殿裁定、最終ラウンド・打ち切り条件つき）

## 経緯

前回調査（`docs/ecad2-t099-uiautomationcore-trigger-survey-onmitsu.md`）で提示した「WM_SIZE経由の強制Measure」理論は、侍のWndProcフック実測により**否定された**——WM_SIZE自然発生時点(626ms)ではActualHeight=18のまま変化なし、UIA FindAllによる正常化時点(85782ms)ではその間WM_SIZEが一度も発生していなかった。前回の理論は一次ソースとしては正確だったが、本現象の発火源ではないと確定した。この結果を率直に受け止め、本ラウンドではWM_SIZE以外の経路（UIAutomationCoreのプロバイダコールバックが直接Measure/Arrangeを誘発する経路の有無）を調査する。

## 依頼内容(DoD)

発火源の特定、または「文書化されておらず特定不能」という明確な結論のいずれかを示すこと。

## 結論(先出し)

**「特定不能」——ただし手がかりは1点得られた。** WPFのUI Automationプロバイダ実装（`ElementProxy.cs`＝実際のCOMラッパー`IRawElementProviderFragment`/`IRawElementProviderSimple`）を一次ソースで精読したが、Measure/Arrange/UpdateLayout/InvalidateMeasure/InvalidateArrangeを直接呼び出すコードは**一切存在しなかった**。唯一の手がかりは、外部UIAクライアントからのコールバックをUIスレッドへマーシャリングする`ElementUtil.Invoke`が、`Dispatcher.Invoke(DispatcherPriority.Send, ...)`という通常のアプリコードでは稀な最高優先度の同期呼び出しを使っている点——これがレイアウトフラッシュの真因である可能性は残るが、一次ソースレベルでの因果関係の確証までは得られなかった。家老指示の「深追いしすぎぬこと」を踏まえ、ここで区切りとする。

---

## 1. `ElementProxy.cs`：実際のCOMプロバイダ実装にレイアウト強制コードなし

出典: [dotnet/wpf](https://github.com/dotnet/wpf) `main`ブランチ、`src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/Automation/ElementProxy.cs`（2026-07-17取得、全549行）。

`AutomationPeer.ProviderFromPeer()`（前回調査で確認済み、`AutomationPeer.cs`1795-1807行）は`ElementProxy.StaticWrap(peer, referencePeer)`を呼び、`AutomationPeer`を実際のCOMインターフェース（`IRawElementProviderFragment`等）でラップする。この`ElementProxy`クラス自体が、外部UIAクライアントの`FindFirst`/`FindAll`/`GetPropertyValue`等の呼び出しを受ける実体である。

全549行を`Measure`/`Arrange`/`UpdateLayout`/`Invalidate`で検索したが**該当なし**。つまり、UI Automationのコールバック実装自体には、WPFのレイアウトシステムを明示的に呼び出す・強制するコードは存在しないと確認できた（一次ソースでの明確な事実）。

## 2. `ElementUtil.Invoke`：`DispatcherPriority.Send`による同期マーシャリング

出典: 同リポジトリ`src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/Automation/ElementUtil.cs`（2026-07-17取得、全315行）。

外部UIAクライアントからのコールバック（COM経由、通常はUIAutomationCore.dllが管理する別スレッド上で受信される）を、WPFのUIスレッドで実行するためのマーシャリング処理：

```csharp
internal static object Invoke(AutomationPeer peer, DispatcherOperationCallback work, object arg)
{
    Dispatcher dispatcher = peer.Dispatcher;
    ...
    object retVal = dispatcher.Invoke(
        DispatcherPriority.Send,
        TimeSpan.FromMinutes(3),
        (DispatcherOperationCallback) delegate(object unused) { ... return work(arg); ... },
        null);
    ...
}
```

`DispatcherPriority.Send`はWPF Dispatcherの優先度体系における最高優先度であり、通常のアプリコード（`BeginInvoke`での`Normal`/`Background`/`ContextIdle`等）とは一線を画す特別な扱いを受ける（`Dispatcher.cs`1275行付近、同一スレッドからの呼び出しならキューを経由せず即座に同期実行、異なるスレッドからならキューに積まれるが最優先で処理される、と`Dispatcher.cs`のコードから読み取れる）。

**この事実が示唆すること（推測、確証には至らず）**：UI Automationからのコールバックは、通常のアプリイベント処理（`Normal`優先度）より高い優先度でUIスレッドに割り込む。この「割り込み処理」自体が、Dispatcherのキュー内で保留中だった他の操作（レイアウトパス等、通常`Loaded`/`Render`優先度で登録される）の処理順序や実行タイミングに何らかの副次的な影響を与える可能性は排除できないが、**「Send優先度の操作が処理される」ことと「保留中のMeasure/Arrangeが強制実行される」ことの間の直接的な因果関係を示す一次証拠は、`ElementProxy`/`ElementUtil`のレベルでは見つからなかった**。

## 3. Web上の既知情報

「UI Automationがレイアウトを強制する」系の既知issueをMS Learn・GitHub issueで検索したが、本件にピンポイントで一致する情報は見つからなかった（`docs/ecad2-t099-uiautomationcore-trigger-survey-onmitsu.md`で実施した前回のWeb調査を踏まえ、本ラウンドでは深追いを避け一次ソース調査に絞った）。

## 4. 調査の限界（打ち切り理由）

- `Dispatcher`の内部キュー処理ロジック（`PromoteAndReprioritizeQueueItems`等、優先度の昇格・再順序付けに関わるより深い内部実装）まで踏み込めば、`DispatcherPriority.Send`が具体的にどう他の保留中操作の順序へ影響するかの詳細が分かる可能性はあるが、これは`Dispatcher.cs`全2876行のさらに深い解析を要し、家老指示の「深追いしすぎぬこと」の範囲を超えると判断した。
- `UIAutomationCore.dll`自体はネイティブ・非公開実装のため、一次ソースでの確認は原理的に不可能。
- WPFの`AutomationPeer`から実際のレイアウトツリーへのアクセス経路（`UIElementAutomationPeer.GetBoundingRectangleCore()`、前回調査で確認済み）は既存の`RenderSize`を読むだけであり、これ単体では強制Measureのトリガーにならないことは前回調査と合わせて確認済み。

## 結論・DoDへの回答

**「文書化されておらず特定不能」**と結論する。UIAutomationCore.dllのプロバイダコールバックがWin32メッセージを介さずに直接Measure/Arrangeを誘発する明示的なコードパスは、WPF側（`ElementProxy`/`ElementUtil`、マネージド層で確認可能な範囲）には存在しない。唯一得られた手がかり（`DispatcherPriority.Send`による同期呼び出しという特殊経路）は、今後さらに深掘りする糸口にはなり得るが、今回のスコープ・時間制約内では確証を得られなかった。

家老裁定どおり、この方向はここで打ち切りを推奨する。対症療法（メニュー開閉トリック、`IsSubmenuOpen`のtrue→false）復元の方針に異論なし——これも実質的には「UIスレッドに何らかの高優先度の処理を挟む」という点で、今回発見した`DispatcherPriority.Send`の経路と間接的に類似する操作であり、対症療法自体の効果とも整合的と考える（**推測**）。

## 派生提案の有無

範囲外の新規作業提案なし。

---

## 出典

- [dotnet/wpf](https://github.com/dotnet/wpf) `main`ブランチ：
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/Automation/ElementProxy.cs`（2026-07-17取得、全549行、Measure/Arrange/UpdateLayout/Invalidate該当なしを確認）
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/Automation/ElementUtil.cs`（2026-07-17取得、全315行、`Invoke`メソッド175-220行）
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Automation/Peers/AutomationPeer.cs`（前回調査で取得済み、`ProviderFromPeer`1795-1807行）
  - `src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/Dispatcher.cs`（前回調査で取得済み、`InvokeImpl`1312-1397行、`DispatcherPriority.Send`分岐1275行）
- `docs/todo.md` T-099節（侍WndProcフック実測結果、WM_SIZE理論の反証経緯）
- `docs/ecad2-t099-uiautomationcore-trigger-survey-onmitsu.md`（前回調査、WM_SIZE理論・自己参照）
- `docs/ecad2-t099-selectedcontent-collapse-root-cause-survey-onmitsu.md`（初回調査、自己参照）
