using System.Globalization;
using System.Text.RegularExpressions;
using BetterCM.Config;
namespace BetterCM.Services;

internal static partial class RuntimeInputs
{
    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
    [GeneratedRegex(@"\{input:([^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
    [GeneratedRegex(@"\{(path|dir|name)\}|\{input:([^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex ArgumentPlaceholder();
    internal static void Validate(CustomActionItem action)
    {
        if (action.Parameters == null || action.Parameters.Count > 20) throw new InvalidDataException("最多定义 20 个运行时参数");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in action.Parameters)
        {
            if (parameter == null || parameter.Id == null || !Identifier().IsMatch(parameter.Id) || !ids.Add(parameter.Id) || string.IsNullOrWhiteSpace(parameter.Title) || parameter.Title.Length > 100 || parameter.DefaultValue == null || parameter.DefaultValue.Length > 1024 || parameter.Choices == null || parameter.Choices.Any(x => string.IsNullOrEmpty(x) || x.Length > 1024) || parameter.Choices.Count > 100) throw new InvalidDataException("参数需要唯一的小写英文标识、名称和有效默认值");
            if (parameter.Kind is not ("text" or "number" or "choice") || parameter.Minimum > parameter.Maximum || parameter.Kind == "choice" && (parameter.Choices.Count == 0 || parameter.Choices.Distinct(StringComparer.Ordinal).Count() != parameter.Choices.Count)) throw new InvalidDataException("参数类型、数值范围或选项列表无效");
            if (parameter.DefaultValue.Length > 0) ValidateValue(parameter, parameter.DefaultValue);
        }
        if (action.PreserveInputLiterals && action.Parameters.Count == 0) return;
        int markers = 0, position = 0;
        while ((position = action.Arguments.IndexOf("{input:", position, StringComparison.Ordinal)) >= 0) { markers++; position += 7; }
        if (markers != Placeholder().Matches(action.Arguments).Count) throw new InvalidDataException("运行参数占位符需要完整的 {input:标识} 格式");
        foreach (Match match in Placeholder().Matches(action.Arguments))
            if (!ids.Contains(match.Groups[1].Value)) throw new InvalidDataException("未定义的运行时参数：" + match.Value);
    }
    private static void ValidateValue(RuntimeParameter parameter, string value)
    {
        if (value.Length > 1024 || value.IndexOf('\0') >= 0 || parameter.Required && string.IsNullOrWhiteSpace(value)) throw new InvalidDataException(parameter.Title + "需要有效内容（最多 1024 字符）");
        if (value.Length == 0 && !parameter.Required) return;
        if (parameter.Kind == "number" && (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number) || number < parameter.Minimum || number > parameter.Maximum)) throw new InvalidDataException($"{parameter.Title}需要 {parameter.Minimum}–{parameter.Maximum} 范围内的整数");
        if (parameter.Kind == "choice" && !parameter.Choices.Contains(value, StringComparer.Ordinal)) throw new InvalidDataException(parameter.Title + "需要从提供的选项中选择");
    }
    internal static IReadOnlyDictionary<string, string> Values(CustomActionItem action, IReadOnlyDictionary<string, string>? supplied, bool preview)
    {
        Validate(action);
        if (supplied != null && supplied.Keys.Any(id => !action.Parameters.Any(x => x.Id == id))) throw new InvalidDataException("包含未定义的运行时参数");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in action.Parameters)
        {
            string value = supplied?.GetValueOrDefault(parameter.Id) ?? parameter.DefaultValue;
            if (preview && parameter.Required && string.IsNullOrEmpty(value)) value = parameter.Kind switch { "number" => parameter.Minimum.ToString(CultureInfo.InvariantCulture), "choice" => parameter.Choices[0], _ => "〈" + parameter.Title + "〉" };
            ValidateValue(parameter, value); result.Add(parameter.Id, value);
        }
        return result;
    }
    internal static string Expand(string token, string path, string directory, IReadOnlyDictionary<string, string> values)
        => values.Count == 0 ? Models.PathFormatter.Expand(token, path, directory) : ArgumentPlaceholder().Replace(token, match => match.Groups[2].Success ? values[match.Groups[2].Value] : match.Groups[1].Value switch { "path" => path, "dir" => directory, _ => Models.PathFormatter.Name(path) });
}
