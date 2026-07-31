using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 右パネル「部品選択」リスト(PartSelectionList)表示専用のラッパー(T-015)。Category/Definitionは
/// PartFolderEntryへの転送プロパティとし、既存バインディング({Binding Category}等)の互換を保つ。
/// Entry(元のPartFolderEntry)は配置処理(TryPlaceElement)へそのまま渡すために公開する。
/// </summary>
public sealed class PartSelectionEntryViewModel : ViewModelBase
{
    public PartFolderEntry Entry { get; }
    public string Category => Entry.Category;
    public PartDefinition Definition => Entry.Definition;

    private ImageSource _thumbnail;
    /// <summary>T-083新規発見5(家老采配2026-07-17): ダークモード切替時にサムネイルを再生成できる
    /// よう可変プロパティ化(従来は読み取り専用)。PartPaletteViewModel.RefreshThumbnailsが
    /// テーマ切替時に差し替える。</summary>
    public ImageSource Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    private bool _isPlaceable = true;
    /// <summary>現在のシートへ置ける部品か(T-136(A)増分2)。false の間はリスト項目を無効化し、
    /// 置けぬ部品をそもそも選ばせぬ(<b>予防</b>)。実際の拒否は <c>ValidatePlacement</c> が受け持つ
    /// (<b>防御</b>)——両建ては殿裁定2026-07-31。
    /// <para>
    /// エントリ自身は現在のシート種別を知らぬゆえ、外から設定する形とした(<see cref="Thumbnail"/> と同型)。
    /// 設定するのは <c>PartPaletteViewModel.RefreshPlaceability</c>。
    /// </para></summary>
    public bool IsPlaceable
    {
        get => _isPlaceable;
        set => SetProperty(ref _isPlaceable, value);
    }

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
        _thumbnail = thumbnail;
        IsOr = isOr;
    }
}
