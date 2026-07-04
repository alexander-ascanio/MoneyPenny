using System.Globalization;

namespace MoneyPenny.Models.Tickets;

public class IndexedTicketsMonthCount
{
    public int Year { get; init; }
    public int Month { get; init; }
    public int TicketCount { get; init; }

    public string MonthName
    {
        get
        {
            var culture = CultureInfo.GetCultureInfo("es-ES");
            var monthName = culture.DateTimeFormat.GetMonthName(Month);
            if (string.IsNullOrEmpty(monthName))
            {
                return Month.ToString("00", culture);
            }

            return $"{char.ToUpper(monthName[0], culture)}{monthName[1..]}";
        }
    }

    public string Label
    {
        get
        {
            if (Month < 1 || Month > 12)
            {
                return $"{Month:00}/{Year}";
            }

            return $"{MonthName} {Year}";
        }
    }
}
