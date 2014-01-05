using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Utilities;


namespace Utilities
{
    public class Console
    {
        public Console()
        {
            Silent = false;
            Detail = false;
        }

        public void Write(String message)
        {
            if (Silent == false)
            {
                System.Console.Write(message);
            }
        }

        public void WriteLine(String message)
        {
            Write(message + "\r\n");
        }

        public void WriteLine()
        {
            WriteLine("");
        }

        public void ReportException(Exception ex)
        {
            WriteLine(ex.GetType().ToString() + ": " + ex.Message);
        }

        public bool CheckConfirm()
        {
            String confirmString = System.Console.ReadLine();

            if (confirmString.StartsWith("y") ||
                confirmString.StartsWith("Y"))
            {
                return true;
            }

            return false;
        }

        public bool Silent { set; get; }
        public bool Detail { set; get; }
    }
}
