# T-101 配置ツール選択中ツールの恒久ハイライト表示 — 静的レビュー（隠密）

**対象コミット**: `ba0ebc7`（feat(app): T-101 - 配置ツール選択中ツールの恒久ハイライト表示）
**レビュー日**: 2026-07-21
**effort**: low（家老指定=1周目軽量既定）
**手法**: `code-review`スキルはdisable-model-invocationにより本セッションから起動不可（既知の制約、onmitsu.md記載）。`git show ba0ebc7 -- <path>`で範囲を明示した手動レビューで代替。

## 結論

概ね妥当。実装は殿裁定（視覚表現=背景色+枠線、対象=配置ツールボタン群のみ）通り。
**忍者実機確認で重点確認してほしい観点1件**（下記所見1）と、**設計判断の確認要1件**（下記所見2）を指摘する。いずれも実装ミスというより実機での見え方・意図の確認が必要な事項。

## 所見1（実機確認要）: 選択中ボタン押下時のMouseOver/Pressedフィードバック

`PlacementToolBarButtonStyle`（`ToolBarButtonStyle`をBasedOn、さらに基底はApp.xaml暗黙的`Button`スタイル）の3層継承において、新設DataTrigger（ActiveToolTag一致）は最派生層のStyle.Triggersに位置する。

WPFの継承チェーンでのTrigger優先順位は「派生Styleのtriggerが基底Styleのtriggerより後にマージされ、後勝ちルールにより優先される」というのがT-089で実証済みのパターン（`ToolBarButtonStyle`のIsEnabled=Falseトリガーが基底App.xamlのIsEnabledトリガーを上書きした実績、MainWindow.xaml 37-58行目）。今回のDataTriggerも同型のはずで、これ自体は理論上妥当（`git show`確認: App.xaml側`ControlTemplate.Triggers`(231-244行)はBackground/BorderBrushを一切触らずVisibility/文字色のみを扱う構造に既に整理済み=T-089対処後、PR-20型の握り潰しリスクは無い）。

ただし、この継承チェーン優先順位により、**選択中のボタンにMouseOverまたはPressedが同時発生しても、DataTriggerが常に勝ちActive背景色を維持し続ける**。「常時ハイライト」という目的には合致するが、副作用として「選択中ボタンを押した際の一時的な押下フィードバック（Button.Pressed.Background）が視覚的に隠れる」可能性がある。パターン台帳PR-23（既定テーマスタイル差替時の複数状態トリガー相互作用見落とし）と類似の性質——バグではなく設計上のトレードオフだが、実機で「選択中ボタンを押しても押した感が全く無い」ことが許容範囲か、忍者に確認いただきたい。

## 所見2（設計判断の確認要）: 自作パーツ選択後のF11ボタンのハイライト消失

`MainWindowViewModel.ResolvePlacementToolTag()`（同ファイル67-73行目）:
```csharp
private string? ResolvePlacementToolTag()
{
    if (Tool.PartId is not string partId) return "PartSelection";
    var entry = PartPalette.Entries.FirstOrDefault(e => e.Category == "" && e.Definition.Id == partId);
    if (entry is null) return null;
    return Tool.IsOr ? $"OR:{entry.Definition.Name}" : entry.Definition.Name;
}
```

F11ボタン押下直後（`Tool.PartId == null`）は`"PartSelection"`を返しF11自体がハイライトされる。しかしそこから自作パーツパネルで**組み込み部品でない**パーツ（`Category != ""`）を実際に選ぶと、`entry`が見つからず`null`を返し、**どのボタンもハイライトされなくなる**（組み込み部品を選んだ場合はF5〜F8等の対応ボタンへハイライトが移る、という対称的な挙動とは異なる）。

自作パーツ配置中は「現在有効なツールが視覚的に分からない」状態になるが、これがバグなのか意図的な範囲外（対象範囲は「配置ツールボタン群」であり自作パーツ個々の項目はボタン化されていないため、対応するボタンが無いのは当然、という解釈もありうる）かは実装からは判定できない。対象範囲に「自作パーツ等」が含まれる（todo.md T-101節）ため、F11ボタン自体の恒久ハイライトが自作パーツ選択後も維持されるべきか、家老・殿の意図を確認されたい。

## 確認して問題なしと判定した点

- **`IsFreeLineDraftHorizontal`との整合**（当初「記入直後にドラフト未確定で誤方向表示では」と疑ったが、コード精読で解消）: `Tool.Mode==PlaceLine`である間は`_freeLineDraft`が必ず非null という不変条件が成立する。`BeginFreeLineDraft`（MainWindowViewModel.cs 1965-1970行）が`_freeLineDraft`とToolを同一メソッド内で連続セットし、`ClearFreeLineDraftIfAny`（2008-2014行）も同様に同時クリアする。`Tool = new ToolState(PlaceLine)`の代入箇所はコードベース全体で当該1箇所のみ（grep確認済み）。よってF9/Shift+F9のハイライト方向誤判定は起きない。
- **`ResolvePlacementToolTag`とTagの対称性**: `BuiltinPlaceButton_Click`→`ActivateBuiltinTool`→`PartPalette.Entries.FirstOrDefault(pe => pe.Category=="" && pe.Definition.Name==partName)`という既存の名前解決と、今回の逆引き（PartId→Name）が同じ`Category==""`条件・同じTag文字列体系（"a接点"/"OR:a接点"等）で対称。F5〜F8の既存Tag（変更なし）と整合。
- **接続点記入・配線分断記入（F10系）の対象外扱い**: MainWindow.xaml diffにTag追加なし、`ActiveToolTag`のswitch式にも対応するcaseなし。todo.md T-101節に殿確認済みと明記の設計判断通り。
- **StringEqualsMultiConverter**: null安全（`values[0] is string`でfalseフォールバック）、ConvertBackはNotSupportedException（MultiBinding片方向のみの用途と合致）。

## 不明点

家老采配メッセージにあった「DoD(3)」に対応する番号付きDoDリストは、`docs/todo.md` T-101節（237-256行目）には見当たらなかった。侍への采配文（peerメッセージ）側にのみ存在する可能性がある。todo.md上の記述との1対1突き合わせは今回不可、家老采配文の原文を前提に本レビューの観点（MouseOver/Pressed/テストモード色分けとの共存）を確認した。

なお「テストモード色分け（IsPressed+IsChecked MultiTrigger）」は`TestModeToolBarButtonStyle`（`ToggleButton`型、テストモードON/OFFボタン専用、MainWindow.xaml 90-124行目）であり、`PlacementToolBarButtonStyle`（`Button`型）とは型・適用対象とも完全に別スタイルのため、直接の共存競合は構造的に発生しない。

## 派生提案

なし。
