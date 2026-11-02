using System.Diagnostics.CodeAnalysis;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Argument checks the runtime can make on every target. <c>ArgumentNullException.ThrowIfNull</c> and
    /// <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> arrived after netstandard2.0; these throw the same
    /// exceptions with the same parameter names.
    /// </summary>
    internal static class Guard
    {
        /// <summary>Throws <see cref="ArgumentNullException"/> when <paramref name="value"/> is null.</summary>
        public static void NotNull([NotNull] object? value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> when <paramref name="value"/> is null and
        /// <see cref="ArgumentException"/> when it is empty or white space.
        /// </summary>
        public static void NotNullOrWhiteSpace([NotNull] string? value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", paramName);
            }
        }
    }
}
