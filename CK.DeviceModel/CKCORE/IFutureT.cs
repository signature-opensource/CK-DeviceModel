using System.Diagnostics.CodeAnalysis;

namespace CK.Core
{

    /// <summary>
    /// Implementation of a <see cref="Future"/> with a <see cref="Value"/>.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public interface IFuture<T>
    {
        /// <summary>
        /// Gets whether <see cref="IsCompleted"/> is true and a <see cref="Value"/> is available.
        /// </summary>
        bool HasValue { get; }

        /// <summary>
        /// Gets the final result.
        /// This must be called when and only when <see cref="HasValue"/> is true
        /// otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        [MaybeNull]
        T Value { get; }
    }
}
