# T-083向け調査: WPF Fluentテーマ/ThemeMode 成熟度確認(隠密)

調査日: 2026-07-14　調査者: 隠密　依頼元: 家老（T-083 UIクロームダークモード候補の裏取り）

## 調査題目

.NET標準のWPF Fluentテーマ + `ThemeMode`（Light/Dark/System）が、ecad2のUIクローム
（メニュー・ツールバー・ダイアログ等）向けダークモード候補として、新規外部依存なしで
実運用に耐える成熟度にあるか。

## 結論（要約）

**現時点（.NET 10世代、2026年前半）でも本機能は公式に実験的機能のままであり、UIクローム
全体の本格ダークモードを任せるには時期尚早**と判断する。新規依存なしという利点はあるが、
下記の未解決課題が複数残る。採用する場合は別途PoC検証（MessageBox・AvalonDock併用箇所の
実機確認）が必須。

## 事実（出典付き）

1. **導入バージョン**: Fluentテーマと`ThemeMode` APIは **.NET 9で導入**。
   `Application.ThemeMode` / `Window.ThemeMode`にLight/Dark/System/Noneを設定可能。
   出典: [Microsoft Learn: What's new in WPF for .NET 9](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)
2. **実験的機能のまま**: コードからの`ThemeMode`変更は`[Experimental("WPF0001")]`指定され、
   `WPF0001`警告の抑制が必須。.NET 10（2025年11月GA）時点でもこの実験扱いは解除されていない。
   出典: [dotnet/wpf: using-fluent.md](https://github.com/dotnet/wpf/blob/main/Documentation/docs/using-fluent.md)、
   [ThemeMode Struct](https://learn.microsoft.com/en-us/dotnet/api/system.windows.thememode?view=windowsdesktop-10.0)
3. **.NET 10公式Whatʼs newにも「Fluent UI style support is still in progress」と明記**。
   出典: [What's new in WPF for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100)
4. **構造的弱点**: Fluentテーマは`App.xaml`へ手動でリソース読み込みする方式で、WPF内部の
   「テーマスタイル」機構（真のシステムテーマ差し替え）には乗っていない。独自カスタムスタイルを
   当てたコントロールはFluentの見た目を失う。
   出典: [badecho.com: "WPF's Fluent Theme Looks Good — But It's Not a Theme"](https://badecho.com/index.php/2025/09/03/fluent-theme-not-a-theme/)
5. **サードパーティコントロール非対応**: 明示的にFluentリソースをマージしない限りテーマが
   適用されない（同上出典）。
6. **MessageBoxがFluent非対応**: .NET 9〜10時点で`MessageBox`は旧来の見た目のまま。
   Issue化済み・未解決。出典: [dotnet/wpf Issue #10415](https://github.com/dotnet/wpf/issues/10415)
7. **ダークモード時のフラッシュ不具合**: `window.Show()`直後に白い画面が一瞬表示される問題
   （2025年2月報告）。原因未特定・ワークアラウンドなし・未解決。
   出典: [dotnet/wpf Issue #10513](https://github.com/dotnet/wpf/issues/10513)
8. **`ThemeMode="System"`の追従動作**: レジストリ経由でOS設定を検知し
   `ThemeManager.OnSystemThemeChanged()`で追従。ハイコントラストは`SystemParameters.HighContrast`
   で検知。出典: [DeepWiki: Theme Modes and Switching](https://deepwiki.com/dotnet/wpf/5.2-theme-modes-and-switching)
9. .NET 10でコントロールスタイル追加（DatePicker/GridSplitter/GridView/GroupBox/Label/
   RichTextBox/TextBox等）やハイコントラストクラッシュ修正はあったが、`DynamicResource`の
   パフォーマンス懸念・`SystemColors`移行互換問題は継続課題。
   出典: [dotnet/wpf Discussion #10387](https://github.com/dotnet/wpf/discussions/10387)、
   [dotnet/wpf Issue #9283](https://github.com/dotnet/wpf/issues/9283)

## AvalonDockとの組み合わせ

検索した範囲では、AvalonDockとWPF `ThemeMode`/Fluentの**具体的な組み合わせ実績・相性問題の
報告は見つからず（不明）**。AvalonDockは独自のテーマパッケージ（VS2013 Dark/Light、Arc等）を
持ち、`ThemeMode` APIとは別系統で動作している模様（事実に近いが実証は不明）。両者を併用する
場合、上記4のカスタムスタイル問題が影響しうると推測されるが、実証情報はなし。

## 不明点

- AvalonDockとFluent/ThemeModeの併用実績・相性問題の有無（要PoCでの実証）
- ecad2既存の直接色指定15件（`LadderCanvas.cs`ほか計6ファイル、家老事前検分より）が
  Fluentテーマとどこまで整合するかは未検証（本調査はWeb一次情報のみ、実コード突合は対象外）

## 推奨（判断は家老・侍・殿に委ねる）

- 採用する場合も「UIクローム全体を丸ごとThemeModeに任せる」設計は避け、MessageBox等の
  非対応箇所を個別に扱う前提でPoCを組むこと。
- AvalonDock導入PoC（T-058、侍担当）と合わせて、実際にThemeMode=Dark時の見た目・
  フラッシュ有無をPoC上で目視確認することを推奨する。
