# WPF 非技術面調査（T-005・隠密）

> 2026-07-03 隠密調査。殿の「路線A（WPF本命）仮確定＋非技術面の多角検証」指示を受け、
> `docs/ecad2-stack-decision-brief.md` のWPF本命判断を補強する目的で実施。
> 対象: (1)ライセンス (2)Microsoftの保守体制・将来性 (3)業界での採用実績。

## 1. ライセンス（.NET/WPFの利用条件・商用利用可否）

**結論**: リスクは低い。商用デスクトップアプリの基盤として問題なく採用可能。ただし実務上の注意点が2点ある。

**根拠**:
- .NET本体（ランタイム/SDK/ライブラリ）はMITライセンス。現行LTSは.NET 10（2025年11月リリース、2028年11月までLTS）。商用利用・再配布は無償、ロイヤリティなし。
  ([dotnet.microsoft.com/platform/free](https://dotnet.microsoft.com/en-us/platform/free), [dotnet/core license-information.md](https://github.com/dotnet/core/blob/main/license-information.md))
- WPF自体（dotnet/wpfリポジトリ）もMITライセンス、著作権者は.NET Foundation and Contributors。表記義務は著作権表示・ライセンス文の同梱のみ。
  ([dotnet/wpf LICENSE.TXT](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT))
- **注意点1**: Windows版の一部ネイティブバイナリ（coreclr.dll、PresentationNative_cor3.dll、wpfgfx_cor3.dll等）はMITではなく「.NET Library License」、D3DCompiler_47_cor3.dll（WPF依存）は「Windows SDK License」が適用される。商用利用・アプリ組込配布は無償だが「スタンドアロン配布不可」「Microsoft商標の不使用」「ソースコード非公開」等の制約あり。
  ([dotnet/core license-information-windows.md](https://github.com/dotnet/core/blob/main/license-information-windows.md), [.NET Library License](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm))
- **注意点2**: Visual Studio Community は組織規模250PC未満かつ年商1000万ドル未満なら開発者5名まで商用利用可。それを超える場合はProfessional/Enterprise版が必要。
  ([visualstudio.microsoft.com/license-terms](https://visualstudio.microsoft.com/license-terms/))
- 過去事例: 2015年の.NET OSS化時のパテント条項（Community Promise）が議論になったが実害なく沈静化。WPF自体・アプリ配布に無関係な別件（clrdbgのサードパーティIDE制約）はあるが本件と無関係。

**推奨案**: このまま採用して問題なし。選定資料には「アプリ組込時、WPFネイティブDLLの一部は.NET Library License/Windows SDK Licenseが適用されスタンドアロン配布不可・商標不使用等の制約がある」旨、および「開発チーム規模次第でVisual Studio Community不可、Professional/Enterpriseライセンス購入要」の2点を注記すべき。

---

## 2. Microsoftの保守体制・将来性

**結論**: リスクは低い。長期保守前提の業務アプリ基盤として採用して問題ない。

**根拠**:
- .NET 10（LTS、2028年11月までサポート）でもWPFは正式に同梱・サポートされ、Fluentテーマ対応や性能改善など継続的に更新されている。
  ([What's new in WPF for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100), [Announcing .NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/))
- [dotnet/wpf roadmap.md](https://github.com/dotnet/wpf/blob/main/roadmap.md) は「メンテナンスモードではない」と明言し、近代化への長期ビジョン（Nullable対応、DirectX刷新、Fluentデザイン、性能・アクセシビリティ改善）を掲げる。
- GitHub実態: 直近90日で38コミット、最終更新は当日、star 7,674、open issue 1,639件と継続的に活動中。
- Microsoft社員の公式Q&A回答:「according to the documentation ... there are no plans to remove wpf」
  ([出典](https://learn.microsoft.com/en-us/answers/questions/1134624/windows-presentation-foundation-(wpf)-end-of-life))

**推奨案**: 新機能追加は緩やか（「維持＋漸進的近代化」）で大規模な新機能ラッシュは期待できない。WinUI3は新規ネイティブUI開発の推奨方向だが、既存WPF投資の継続利用を否定する動きはなく両立方針。長期採用時はdotnet/wpfのIssue対応速度とLTSリリースサイクルを定期的にウォッチすることを推奨。

**不明点**: WPF単独の明示的なEOL方針文書は確認できず、.NETランタイム自体のLTS/STSサイクルに準ずる形と推測されるに留まる。

---

## 3. 業界での採用実績

**結論**: 採用実績は十分。CAD/SCADA・HMI分野でも実績が確認でき、実績面のリスクは許容範囲。

**根拠**:
- デスクトップ業務アプリ全般: Visual Studio・Blend自体がWPF製。ArcGIS Pro（Esri）もWPF/MVVMアーキテクチャを採用。
- CAD/制御盤・PLC/SCADA・HMI分野: Eplan Electric P8（電気CAD大手）がWPF連携APIを保有。Movicon.NExT、ETAP HMI、Open Automation Softwareの「WPF HMI .NET」など、制御盤・PLC・HMI向け専用WPF製品が実在。
  ([Open Automation Software](https://openautomationsoftware.com/products/hmi-scada-for-net/iot-wpf-hmi-net/), [ETAP HMI](https://etap.com/product/human-machine-interface))
- EOLリスク: 2018年にMITライセンスでOSS化、.NET Foundation管理下に。2022年頃「WPFは死んだのか」という業界論争があった（確度低・停滞感の指摘に留まる）。
- 大規模障害事例: WPF採用に起因する重大障害事例は調査範囲では確認できず（不明）。
- 他技術への移行事例: 具体的な製品名でのWinUI3/Avalonia移行事例は確認できず（不明）。

**推奨案**: 実績面のリスクは許容範囲。「枯れた技術」であるがゆえの将来的な技術者確保・クロスプラットフォーム要求への対応力には留意し、選定資料には「Windows専用・安定性優先の前提での採用」と明記することを推奨。

**不明点**: 大規模障害事例・移行事例の一次情報は確認できず。深掘りするなら英語圏フォーラム（Reddit r/dotnet, Stack Overflow）や個別製品リリースノートの継続調査が有効。

---

## 総合結論（3項目通し）

いずれの観点（ライセンス・保守体制・採用実績）でもWPF採用の非技術面リスクは**低い〜許容範囲**であり、`docs/ecad2-stack-decision-brief.md` のWPF本命判断を補強する結果となった。ただし以下3点は選定資料への注記を推奨:
1. WPFネイティブDLLの一部に.NET Library License/Windows SDK Licenseが適用され、スタンドアロン配布不可・商標不使用等の制約がある
2. 開発チーム規模次第でVisual Studio Community不可（Professional/Enterprise要）
3. Windows専用・安定性優先の前提での採用である旨（クロスプラットフォーム要求が将来生じた場合はAvalonia等への再検討余地を残す）
