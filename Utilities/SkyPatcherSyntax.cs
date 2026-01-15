using Mutagen.Bethesda.Plugins;

namespace Boutique.Utilities;

public static class SkyPatcherSyntax
{
    public static string? ExtractFilterValue(string line, string filterName)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var filterPrefix = filterName + "=";
        var index = line.IndexOf(filterPrefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var start = index + filterPrefix.Length;
        var end = line.IndexOf(':', start);

        var value = end >= 0 ? line.Substring(start, end - start) : line[start..];
        return value.Trim();
    }

    public static List<string> ExtractFilterValues(string line, string filterName)
    {
        var value = ExtractFilterValue(line, filterName);
        if (string.IsNullOrEmpty(value))
            return [];

        return value.Split(',')
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    public static bool? ParseGenderFilter(string line)
    {
        var value = ExtractFilterValue(line, "filterByGender");
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Equals("female", StringComparison.OrdinalIgnoreCase) ? true :
               value.Equals("male", StringComparison.OrdinalIgnoreCase) ? false : null;
    }

    public static List<FormKey> ParseFormKeys(string line, string filterName)
    {
        var values = ExtractFilterValues(line, filterName);
        var results = new List<FormKey>();

        foreach (var value in values)
        {
            if (FormKeyHelper.TryParse(value, out var formKey))
                results.Add(formKey);
        }

        return results;
    }

    public static bool HasFilter(string line, string filterName) =>
        line.Contains(filterName + "=", StringComparison.OrdinalIgnoreCase);
}
