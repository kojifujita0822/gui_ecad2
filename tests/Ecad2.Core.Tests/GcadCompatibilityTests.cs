using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// 殿裁定(T-007)の要件(1)「既存.GCADファイルとの互換性は維持する」の検証。
/// gui_ecad(GuiEcad.App/Assets/Parts/thermal-relay-a.gcadpart)から採取した実サンプルJSONを
/// そのまま埋め込み、Ecad2.Persistenceが同一スキーマで読み込めることを確認する。
/// </summary>
public class GcadCompatibilityTests
{
    // gui_ecad実サンプル: GuiEcad.App/Assets/Parts/thermal-relay-a.gcadpart をそのまま採取。
    private const string ThermalRelayAJson = """
        {
          "id": "fade680f05b74c0098051fa3558e690b",
          "name": "サーマルリレ-a",
          "widthCells": 1,
          "heightCells": 1,
          "role": "contactNO",
          "ports": [
            { "name": "L", "rowOffset": 0, "boundaryOffset": 0 },
            { "name": "R", "rowOffset": 0, "boundaryOffset": 1 }
          ],
          "primitives": [
            { "type": "circle", "cx": 0.875, "cy": 0, "r": 0.125 },
            { "type": "circle", "cx": 0.125, "cy": 0, "r": 0.125 },
            { "type": "line", "x1": 0.125, "y1": -0.1875, "x2": 0.875, "y2": -0.1875 },
            { "type": "line", "x1": 0.625, "y1": -0.3125, "x2": 0.375, "y2": -0.0625 },
            { "type": "line", "x1": 0.375, "y1": -0.3125, "x2": 0.625, "y2": -0.0625 }
          ]
        }
        """;

    [Fact]
    public void DeserializeOne_gui_ecad実サンプルpart_を読み込める()
    {
        var part = PartLibrarySerializer.DeserializeOne(ThermalRelayAJson);

        Assert.Equal("fade680f05b74c0098051fa3558e690b", part.Id);
        Assert.Equal("サーマルリレ-a", part.Name);
        Assert.Equal(PartRole.ContactNO, part.Role);
        Assert.Equal(2, part.Ports.Count);
        Assert.Equal(5, part.Primitives.Count);
        Assert.IsType<PartCircle>(part.Primitives[0]);
        Assert.IsType<PartLine>(part.Primitives[2]);
    }

    [Fact]
    public void GcadSerializer_SerializeとDeserializeが往復一致する()
    {
        var doc = new LadderDocument
        {
            Info = new DocumentInfo { Title = "T-007互換性検証", CompanyName = "ecad2" },
            Sheets =
            {
                new Sheet
                {
                    Name = "シート1",
                    Elements =
                    {
                        new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 1), DeviceName = "CR1" },
                    },
                },
            },
        };

        string json = GcadSerializer.Serialize(doc);
        var restored = GcadSerializer.Deserialize(json);

        Assert.Equal(GcadSerializer.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal("T-007互換性検証", restored.Info.Title);
        Assert.Single(restored.Sheets);
        Assert.Single(restored.Sheets[0].Elements);
        Assert.Equal("CR1", restored.Sheets[0].Elements[0].DeviceName);
    }

    [Fact]
    public void Deserialize_未知のSchemaVersionはNotSupportedExceptionを投げる()
    {
        const string json = """{ "schemaVersion": 999, "sheets": [] }""";
        Assert.Throws<NotSupportedException>(() => GcadSerializer.Deserialize(json));
    }
}
