using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 右パネル「部品選択」リスト(PartSelectionList)表示専用のラッパー(T-015)。Category/Definitionは
/// PartFolderEntryへの転送プロパティとし、既存バインディング({Binding Category}等)の互換を保つ。
/// Entry(元のPartFolderEntry)は配置処理(TryPlaceElement)へそのまま渡すために公開する。
/// </summary>
public sealed class PartSelectionEntryViewModel
{
    public PartFolderEntry Entry { get; }
    public string Category => Entry.Category;
    public PartDefinition Definition => Entry.Definition;
    public ImageSource Thumbnail { get; }

    public PartSelectionEntryViewModel(PartFolderEntry entry, ImageSource thumbnail)
    {
        Entry = entry;
        Thumbnail = thumbnail;
    }
}
