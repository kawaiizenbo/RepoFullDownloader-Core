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
        public Repo[] repos { get; set; } = { new Repo() { url = "http://cydia.invoxiplaygames.uk/" , isInstaller = false}, new Repo() { url = "http://apptapp.saurik.com/", isInstaller = true } };
        public int delay { get; set; } = 1;
    }

    class Repo
    {
        public string url { get; set; }
        public bool isInstaller { get; set; }
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
