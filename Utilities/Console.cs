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

        public String EnterPassword()
        {
            String password = "";

            ConsoleKeyInfo info = System.Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    System.Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        // Remove one character
                        password = password.Substring(
                            0,
                            password.Length - 1);

                        // Get the location of the cursor
                        int pos = System.Console.CursorLeft;

                        // Move the cursor to the left by one character
                        System.Console.SetCursorPosition(
                            pos - 1,
                            System.Console.CursorTop);

                        // Replace it with space
                        System.Console.Write(" ");

                        // move the cursor to the left by one character again
                        System.Console.SetCursorPosition(
                            pos - 1,
                            System.Console.CursorTop);
                    }
                }

                info = System.Console.ReadKey(true);
            }

            // Add a new line because user pressed enter
            System.Console.WriteLine();

            return password;
        }

        public bool Silent { set; get; }
        public bool Detail { set; get; }
    }
}
