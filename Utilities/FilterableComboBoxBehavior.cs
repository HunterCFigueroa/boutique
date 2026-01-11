using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Boutique.Utilities;

public static class FilterableComboBoxBehavior
{
    public static readonly DependencyProperty FilterPathProperty =
        DependencyProperty.RegisterAttached(
            "FilterPath",
            typeof(string),
            typeof(FilterableComboBoxBehavior),
            new PropertyMetadata(null, OnFilterPathChanged));

    public static string GetFilterPath(DependencyObject obj) =>
        (string)obj.GetValue(FilterPathProperty);

    public static void SetFilterPath(DependencyObject obj, string value) =>
        obj.SetValue(FilterPathProperty, value);

    private static void OnFilterPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox comboBox)
            return;

        if (e.NewValue is string filterPath && !string.IsNullOrEmpty(filterPath))
        {
            comboBox.Loaded -= ComboBox_Loaded;
            comboBox.Loaded += ComboBox_Loaded;

            if (comboBox.IsLoaded)
                AttachTextChangedHandler(comboBox, filterPath);
        }
    }

    private static void ComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
            return;

        var filterPath = GetFilterPath(comboBox);
        if (!string.IsNullOrEmpty(filterPath))
            AttachTextChangedHandler(comboBox, filterPath);
    }

    private static void AttachTextChangedHandler(ComboBox comboBox, string filterPath)
    {
        var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
        if (textBox == null)
            return;

        textBox.TextChanged += (s, _) =>
        {
            if (s is not TextBox tb)
                return;

            var view = CollectionViewSource.GetDefaultView(comboBox.ItemsSource);
            if (view == null)
                return;

            var filterText = tb.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(filterText))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = item =>
                {
                    if (item == null)
                        return false;

                    var value = GetPropertyValue(item, filterPath);
                    return value?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true;
                };
            }

            if (!comboBox.IsDropDownOpen && !string.IsNullOrEmpty(filterText))
                comboBox.IsDropDownOpen = true;
        };
    }

    private static string? GetPropertyValue(object item, string propertyPath)
    {
        var type = item.GetType();
        var prop = type.GetProperty(propertyPath);
        return prop?.GetValue(item)?.ToString();
    }
}
