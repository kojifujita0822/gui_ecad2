# T-110増分3 AutoHide代替UI(案1) 退避コード

コミット分割方針((1)タイトルバー非表示→(2)AutoHide代替UIの2コミット分け、隠密推奨)のため、
2026-07-22セッション終盤に一時的にファイルから削除し退避したコード。次回セッションで
以下をそのまま該当箇所へ再挿入すれば実装(2)が復元できる(簡体字修正済み・正しい表記)。

設計書: `docs/ecad2-t110-increment3-titlebar-hide-and-autohide-ui-design-onmitsu.md` §3.2(案1)

---

## 1. MainWindow.xaml側

挿入位置: `<MenuItem Header="ダークモード(作図色)(_D)" .../>` の直後の `<Separator/>` と、
`<!-- T-058増分4... -->` コメント(現在のレイアウトを既定として保存メニュー直前)の間。

```xml
                <!-- T-110増分3(裁5付帯裁定=AutoHide機能を残す、殿裁定=案1、家老采配2026-07-22): タイトル
                     バー完全非表示(本ファイル内TitleBarHiddenAnchorableControlStyle)に伴いピンでの
                     発動・復帰が困難になる4ペインへ、メニュー経由の代替動線を提供する。発動・復帰とも
                     同じ項目で完結(トグル)。チェック状態はSubmenuOpenedで都度評価する(IsAutoHiddenの
                     変更通知有無が未確認のため、通知に依存しない設計。設計書§3.1-4/§3.2)。 -->
                <MenuItem Header="パネルを自動的に隠す(_A)" SubmenuOpened="AutoHideSubmenu_SubmenuOpened">
                    <MenuItem x:Name="AutoHideLeftPaletteMenuItem" Header="シート(_S)" IsCheckable="True" Tag="LeftPalette" Click="AutoHideMenuItem_Click"/>
                    <MenuItem x:Name="AutoHideDeviceTableMenuItem" Header="機器表(_D)" IsCheckable="True" Tag="DeviceTable" Click="AutoHideMenuItem_Click"/>
                    <MenuItem x:Name="AutoHideRightPanelBottomMenuItem" Header="プロパティ(_P)" IsCheckable="True" Tag="RightPanelBottom" Click="AutoHideMenuItem_Click"/>
                    <MenuItem x:Name="AutoHideOutputPanelMenuItem" Header="出力(_O)" IsCheckable="True" Tag="OutputPanel" Click="AutoHideMenuItem_Click"/>
                </MenuItem>
                <Separator/>
```

つまり、現状(挿入前)は:
```xml
                <MenuItem Header="ダークモード(作図色)(_D)" IsCheckable="True" IsChecked="{Binding IsDarkMode, Mode=TwoWay}"/>
                <Separator/>
                <!-- T-058増分4(殿裁定=保存タイミング両方の1つ、明示コマンド): 現在のパネルレイアウト
```
これを、上記の新規ブロックを`<Separator/>`の直後・`<!-- T-058増分4...`の直前に挿入する形にする。

## 2. MainWindow.xaml.cs側

挿入位置: `UpdateRightPanelBottomTitle()` メソッドの直後、`RegisterDockingContents()` メソッドの直前。

```csharp
    // T-110増分3(裁5付帯裁定、家老采配2026-07-22、設計書§3.1-4): メニューを開くたびに実際の
    // AutoHide状態(LayoutAnchorable.IsAutoHidden)を4項目へ反映する。変更通知の有無が未確認のため
    // Bindingではなく都度評価する設計(通知が無くても正しく動く、通知があっても害はない)。
    private void AutoHideSubmenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        AutoHideLeftPaletteMenuItem.IsChecked = IsPaneAutoHidden("LeftPalette");
        AutoHideDeviceTableMenuItem.IsChecked = IsPaneAutoHidden("DeviceTable");
        AutoHideRightPanelBottomMenuItem.IsChecked = IsPaneAutoHidden("RightPanelBottom");
        AutoHideOutputPanelMenuItem.IsChecked = IsPaneAutoHidden("OutputPanel");
    }

    private bool IsPaneAutoHidden(string contentId)
    {
        var anchorable = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
        return anchorable?.IsAutoHidden ?? false;
    }

    // T-110増分3(裁5付帯裁定、家老采配2026-07-22、設計書§3.1-1/2): 発動・復帰とも同じ項目のトグル。
    // 対象取得はContentId検索(x:Name参照は使わない、レイアウトDeserializeでモデルツリーが丸ごと
    // 差し替わるT-099(c)の教訓)。ToggleAutoHide()はpublic、DockingManager.ExecuteAutoHideCommand
    // (internal)の中身も同メソッド呼出のみと一次ソース確認済み(設計書§3.1-1)。
    private void AutoHideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string contentId }) return;
        var anchorable = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
        anchorable?.ToggleAutoHide();
    }
```

## 3. 復元後の確認手順

1. 上記2箇所をXAML/コードビハインドへ再挿入
2. `dotnet build src/Ecad2.sln` で0エラー0警告を確認(忍者のEcad2.App.exe起動有無を事前に一声確認すること、samurai.md「ビルド前に忍者へ起動確認」参照)
3. `dotnet test src/Ecad2.sln` で既存件数(Core131件/App795件)から変化がないことを確認
4. ContentId整合性再確認: `Grep pattern="ContentId=\"(LeftPalette|DeviceTable|RightPanelBottom|OutputPanel)\"|Value=\"(LeftPalette|DeviceTable|RightPanelBottom|OutputPanel)\"|Tag=\"(LeftPalette|DeviceTable|RightPanelBottom|OutputPanel)\""` で計12箇所(4種×3箇所)一致を確認
5. (1)タイトルバー非表示のコミット→(2)本コードのコミット、の順で2コミットに分ける(隠密推奨、設計書§7)
6. 検証パイプライン(隠密静的レビュー→忍者実機確認)を回す(設計書§6.1・§6.2)

## 4. 現在(2026-07-22セッション終了時点)のファイル状態

- `src/Ecad2.App/MainWindow.xaml`: (1)タイトルバー非表示のみ実装済み(`TitleBarHiddenAnchorableControlStyle`、Window.Resources内)、(2)は未挿入(本ファイルの内容がそれ)
- `src/Ecad2.App/MainWindow.xaml.cs`: (1)の`ApplyDockingManagerThemes`への登録行のみ実装済み、(2)は未挿入(本ファイルの内容がそれ)
- ビルド・テストは(1)のみの状態でまだ実行していない(簡体字修正が直近の最終作業だったため)。次回セッション冒頭でまず(1)のみの状態でビルド・テスト確認→コミット→本ファイルの内容を復元、の順を推奨。

## 5. 補足(初回保存先の訂正)

当初scratchpad(セッション固有の一時ディレクトリ)へ保存していたが、次回セッションから
参照できない可能性に気づき、本ファイル(`docs-notes/`配下、永続)へ移し替えた。
scratchpad側のファイルは無視してよい(次回セッションでは別セッションIDのため実質アクセス不能)。
