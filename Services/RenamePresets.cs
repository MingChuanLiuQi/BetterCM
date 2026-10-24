using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

internal static class RenamePresets
{
    internal static IReadOnlyList<RenamePreset> Builtins => [new() { Id = "builtin-clean", Title = "清理首尾空格", Options = new() { TrimSpaces = true } }, new() { Id = "builtin-number", Title = "自然排序后编号", Options = new() { Numbered = true, Suffix = "-", SortOrder = "name" } }];
    internal static void Validate(List<RenamePreset>? presets)
    {
        if (presets == null || presets.Count > 100) throw new InvalidDataException("最多保存 100 个重命名预设");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var preset in presets)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Id) || !ids.Add(preset.Id) || string.IsNullOrWhiteSpace(preset.Title) || preset.Title.Length > 100 || preset.Options == null) throw new InvalidDataException("预设需要唯一 ID、名称和规则");
            BatchRename.ValidateOptions(preset.Options);
        }
    }
    internal static IReadOnlyList<SelectedItem> Sort(IReadOnlyList<SelectedItem> items, string order, CancellationToken cancellation = default)
    {
        var comparer = Comparer<string>.Create(CompareNames);
        DateTime Modified(SelectedItem item) { cancellation.ThrowIfCancellationRequested(); return File.GetLastWriteTimeUtc(item.Path); }
        return order switch
        {
            "name" => items.OrderBy(x => x.Name, comparer).ToArray(),
            "name-desc" => items.OrderByDescending(x => x.Name, comparer).ToArray(),
            "modified" => items.OrderBy(Modified).ToArray(),
            "modified-desc" => items.OrderByDescending(Modified).ToArray(),
            _ => items.ToArray()
        };
    }
    internal static int CompareNames(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int startA = i, startB = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                string first = a[startA..i].TrimStart('0'), second = b[startB..j].TrimStart('0');
                int numeric = first.Length.CompareTo(second.Length);
                if (numeric == 0) numeric = string.CompareOrdinal(first, second);
                if (numeric != 0) return numeric;
                continue;
            }
            int text = char.ToUpperInvariant(a[i++]).CompareTo(char.ToUpperInvariant(b[j++]));
            if (text != 0) return text;
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
