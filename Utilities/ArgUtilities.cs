using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utilities
{
    public class ArgUtilities
    {
        public static bool HasAnotherArgument(
            string[] args,
            int argIndex,
            Utilities.Console console)
        {
            if (argIndex >= args.Length)
            {
                console.WriteLine("Missing argument for option.");
                int exitCode = 1;
                Environment.Exit(exitCode);
            }

            return true;
        }
    }
}
