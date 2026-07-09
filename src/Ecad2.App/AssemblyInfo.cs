using System.Runtime.CompilerServices;
using System.Windows;

// T-050: TraceLog(internal)の正規化ロジックをリフレクション越しにテストするため
// (`typeof(TraceLog)`自体の型解決にinternal可視性が要る、既存のMainWindowViewModel.MapToDeviceClass
// のリフレクションテストはクラス自体がpublicのため不要だった差異)。
[assembly: InternalsVisibleTo("Ecad2.App.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
