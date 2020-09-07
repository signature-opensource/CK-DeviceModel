using System;
using System.Collections.Generic;
using System.Text;

namespace CK.DeviceModel
{
    /// <summary>
    /// Support for type safe generic <see cref="CloneableCopyCtorExtension.Clone{T}(T)"/>.
    /// Classes that implement this interface MUST expose a public copy constructor.
    /// </summary>
    public interface ICloneableCopyCtor
    {
    }

    /// <summary>
    /// Implements <see cref="ICloneableCopyCtor"/>.
    /// </summary>
    public static class CloneableCopyCtorExtension
    {
        /// <summary>
        /// Clones this object by using its copy constructor (that must exist).
        /// </summary>
        /// <returns>A cloned instance.</returns>
        public static T Clone<T>( this T o ) where T : ICloneableCopyCtor
        {
            return (T)Activator.CreateInstance( typeof( T ), new object[] { o } );
        }

    }

}
