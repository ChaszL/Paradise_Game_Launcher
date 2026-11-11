namespace GameLauncher;
public class DashboardProgressConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        double percentage = 0;
        if (value is double d) percentage = d;
        else if (value is float f) percentage = f;
        else if (value is int i) percentage = i;

        // Keep bounds between 0% and 100%
        percentage = Math.Max(0, Math.Min(100, percentage));

        // The parameter represents the relative circumference of the circle
        double circumference = double.Parse((string)parameter, System.Globalization.CultureInfo.InvariantCulture);
        double dashLength = (percentage / 100.0) * circumference;
        
        // Return the filled amount, followed by a massive empty gap (1000) so it doesn't repeat
        return new System.Windows.Media.DoubleCollection { dashLength, 1000 }; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    { 
        throw new NotImplementedException();
    }
}