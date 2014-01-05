using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utilities
{
    public class PathUtilities
    {
        /// <summary>
        /// Combine a native path with a standard stem to produce a native
        /// path.
        /// </summary>
        /// <param name="nativePath">
        /// Native path
        /// </param>
        /// <param name="standardStem">
        /// Standard stem
        /// </param>
        /// <returns>
        /// Combined native path
        /// </returns>
        static public string NativeFromNativeAndStandard(
            String nativePath,
            String standardStem)
        {
            // TODO: Fix this because the paths look funny on MSDOS even
            // though they work correctly.

            // Here we are using a trick that a standard file path can be
            // interpreted correctly as the latter part of a native path in
            // MS-DOS.
            return System.IO.Path.Combine(
                nativePath,
                standardStem);
        }
    }
}
