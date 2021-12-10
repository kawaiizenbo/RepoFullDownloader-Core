using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepoFullDownloader
{
    class Options
    {
        public bool originalFilenames { get; set; } = false;
        public Repo[] repos { get; set; } = { 
            new Repo() 
            { 
                url = "http://repo.kawaiizenbo.me/", 
                type = "cydia"
            }, 
            new Repo() 
            { 
                url = "http://apptapp.saurik.com/", 
                type = "installer" 
            },
            new Repo()
            {
                url = "http://apt.saurik.com/",
                type = "dist",
                distAttributes = new DistAttributes()
                {
                    suites = "ios/",
                    components = "main",
                }
            }
        };
        public int delay { get; set; } = 1;
    }

    class Repo
    {
        public string url { get; set; }
        public string type { get; set; }
        public DistAttributes distAttributes { get; set; } = null;
    }

    class DistAttributes
    {
        public string suites { get; set; }
        public string components { get; set; }
    }

    class CydiaPackage
    {
        public CydiaPackage(string _link, string _name, string _version)
        {
            this.link = _link;
            this.name = _name;
            this.version = _version;
        }
        public string link { get; set; }
        public string name { get; set; }
        public string version { get; set; }
    }
}
