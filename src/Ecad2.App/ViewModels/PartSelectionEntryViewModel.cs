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

    /// <summary>OR接続配置用の論理エントリか(T-037、殿裁定=案A)。true時は配置操作(TryPlaceElement)
    /// のisOr引数へそのまま渡す。Entry(PartFolderEntry)自体は通常版と共有し、Core層は無変更
    /// (隠密調査`docs/ecad2-p010-or-fixed-parts-investigation-onmitsu.md`の案1どおり)。</summary>
    public bool IsOr { get; }

    /// <summary>リスト表示名(T-037)。IsOr時は「OR」を前置し、ニーモニック命名規則
    /// (design-brief 11節、「ORa接点」「ORb接点」等の日本語ラダー用語で統一)に合わせる。</summary>
    public string DisplayName => IsOr ? "OR" + Definition.Name : Definition.Name;

    public PartSelectionEntryViewModel(PartFolderEntry entry, ImageSource thumbnail, bool isOr = false)
    {
        Entry = entry;
        Thumbnail = thumbnail;
        IsOr = isOr;
    }
}
