using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Configuration;
using System.Xml;
using System.Collections;

namespace LoadTrace
{
    partial class Program
    {
    }

    public class LoadTraceSection : ConfigurationSection
    {
        // Create a "remoteOnly" attribute.
        [ConfigurationProperty("verbose", DefaultValue=false)]
        public Boolean Verbose
        {
            get
            {
                return (Boolean)this["verbose"];
            }
            //set
            //{
            //    this["verbose"] = value;
            //}
        }

        [ConfigurationProperty("reprocess", DefaultValue=false)]
        public Boolean Reprocess
        {
            get
            {
                return (Boolean)this["reprocess"];
            }
        }

        [ConfigurationProperty("resetPermissions", DefaultValue=false)]
        public Boolean ResetPermissions
        {
            get
            {
                return (Boolean)this["resetPermissions"];
            }
        }

        [ConfigurationProperty("cleanUpServerName", DefaultValue = false)]
        public Boolean CleanUpServerName
        {
            get
            {
                return (Boolean)this["cleanUpServerName"];
            }
        }

        [ConfigurationProperty("server", IsRequired=true)]
        public string Server
        {
            get
            {
                return (string)this["server"];
            }
        }

        [ConfigurationProperty("workDirectory", IsRequired=false, DefaultValue="")]
        public string WorkDirectory
        {
            get
            {
                return (string)this["workDirectory"];
            }
        }

        [ConfigurationProperty("archiveDirectory", IsRequired = false, DefaultValue = "")]
        public string ArchiveDirectory
        {
            get
            {
                return (string)this["archiveDirectory"];
            }
        }

        [ConfigurationProperty("database", IsRequired=true)]
        public string Database
        {
            get
            {
                return (string)this["database"];
            }
        }

        [ConfigurationProperty("servers")]
        public ServerElementCollection Servers
        {
            get { return (ServerElementCollection)this["servers"]; }
        }

        [ConfigurationProperty("appNameFixes")]
        public AppNameFixElementCollection AppNameFixes
        {
            get { return (AppNameFixElementCollection)this["appNameFixes"]; }
        }
    }

    public class ServerElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }


        [ConfigurationProperty("traceWildcard", IsRequired = true)]
        public string traceWildcard
        {
            get { return (string)this["traceWildcard"]; }
            set { this["traceWildcard"] = value; }
        }
    }

    public class ServerElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new ServerElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ServerElement)element).Name;
        }
    }

    public class AppNameFixElement : ConfigurationElement
    {
        [ConfigurationProperty("regex", IsKey = true, IsRequired = true)]
        public string Regex
        {
            get { return (string)this["regex"]; }
        }

        [ConfigurationProperty("replacement", IsRequired = true)]
        public string Replacement
        {
            get { return (string)this["replacement"]; }
        }
    }

    public class AppNameFixElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new AppNameFixElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((AppNameFixElement)element).Regex;
        }
    }

    public static class LoadTraceConfig
    {
        public static LoadTraceSection AppConfigFile;
    }
}
