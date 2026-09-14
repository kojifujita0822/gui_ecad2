using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Simulation;

namespace NlToLadderPoc;

/// <summary>
/// 中間表現（<see cref="SelfHoldSpec"/>）から自己保持回路の<see cref="LadderDocument"/>を機械的に組み立てる。
/// 「AIには論理構造の判定だけを担わせ、座標・結線といったレイアウト計算は決定的なコードで行う」という
/// 役割分担の、コード側を担う部分（殿ご提案2026-09-14の案1）。
/// <para>
/// レイアウト＝行0に Set(NO押釦)・Reset(NC押釦)・Coil を横並びで配置し、行1に Coil の a接点を
/// Set と同じ列境界へ配置、左右2本の<see cref="VerticalConnector"/>で行0のSetと並列（OR）にする
/// ——教科書的な自己保持回路の最小形。
/// </para>
/// </summary>
public static class SelfHoldBuilder
{
    private const int SetColumn = 1;
    private const int ResetColumn = 2;
    private const int CoilColumn = 10;

    public static LadderDocument Build(SelfHoldSpec spec)
    {
        var doc = new LadderDocument
        {
            Info = new DocumentInfo { Title = "自己保持回路（NL-to-Ladder PoC生成）" },
        };

        doc.Devices.ByName[spec.Set] = new Device { Name = spec.Set, Class = DeviceClass.PushButton, Comment = "自己保持ON入力" };
        doc.Devices.ByName[spec.Reset] = new Device { Name = spec.Reset, Class = DeviceClass.PushButton, Comment = "自己保持OFF入力" };
        doc.Devices.ByName[spec.Coil] = new Device { Name = spec.Coil, Class = DeviceClass.Relay, Comment = "自己保持コイル" };

        var sheet = new Sheet
        {
            PageNumber = 1,
            Name = "制御回路",
            Grid = new GridSpec { Rows = 6, Columns = 15 },
        };

        sheet.Elements.Add(new ElementInstance
        {
            Pos = new GridPos(0, SetColumn),
            DeviceName = spec.Set,
            PartId = BuiltinPartIds.PushButtonNO,
        });
        sheet.Elements.Add(new ElementInstance
        {
            Pos = new GridPos(0, ResetColumn),
            DeviceName = spec.Reset,
            PartId = BuiltinPartIds.PushButtonNC,
        });
        sheet.Elements.Add(new ElementInstance
        {
            Pos = new GridPos(0, CoilColumn),
            DeviceName = spec.Coil,
            PartId = BuiltinPartIds.Coil,
        });
        // 自己保持接点（Coilのa接点）。左端（列SetColumn）は行1自身の母線直結に任せ（NetlistBuilder.
        // LeftRailReachedは「行の最初の要素の左端より左に縦コネクタが無ければ左母線へ繋がる」設計のため、
        // ここへ縦コネクタを置くと逆に行1が母線から遮断される）、右端（列SetColumn+1）だけを
        // 縦コネクタでSet/Resetの接続点へ繋ぐことで、行0のSetと電気的に並列（OR）になる。
        sheet.Elements.Add(new ElementInstance
        {
            Pos = new GridPos(1, SetColumn),
            DeviceName = spec.Coil,
            PartId = BuiltinPartIds.ContactNO,
        });

        sheet.Connectors.Add(new VerticalConnector { Column = SetColumn + 1, TopRow = 0, BottomRow = 1 });

        doc.Sheets.Add(sheet);
        CircuitNumberer.Number(doc);
        return doc;
    }
}
