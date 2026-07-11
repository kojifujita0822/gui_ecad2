# T-062 .NET 10移行 差分レビュー（隠密）

対象: ブランチ`t062-net10-migration`、コミット`143e1c0`。家老指定4観点の手動確認を実施。

**結論：全4観点クリーン。忍者実機回帰へ回して差し支えない。**

---

## (a) TFM値の正確性

| プロジェクト | 変更前 | 変更後 | 判定 |
|---|---|---|---|
| `Ecad2.App.csproj` | `net8.0-windows` | `net10.0-windows` | OK |
| `Ecad2.Rendering.Wpf.csproj` | `net8.0-windows` | `net10.0-windows` | OK |
| `Ecad2.Core.csproj` | `net8.0` | `net10.0` | OK |
| `Ecad2.Pdf.csproj` | `net8.0` | `net10.0` | OK |
| `Ecad2.Core.Tests.csproj` | `net8.0` | `net10.0` | OK |
| `Ecad2.App.Tests.csproj` | `net8.0-windows` | `net10.0-windows` | OK |

家老指定の対応関係（App/Rendering.Wpf=windows系、Core/Pdf/Core.Tests=非windows系、App.Testsのみ
windows系）と完全一致。`Ecad2.Core.Tests`が`net10.0`（非windows）である一方`Ecad2.App.Tests`が
`net10.0-windows`である非対称性も、コミットメッセージの説明（下位TFMが上位TFM本体を
ProjectReferenceできない制約=NU1201のため、テストプロジェクトは各々の参照先本体に合わせる）
どおりで妥当。

## (b) 差分にTFM変更以外の混入がないか

`git show 143e1c0`で検分。6ファイルとも`<TargetFramework>`要素1行のみの変更（6 insertions, 6
deletions、他の行に一切触れていない）。PackageReference・ProjectReference・その他のPropertyGroup
設定はいずれも無変更。混入なし。

（作業ツリーに`docs/todo.md`の未ステージ変更が別途あるが、コミット`143e1c0`には含まれない別件の
ため、本レビューの対象外と判断する）

## (c) PDFsharp 6.2.4据え置き判断の妥当性

`dotnet list package --outdated`（`src/Ecad2.Pdf`から実行）で「更新なし」を確認——コミット
メッセージの主張どおり最新版。NuGet.org公式情報でPDFsharp 6.2.4が`net10.0`を明示的にサポート
（"`.NET 10.0 is compatible`"、netstandard2.0への依存のみでなく個別ターゲット）していることも
確認した。据え置き判断は妥当。

## (d) ビルド・テスト実測

```
dotnet build src/Ecad2.sln --no-incremental → 0エラー・0警告
  全プロジェクトが期待どおりnet10.0/net10.0-windowsへ出力されることを確認
dotnet test src/Ecad2.sln --no-build
  Ecad2.Core.Tests: 64件 合格（.NETCoreApp,Version=v10.0で実行）
  Ecad2.App.Tests: 419件 合格（.NETCoreApp,Version=v10.0で実行、失敗0）
```

mainブランチ（net8.0時点）と同件数・同結果で回帰なし。

---

## 結論

家老指定4観点いずれもクリーン。差分は6csprojのTFM更新1行ずつのみで、機能的な変更は皆無。
PDFsharp含め依存パッケージの互換性も一次情報で確認済み。忍者実機回帰へ回して差し支えない。

なお、懸案のStryker実行障害（`docs/ecad2-t051-stryker-analysis-blocker-onmitsu.md`）は、
現在SDK=`10.0.301`・`global.json`無しの状態で、本移行によりTargetFramework=net10.0が
SDKバージョンと一致する。家老仮説（SDK/TFM不一致がBuildalyzerの解析失敗を招いていた）の
検証は、マージ完了後にmain上で家老の采配により改めて実施される。

---

## 出典
- コミット`143e1c0`（`git show 143e1c0`の全差分）
- https://www.nuget.org/packages/PDFsharp/6.2.4 （TFM対応確認）
- `docs/ecad2-t051-stryker-analysis-blocker-onmitsu.md`（Stryker障害・家老仮説の出典）
