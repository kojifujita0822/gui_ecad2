# T-108追加修正 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `f0bb3f7`（PdfPreviewDialog/StatusBarのダークモード対応）
手法: `git show f0bb3f7 -- <path>` で範囲を明示した手動レビュー＋一次ソース突合。effort=low（1周目既定）。

## 結論

**指摘なし。DoD（隠密全体点検で発見した優先度高2件の修正）を満たすと判断する。** ビルド確認はEcad2.App.exeがDLLロック中のため実行不可（忍者の実機確認と思われる、`onmitsu.md`既定どおり無理に強行せず静的差分確認で代替）。静的レビューとしては十分な材料が揃っている。忍者実機確認へ進めてよい。

## 確認観点と根拠

### (a) DoD整合確認

`docs/ecad2-t108-darkmode-audit-onmitsu.md`（隠密全体点検報告）で指摘した優先度高2件——(1)`PdfPreviewDialog.xaml`のWindow要素自体のBackground/Foreground欠落、(2)`StatusBar`/`StatusBarItem`用の暗黙的スタイル不在によるAero2既定テーマスタイル本体の固定色適用——の両方に対応する修正であることを確認。DoD整合確認OK。

### (b) `PdfPreviewDialog.xaml`：Window要素への追加、`PageLabel`/`ZoomLabel`継承解決の妥当性

`Background="{DynamicResource DialogBackgroundBrush}"`・`Foreground="{DynamicResource DialogForegroundBrush}"`をWindow要素へ追加。これは他の全6ダイアログ（`AddSheetDialog`・`DocumentInfoDialog`・`RenameDialog`・`SheetSettingsDialog`・`AboutDialog`・`UsageWindow`）と完全に同型のパターンであり、既に実績のある確立された対処法。

`PageLabel`/`ZoomLabel`（`TextBlock`）への個別Foreground追加は行わず継承に委ねる判断について：`TextBlock`は`Control`派生ではなく`FrameworkElement`直下のクラスであり、`DefaultStyleKey`によるテーマスタイル機構自体を持たない。`Foreground`は純粋なDependencyProperty継承（`Inherits=true`）に従うため、Window自体にForegroundが確定していれば、その子孫の`TextBlock`は自動的に継承する。今回の全体点検（T-108本体）で確認した他の対応済み`TextBlock`（`DocumentInfoDialog`等の`TextBlock`ラベル群）も同じ継承パターンで機能しており、妥当な判断。

### (c) `StatusBar`/`StatusBarItem`暗黙的スタイル新設の一次ソース整合・Template新設不要判断の妥当性

一次ソース`Aero2.NormalColor.xaml`5391-5404行（隠密が今回のT-108本体調査時にscratchpadへ保存済みの分を再確認）：`StatusBar`の既定`ControlTemplate`は`Border Background="{TemplateBinding Background}" ... <ItemsPresenter .../></Border>`という、`TemplateBinding`でBackgroundを反映するだけのシンプルな構造。Foregroundへの直接言及はTemplate内に無く、`TextElement.Foreground`の継承チェーン（`StatusBar`→`ItemsPresenter`→`StatusBarItem`→子孫）に委ねられる設計。

侍の実装（Style本体のみ新設、`Template`のSetterは追加しない）：
```xml
<Style TargetType="{x:Type StatusBar}">
    <Setter Property="Background" Value="{DynamicResource ToolBarBackgroundBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource ToolBarForegroundBrush}"/>
</Style>
<Style TargetType="{x:Type StatusBarItem}">
    <Setter Property="Foreground" Value="{DynamicResource ToolBarForegroundBrush}"/>
</Style>
```
これはPR-24パターンの正しい対処法——ecad2側に暗黙的スタイルが新設されたことで、`Background`/`Foreground`プロパティについてはこのStyleのSetterがAero2既定テーマスタイルのSetterより優先して適用される。`Template`プロパティについては、ecad2側StyleにSetterが無いため、依然としてAero2既定のシンプルなTemplateへプロパティ単位でフォールバックする——これは意図した動作であり、`Template`自体の新設が不要という判断は一次ソースの構造（`TemplateBinding`のみで完結する単純さ）から見て妥当。

`ToolBarBackgroundBrush`/`ToolBarForegroundBrush`は新規キーではなく、`MainWindow.xaml`のツールバー領域（Grid.Row="1"、974行等）で既に使われている確立されたキーであることを両テーマファイルで確認済み。ステータスバーとツールバーは共に画面上下の帯状UIという類似性があり、既存のツールバー系配色を再利用する設計判断は自然。

### (d) `StatusBarItem`の`Background=Transparent`維持の妥当性

侍は`StatusBarItem`のStyleに`Foreground`のみ追加し、`Background`のSetterは追加していない。一次ソース（5430-5431行）で`StatusBarItem`の既定Style本体は`Background="Transparent"`であることを確認済み——これがそのまま維持される。`StatusBarItem`自体が透明であれば、親`StatusBar`のBackground（今回確定させた`ToolBarBackgroundBrush`）がそのまま透けて見えるため、視覚的に統一された単色の帯として表示される。もし`StatusBarItem`にも個別Backgroundを指定すると、不要な重ね塗りや意図しない境界が生じる可能性がある。Transparent維持は意図的かつ正しい判断。

### (e) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド確認（未完了・理由明記）

`dotnet build src/Ecad2.App/Ecad2.App.csproj --no-incremental`を実行したところ、`Ecad2.App.exe`（PID 25376）がDLLをロック中でコピー失敗（`MSB3027`）。忍者へ使用状況を確認する`send_message`を送信済み（本レビュー完了時点で未応答）。`onmitsu.md`「ビルド確認前にEcad2.App起動有無を確認する」既定に従い、無理にビルドを強行せず、コード上の静的差分確認（(b)(c)(d)節）で判定した。XAML構文（開始/終了タグの対応、属性記述）にも異常は見当たらない。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし。

## 出典

- `src/Ecad2.App/App.xaml`（StatusBar/StatusBarItem暗黙的スタイル新設）
- `src/Ecad2.App/Views/PdfPreviewDialog.xaml`（Window要素Background/Foreground追加）
- `src/Ecad2.App/MainWindow.xaml`（ToolBarBackgroundBrush/ForegroundBrush既存使用箇所）
- `src/Ecad2.App/Themes/Theme.Dark.xaml`・`Theme.Light.xaml`（DynamicResourceキー定義）
- WPF本体一次ソース`dotnet/wpf`（scratchpad保存済み`Aero2.NormalColor.xaml`、StatusBar/StatusBarItem 5377-5458行）
- `docs/ecad2-t108-darkmode-audit-onmitsu.md`（隠密全体点検報告、DoD根拠）
