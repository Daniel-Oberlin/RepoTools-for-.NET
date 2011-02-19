using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace RepositorySync
{
    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;
            DateTime startTime = DateTime.Now;

            int argIndex = 0;

            string commandArg = "help";
            if (args.Length > 0)
            {
                commandArg = args[argIndex++];
            }

            //RepositoryTool tool = new RepositoryTool();

            //tool.WriteLogDelegate =
            //    delegate(String message)
            //    {
            //        Write(message);
            //    };

            while (argIndex < args.Length)
            {
                string nextArg = args[argIndex++];

                switch (nextArg)
                {
                }
            }

            switch (commandArg)
            {
            }
        }
    }
}
