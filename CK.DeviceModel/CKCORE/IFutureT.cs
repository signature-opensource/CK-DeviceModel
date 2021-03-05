using System.Diagnostics.CodeAnalysis;

namespace CK.Core
{

    /// <summary>
    /// Implementation of a <see cref="Future"/> with a <see cref="Value"/>.
    /// <para>
    /// The <see cref="IFuture.AsTask()"/> exposes a task that can be awaited before exploiting
    /// the <see cref="Value"/>. This <c>AsTask()</c> doesn't return the Value itself and it is intentional.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public interface IFuture<out T> : IFuture
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
