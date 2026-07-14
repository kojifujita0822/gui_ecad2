# T-087設計見直し: F11/Shift+Tab循環と部品パネル起動の設計(隠密、2026-07-14)

殿裁定により一旦立ち止まり、往復4周連続で同一アプローチ(ガード追加)が不完全な修正に終わった
根本原因を設計面から見直す。

## 経緯の整理

往復4周を通じて次々発見された副作用は、いずれも**単一の根本原因**に起因する:

`ActivateOpenPartSelection()`が「記入中ドラフト無条件破棄(`CancelResidualDraftForToolSwitch`)」
「Tool無条件上書き(`new ToolState(PlaceElement)`、PartId=null)」という2つの副作用を持つメソッドを、
**フォーカス移動という軽量操作(F11単発・Shift+Tab循環)から無条件に呼んでいる**ことが根本原因。
往復1〜4周目は「呼ぶこと自体」は変えず、呼ぶ条件(CanEditDiagramガード)を後追いで足す対症療法を
重ねたため、CanEditDiagramではカバーできない副作用2・3(通常編集中に起きる)を毎回見落とし続けた。

## 論点1: Toolの強制切替は本当に必要か(フォーカス移動のみへ遅延できないか)

**成立しない**。`MainWindow.xaml:559-563`のコメントが明記するとおり、右下パネルは
「プロパティ⇔部品選択」を`Tool.Mode==PlaceElement`で排他的に切り替える設計(T-026段階4-7、
案B=GX Works3式)。部品選択パネル(`PartSelectionList`)は`Tool.Mode!=PlaceElement`の間
`Visibility=Collapsed`であり、**WPFはCollapsed要素へKeyboard.Focus()できない**(今回3周に渡り
確認済みの制約)。つまり「フォーカスだけ先に移し、Tool切替は実際のクリック時まで遅延する」設計は、
現行のXAML構造(下段パネルの排他表示)を前提とする限り**技術的に不可能**。

これを覆すには「部品選択パネルとプロパティパネルを常時DOM上に存在させ、強調表示だけ切り替える」
等、T-026で確立した下段パネル排他表示という設計思想自体を変更する必要があり、影響範囲が
本タスク(F11/Shift+Tab)の枠を大きく超える。**本タスクの範囲では現行のTool切替必須という前提を
維持するのが妥当**と判断する。

## 論点2・3: 記入中ドラフト・武装済みToolが存在する場合の振る舞い(選択肢整理)

### 記入中ドラフト(縦コネクタ/自由線/画像挿入)がある場合

- (a) 無条件で切り替え・破棄(現行、往復1〜4周とも未解消): データロス感がありユーザー体験として
  最も危険。特にF11/Shift+Tabは「ナビゲーション操作」という軽量な位置づけのため、ここで記入内容が
  消えるのは直感に反する。
- (b) 確認ダイアログを挟む: 技術的には可能だが、F11単発・Shift+Tab連打のたびにダイアログが出ると
  過剰(特にShift+Tab循環は本来ノーモーダルな操作)。
- **(c)【推奨】記入中はこの操作自体を抑制する**: 既存の`HasAnyDraft`プロパティ
  (`MainWindowViewModel.cs:1674`、T-069往復3周目で確立済み、`_connectorDraft is not null ||
  _freeLineDraft is not null || _imageInsertDraft is not null`)をそのまま流用でき、追加実装が
  最小で済む。記入中は「今まさに配置作業の途中」であり、この状態でF11/Shift+Tabを押しても
  部品パネルへ移動する必然性自体が薄い。

### 武装済みTool(PartId確定済み、Tool.Mode==PlaceElement)がある場合

このケースは実は(a)(b)(c)のいずれでもなく、**より良い第4の選択肢が成立する**:
`Tool.Mode`が既に`PlaceElement`であるなら、`IsPartSelectionVisible`は既にtrueであり
**部品選択パネルは既に開いている**。この場合`ActivateOpenPartSelection()`(Tool再代入)を
呼ぶ必要が無く、単に`FocusPanel(PartSelectionList)`だけで足りる。`new ToolState(PlaceElement)`
での上書き自体を回避できるため、武装状態(PartId)は自然に保持される。

## 実装方針(案)

```csharp
private void ActivateAndFocusPartSelection()
{
    if (!_viewModel.CanEditDiagram) return;
    // 選択肢(c): 記入中ドラフトがある間はこの操作自体を抑制する。
    if (_viewModel.HasAnyDraft) return;
    // 既にTool.Mode==PlaceElement(武装済み含む)ならTool再代入不要、フォーカス移動のみで足りる。
    if (_viewModel.Tool.Mode != ViewModels.ToolMode.PlaceElement)
        ActivateOpenPartSelection();
    Dispatcher.BeginInvoke(new Action(() => FocusPanel(PartSelectionList)), DispatcherPriority.Loaded);
}
```

この設計で、副作用1(CanEditDiagramガード、既存)・副作用2(HasAnyDraftガード、新規)・
副作用3(Tool.Mode既にPlaceElementなら再代入しない、新規)の3つ全てが構造的に解消される
(個別の対症療法ではなく、メソッド自体が「呼んでよい条件」を内包する設計)。

## 殿確認が必要な論点(UI/UX判断を伴う)

1. **記入中ドラフトがある状態でF11/Shift+Tab循環がPartSelectionListの番に来た場合、ユーザーへの
   フィードバックは何も出さず単に無反応でよいか、それともステータスメッセージ(例:「記入中は
   部品パネルを切り替えられません」)で理由を示すべきか。** F11は他の記入中ガード済みショートカット
   (F5〜F10等、記入中はCanEditDiagram等で無反応)と同じ「無反応」で一貫性があると考えるが、
   Shift+Tab循環の場合は毎回この番で「詰まる」ように見える可能性があり、無反応か・次のパネルへ
   スキップするか、いずれが自然か殿の感覚を伺いたい。
2. **Shift+Tab循環でPartSelectionListの番がHasAnyDraft等により抑制された場合、循環インデックスを
   1つ進めて次のパネル(SheetNavList)へ移すべきか、それともその場に留まる(フォーカス変化なし)べきか。**
   前者はUXとして「循環が滑らかに続く」利点があるが実装がやや複雑になり、後者は実装がシンプルだが
   ユーザーがShift+Tabを繰り返し押す必要が生じる。

いずれも軽微なUX選好の論点であり、実装自体は上記方針案でどちらの答えでも対応可能。殿の裁定を
仰いだうえで侍へ実装采配されたい。

## 影響範囲

- 変更対象: `src/Ecad2.App/MainWindow.xaml.cs`の`ActivateAndFocusPartSelection`(既存メソッドの
  中身変更のみ、F11ケース・CyclePanelFocus内呼び出しは変更不要)。
- 新規依存: `HasAnyDraft`(既存プロパティ、追加実装不要)。
- 既存のCanEditDiagramガード・共有ヘルパー化(往復3〜4周目の成果)はそのまま活かせる。

## 出典

- `src/Ecad2.App/MainWindow.xaml:559-563`(下段パネル排他表示設計、T-026)
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1669-1687`(`HasAnyDraft`・
  `CancelResidualDraftForToolSwitch`、T-069往復3〜4周目で確立済みの同型パターン)
- `src/Ecad2.App/MainWindow.xaml.cs`(`ActivateOpenPartSelection`2339-2345行付近、
  `ActivateAndFocusPartSelection`2705-2712行付近、いずれもコミット60fd2c1時点の行番号)

## 不明点

なし(選択肢はいずれも技術的に実装可能、殿確認事項は上記2点のUX選好のみ)。
