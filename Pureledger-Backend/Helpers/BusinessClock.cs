namespace PureLedger.Helpers
{
    /// <summary>
    /// The one definition of "today" for accounting dates (entry dates, receipt dates, decision dates).
    /// Instants such as token expiry and audit timestamps stay in UTC; calendar dates follow the business
    /// time zone, so the date does not depend on the server's own zone or flip at 05:00 local time.
    /// </summary>
    public static class BusinessClock
    {
        public const string DefaultTimeZoneId = "Asia/Karachi";

        private static TimeZoneInfo _zone = Resolve(DefaultTimeZoneId);

        public static TimeZoneInfo TimeZone => _zone;

        /// <summary>Today's calendar date in the business time zone.</summary>
        public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, _zone));

        /// <summary>Applies the configured zone id (IANA or Windows). A blank value keeps the default.</summary>
        public static void Configure(string? timeZoneId) =>
            _zone = Resolve(string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim());

        private static TimeZoneInfo Resolve(string id)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Windows hosts without ICU data know Pakistan by its Windows id.
                if (id == DefaultTimeZoneId)
                {
                    try { return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time"); }
                    catch (Exception inner) when (inner is TimeZoneNotFoundException or InvalidTimeZoneException) { }
                    return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "Pakistan Standard Time", "Pakistan Standard Time");
                }
                throw new InvalidOperationException(
                    $"Business:TimeZoneId '{id}' is not a known time zone on this server.", ex);
            }
        }
    }
}
