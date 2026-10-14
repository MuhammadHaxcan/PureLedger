using System.Reflection;
using System.Text.RegularExpressions;

namespace PureLedger.Exceptions
{
    /// <summary>
    /// The caller has no valid identity (not signed in, or the account is missing/inactive). It is an
    /// <see cref="UnauthorizedAccessException"/> so existing catch blocks still match, but it maps to
    /// HTTP 401, whereas every other <see cref="UnauthorizedAccessException"/> is an access denial (403).
    /// </summary>
    public sealed class NotAuthenticatedException : UnauthorizedAccessException
    {
        public NotAuthenticatedException(string message = "User is not authenticated.") : base(message)
        {
        }
    }

    /// <summary>
    /// Separates deliberate messages from internal ones. Application code reports business and validation
    /// problems with <c>InvalidOperationException</c>, <c>ArgumentException</c> and <c>KeyNotFoundException</c>,
    /// and those messages are written for users. The same types are also thrown by the framework, EF Core and
    /// Npgsql ("Sequence contains no elements", "A second operation was started on this context...",
    /// "Value cannot be null (Parameter 'x')"), and those must never reach a client.
    /// </summary>
    public static class ExceptionClassifier
    {
        private static readonly Assembly ApplicationAssembly = typeof(ExceptionClassifier).Assembly;
        private static readonly Regex ParameterSuffix =
            new(@"\s*\(Parameter '[^']*'\)\s*$", RegexOptions.Compiled);

        /// <summary>True when the exception was thrown by this application's own code.</summary>
        public static bool IsApplicationRaised(Exception exception) =>
            exception.TargetSite?.DeclaringType?.Assembly == ApplicationAssembly;

        /// <summary>The exception's own message when the application raised it, otherwise the supplied fallback.</summary>
        public static string SafeMessage(Exception exception, string fallback) =>
            IsApplicationRaised(exception) ? UserMessage(exception) : fallback;

        /// <summary>The message to show a user; drops the "(Parameter 'name')" suffix ArgumentException appends.</summary>
        public static string UserMessage(Exception exception) =>
            ParameterSuffix.Replace(exception.Message, string.Empty);
    }
}
