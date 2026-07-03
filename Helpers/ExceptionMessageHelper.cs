using Microsoft.EntityFrameworkCore;

namespace MoneyPenny.Helpers;

public static class ExceptionMessageHelper
{
    public static string GetDetail(Exception exception)
    {
        if (exception is DbUpdateException dbUpdateException
            && !string.IsNullOrWhiteSpace(dbUpdateException.InnerException?.Message))
        {
            return dbUpdateException.InnerException.Message;
        }

        return exception.GetBaseException().Message;
    }
}
