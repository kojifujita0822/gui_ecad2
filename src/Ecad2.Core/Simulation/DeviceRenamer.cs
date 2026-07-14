using Ecad2.Model;

namespace Ecad2.Simulation;

/// <summary>
/// ドキュメント全体の DeviceName を一括置換するユーティリティ。
/// 検索は大文字小文字を区別しない（実機器名は慣習的に大文字だが入力ゆれに対応）。
/// DeviceTable も同時に更新する。
/// </summary>
public static class DeviceRenamer
{
    /// <summary>
    /// <paramref name="doc"/> 内の全シート・全要素の DeviceName が
    /// <paramref name="from"/> に一致するものを <paramref name="to"/> に置換する。
    /// DeviceTable のキーも移行する。
    /// </summary>
    /// <returns>置換した要素数（DeviceTable の移行は含まない）。</returns>
    public static int Rename(LadderDocument doc, string from, string to)
    {
        if (string.IsNullOrEmpty(from) || from == to) return 0;

        int count = 0;
        foreach (var sheet in doc.Sheets)
        {
            foreach (var elem in sheet.Elements)
            {
                if (string.Equals(elem.DeviceName, from, StringComparison.OrdinalIgnoreCase))
                {
                    elem.DeviceName = to;
                    count++;
                }
            }
        }

        // DeviceTable のキー移行（from が登録されていれば to へ移す。大文字小文字を区別しない）。
        // T-070隠密レビュー指摘D-2: to が既に別の Device として登録済みの場合、無条件上書きすると
        // to 側の既存 BOM 情報(Model/Maker/Quantity)が失われる。既に to が存在するなら from 側は
        // 削除のみに留め、to の既存 Device は保持する(MainWindowViewModel.MigrateOrRegisterDeviceの
        // 単発置換側と同種の保護、片方だけ保護され非対称になっていたものを揃える)。ただし
        // "m1"->"M1"のような大文字小文字違いの自己リネームでは、to の既存キー探索が from 自身
        // (key)にヒットしうる。それは「別枠で既に登録済み」ではなく移行対象そのものなので、
        // key と同一なら「既に登録済み」扱いにせず通常どおりキー移行する。
        var key = doc.Devices.ByName.Keys
            .FirstOrDefault(k => string.Equals(k, from, StringComparison.OrdinalIgnoreCase));
        if (key is not null && doc.Devices.ByName.TryGetValue(key, out var device))
        {
            var existingToKey = doc.Devices.ByName.Keys
                .FirstOrDefault(k => string.Equals(k, to, StringComparison.OrdinalIgnoreCase));
            doc.Devices.ByName.Remove(key);
            if (existingToKey is null || existingToKey == key)
            {
                device.Name = to;
                doc.Devices.ByName[to] = device;
            }
        }

        return count;
    }

    /// <summary>
    /// <paramref name="doc"/> 内で <paramref name="name"/> に一致する要素をすべて返す。
    /// 検索バーのハイライト用。
    /// </summary>
    public static IReadOnlyList<(Sheet Sheet, ElementInstance Element)> Find(
        LadderDocument doc, string name)
    {
        if (string.IsNullOrEmpty(name)) return Array.Empty<(Sheet, ElementInstance)>();

        var results = new List<(Sheet, ElementInstance)>();
        foreach (var sheet in doc.Sheets.OrderBy(s => s.PageNumber))
            foreach (var elem in sheet.Elements)
                if (string.Equals(elem.DeviceName, name, StringComparison.OrdinalIgnoreCase))
                    results.Add((sheet, elem));
        return results;
    }
}
