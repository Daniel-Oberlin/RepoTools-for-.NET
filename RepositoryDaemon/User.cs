using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryDaemon
{
    public class User
    {
        public User(String name, bool isAdministrator = false)
        {
            Name = name;
            IsAdministrator = isAdministrator;
        }

        public String Name { set; get; }
        public bool IsAdministrator { set; get; }
    }
}
