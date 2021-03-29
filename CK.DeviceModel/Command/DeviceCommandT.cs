using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.DeviceModel
{
    /// <summary>
    /// Abstract base class for commands with result that a device can handle.
    /// </summary>
    public abstract class DeviceCommand<THost,TResult> : DeviceCommandWithResult<TResult> where THost : IDeviceHost
    {
        /// <summary>
        /// Initializes a new <see cref="DeviceCommand{THost,TResult}"/>.
        /// </summary>
        protected DeviceCommand()
        {
        }

        /// <summary>
        /// Initializes a new <see cref="DeviceCommand{THost,TResult}"/> that transforms any exception or cancellation into
        /// a specific result.
        /// See <see cref="CommandCompletionSource{TResult}"/>.
        /// </summary>
        /// <param name="errorOrCancelResult">Result that will replace error or cancellation.</param>
        public DeviceCommand( TResult errorOrCancelResult )
            : base( errorOrCancelResult )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="DeviceCommand{THost,TResult}"/> thats can transform errors or cancellation
        /// into specific results.
        /// See <see cref="CommandCompletionSource{TResult}"/>.
        /// </summary>
        /// <param name="transformError">Optional transformation from the error to a result.</param>
        /// <param name="ignoreCanceled">True to transform cancellation into <paramref name="ignoreCanceled"/> result value.</param>
        /// <param name="cancelResult">The result to use on cancellation. Used only is <paramref name="ignoreCanceled"/> is true.</param>
        protected DeviceCommand( Func<Exception, TResult>? transformError, bool ignoreCanceled, TResult cancelResult )
            : base( transformError, ignoreCanceled, cancelResult )
        {
        }


        /// <inheritdoc />
        public sealed override Type HostType => typeof(THost);
    }
}
