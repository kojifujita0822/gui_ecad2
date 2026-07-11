using System.Globalization;
using Ecad2.App.Converters;
using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.App.Tests;

/// <summary>
/// T-053: 機器表「種別」列のDeviceClass→日本語表示化。PDF出力(DiagramRenderer.DeviceClassLabel)と
/// 同一文言を用いる(表記統一、殿裁定2026-07-10)。
/// </summary>
public class DeviceClassToTextConverterTests
{
    private static readonly DeviceClassToTextConverter Converter = new();

    [Theory]
    [InlineData(DeviceClass.Relay, "リレー")]
    [InlineData(DeviceClass.PushButton, "押しボタン")]
    [InlineData(DeviceClass.SelectSwitch, "切替SW")]
    [InlineData(DeviceClass.Lamp, "表示灯")]
    [InlineData(DeviceClass.Timer, "タイマ")]
    [InlineData(DeviceClass.Counter, "カウンタ")]
    [InlineData(DeviceClass.Terminal, "端子台")]
    [InlineData(DeviceClass.Other, "その他")]
    public void Convert_ReturnsJapaneseLabel(DeviceClass deviceClass, string expected)
    {
        var result = Converter.Convert(deviceClass, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    /// <summary>PDF出力側(DiagramRenderer.DeviceClassLabel)と文言が完全一致することの回帰テスト
    /// (別々の文言定義に分岐すると画面とPDFの表記が将来ズレうる、殿裁定「PDF表記統一」の直接検証)。</summary>
    [Theory]
    [InlineData(DeviceClass.Relay)]
    [InlineData(DeviceClass.PushButton)]
    [InlineData(DeviceClass.SelectSwitch)]
    [InlineData(DeviceClass.Lamp)]
    [InlineData(DeviceClass.Timer)]
    [InlineData(DeviceClass.Counter)]
    [InlineData(DeviceClass.Terminal)]
    [InlineData(DeviceClass.Other)]
    public void Convert_MatchesDiagramRendererDeviceClassLabel(DeviceClass deviceClass)
    {
        var result = Converter.Convert(deviceClass, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(DiagramRenderer.DeviceClassLabel(deviceClass), result);
    }

    [Fact]
    public void Convert_NonDeviceClassValue_ReturnsToStringFallback()
    {
        var result = Converter.Convert("不正な値", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("不正な値", result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            Converter.ConvertBack("リレー", typeof(DeviceClass), null, CultureInfo.InvariantCulture));
    }
}
