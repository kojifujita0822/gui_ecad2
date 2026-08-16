; ecad2 インストーラー（Inno Setup 7.x）
; GuiEcad の installer/GuiEcad_Setup.iss を土台に、ecad2 向けへ書き換えたもの（T-123）。
;
; ビルド前に自己完結publishを実行しておくこと:
;   dotnet publish src\Ecad2.App\Ecad2.App.csproj -c Release -r win-x64 --self-contained true
;
; 自己完結（--self-contained true）は殿裁定2026-08-14。配布先に .NET ランタイムの導入を
; 要さぬ代わり、出力は 436ファイル・142MB 規模になる（LZMA2圧縮で縮む）。
; なお WPF はトリミング（PublishTrimmed）に非対応のため、この規模を削る王道は無い
; （SDKが error NETSDK1168 で明確に拒否する。実測確認済み）。

#define AppName      "ecad2"
#define AppVersion   "0.8.0"
#define AppPublisher "FK TEQUNO"
#define AppExeName   "Ecad2.App.exe"
; ecad2 の文書ファイル。GuiEcad と同じ拡張子を用いる（殿裁定2026-08-14、競合は承知のうえ）。
#define DocExt       ".gcad"
; ProgID は GuiEcad の "GuiEcad.Document" とは別名にする。同名にすると、どちらかの
; アンインストールが他方の関連付けまで道連れに消す。
#define DocProgId    "Ecad2.Document"
; publish/ フォルダへの相対パス（installer/ の 1 つ上がリポジトリルート）
#define SourceDir    "..\src\Ecad2.App\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
; GuiEcad とは別のGUID。使い回すと Windows が「同じ製品の更新」と誤認し、
; 一方のインストールが他方を置き換えてしまう。
AppId={{BB05039F-1514-42D2-B638-8DAB15760375}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/kojifujita0822
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputBaseFilename=Ecad2_Setup_{#AppVersion}
OutputDir=.
Compression=lzma2
SolidCompression=yes
; 64 ビット専用インストール
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; アンインストーラーをコントロールパネルに登録
UninstallDisplayName={#AppName}
; アイコンは exe へ焼き込んだもの（csproj の ApplicationIcon）を参照する。
; .ico を別途配布ファイルへ含める必要はない。
UninstallDisplayIcon={app}\{#AppExeName},0
; 管理者権限（ProgramFiles への書き込み・HKLM への関連付けに必要）。
; PrivilegesRequiredOverridesAllowed は置かぬ——常に管理者モードで走らせることで、
; [Registry] の HKA が必ず HKLM へ解決される（非管理者モードでは HKCU へ倒れる）。
PrivilegesRequired=admin
; ファイル関連付けの変更をエクスプローラーに通知
ChangesAssociations=yes
; 上書きインストール時に起動中アプリを自動終了
CloseApplications=yes
; exeプロパティのファイルバージョン（Major.Minor.Patch.0 形式）
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する(&D)"; GroupDescription: "追加タスク:"; Flags: checkedonce

[InstallDelete]
; 上書きインストール前に旧バージョンのファイルをすべて削除（残留DLL等の競合防止）。
; 対象はインストール先（{app}）のみ。利用者の図面・自作パーツが入る
; MyDocuments\Ecad2\ は決して含めない。
Type: filesandordirs; Name: "{app}"

[Files]
; publish/ フォルダの全ファイルを再帰的にインストール
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; スタートメニュー
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName} のアンインストール"; Filename: "{uninstallexe}"
; デスクトップ（オプション）
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; .gcad ファイル関連付け。ダブルクリックで開けるようにするには、この登録と
; アプリ側の起動引数処理（StartupArguments.ResolveDocumentPath）の両方が要る。
; "%1" のダブルクォートは空白を含むパスのため必須。
;
; 【T-151期・HKCR から HKA へ改めた。殿ご裁可2026-08-16】
; Inno Setup 公式ヘルプ（[Registry] section）が「Using HKCR is not recommended, use HKA with
; the Subkey parameter set to Software\Classes instead」と明記しておる。理由は着地先が
; 環境によって割れるゆえ——Microsoft公式（HKEY_CLASSES_ROOT Key）に曰く、HKCR へ「値」を書き、
; そのキーが既に HKCU\Software\Classes に在れば、システムは HKLM ではなくそちらへ書き込む。
;
; これが現に起きておった。旧 .iss は HKCR へ書いており、
;   ・Ecad2.Document（新規キー）  → HKLM\Software\Classes へ着地
;   ・.gcad（GuiEcad が HKCU に既に作っておったキー） → HKCU\Software\Classes へ吸い寄せられた
; となり、拡張子と ProgID の紐付けだけが「その利用者だけのもの」になっておった。
; 新規ローカルユーザーには届かず、シェルは ERROR_NO_ASSOCIATION を返す（忍者の実測2026-08-16）。
;
; HKA は管理者インストールモードで HKLM、それ以外で HKCU に解決される。本インストーラーは
; PrivilegesRequired=admin ゆえ常に前者に定まり、着地先が環境によって割れることが無くなる。
; uninsdeletevalue／uninsdeletekey の働きは Root に依らぬ（同ヘルプ Flags 節で確認）。
Root: HKA; Subkey: "Software\Classes\{#DocExt}"; ValueType: string; ValueName: ""; ValueData: "{#DocProgId}"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#DocProgId}"; ValueType: string; ValueName: ""; ValueData: "ecad2 回路図ファイル"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#DocProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#DocProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

[Run]
; インストール完了後にアプリを起動（オプション）
Filename: "{app}\{#AppExeName}"; Description: "{#AppName} を起動する"; Flags: nowait postinstall skipifsilent
