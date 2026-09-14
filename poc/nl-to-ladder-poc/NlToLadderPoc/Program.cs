using Ecad2.Persistence;
using NlToLadderPoc;

if (args.Length < 2)
{
    Console.Error.WriteLine("使い方: NlToLadderPoc \"<自己保持の説明文>\" <出力先.gcad>");
    Console.Error.WriteLine("例:     NlToLadderPoc \"PB1でON、PB2でOFF、CR1保持\" out.gcad");
    return 1;
}

string input = args[0];
string outputPath = args[1];

var spec = SelfHoldParser.TryParse(input);
if (spec is null)
{
    Console.Error.WriteLine($"解析できませんでした（決め打ちパターン「XXでON、YYでOFF、ZZ保持」のみ対応）: {input}");
    return 1;
}

Console.WriteLine($"解析結果: Set={spec.Set} Reset={spec.Reset} Coil={spec.Coil}");

var doc = SelfHoldBuilder.Build(spec);
GcadSerializer.Save(doc, outputPath);

Console.WriteLine($"出力: {outputPath}");
return 0;
