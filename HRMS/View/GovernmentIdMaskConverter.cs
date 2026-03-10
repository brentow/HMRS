using HRMS.Model;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HRMS.View
{
    public sealed class GovernmentIdMaskConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var rawValue = values.Length > 0 ? values[0]?.ToString() : null;
            var isReadOnly = values.Length > 1 && values[1] is bool readOnly && readOnly;

            return isReadOnly
                ? SensitiveIdProtector.Mask(rawValue)
                : rawValue ?? string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new[]
            {
                value?.ToString() ?? string.Empty,
                Binding.DoNothing
            };
        }
    }
}
