using System;
using System.Globalization;
using System.Windows.Data;

namespace HRMS
{
    public class BooleanToActiveTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? "Active" : "Inactive";
            }
            return "Inactive";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                return s.Equals("Active", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}
