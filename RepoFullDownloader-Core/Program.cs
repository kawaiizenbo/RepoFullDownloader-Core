using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;

using RepoFullDownloader;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;

namespace RepoFullDownloader_Core
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("RepoFullDownloader by KawaiiZenbo");

            // Initial Checks
            if (!Directory.Exists("./output/"))
            {
                Directory.CreateDirectory("./output/");
            }
            if(args.Length != 0)
            {
                string url = args[0];
                if(!args[0].StartsWith("https://") || !args[0].StartsWith("http://"))
                {
                    url = "http://" + url;
                }
                if (!args[0].EndsWith("/"))
                {
                    url += "/";
                }
                try
                {
                    DownloadRepo(url, false);
                }
                catch(Exception)
                {
                    Console.WriteLine("No APT repo was found, trying as Installer repo");
                    DownloadInstallerRepo(url);
                }
            }
            else
            {
                if (!File.Exists("./options.json"))
                {
                    Console.WriteLine("Could not find options.json");
                    Console.WriteLine("Generating example...");
                    // generate example options
                    Options exampleOptions = new Options();
                    exampleOptions.originalFilenames = false;
                    Repo repo1 = new Repo();
                    repo1.url = "http://cydia.invoxiplaygames.uk/";
                    repo1.isInstaller = false;
                    Repo repo2 = new Repo();
                    repo2.url = "http://apptapp.saurik.com";
                    repo2.isInstaller = true;
                    exampleOptions.repos = new[] { repo1, repo2 };
                    string exampleOut = JsonSerializer.Serialize(exampleOptions);
                    File.WriteAllText("./options.json", exampleOut);
                    return;
                }

                // Load Options from 'options.json'
                string optionsJson = File.ReadAllText("./options.json");
                Options options = JsonSerializer.Deserialize<Options>(optionsJson);

                foreach (Repo r in options.repos)
                {
                    if (r.isInstaller)
                    {
                        DownloadInstallerRepo(r.url);
                    }
                    else
                    {
                        DownloadRepo(r.url, options.originalFilenames);
                    }
                }
            }

            // this is the part where it is over
            Console.WriteLine("done :)");
        }

        static void DownloadRepo(string link, bool keepOg)
        {
            
            WebClient webClient = new WebClient();
            // headers because some repos are 'interesting'
            webClient.Headers.Add("X-Machine", "iPod4,1");
            webClient.Headers.Add("X-Unique-ID", "0000000000000000000000000000000000000000");
            webClient.Headers.Add("X-Firmware", "6.1");
            webClient.Headers.Add("User-Agent", "Telesphoreo APT-HTTP/1.0.999");
            // Attempt to download packages file (try/catch hell)
            try
            {
                Console.WriteLine("Attempting to download " + link + "Packages.bz2");
                webClient.DownloadFile(new Uri(link + "Packages.bz2"), "Packages.bz2");
                FileStream packagesBz2 = new FileInfo("Packages.bz2").OpenRead();
                FileStream packagesBz2Decompressed = File.Create("Packages");
                BZip2.Decompress(packagesBz2, packagesBz2Decompressed, true);
            }
            catch (Exception e)
            {
                try
                {
                    Console.WriteLine("Could not download " + link + "Packages.bz2: " + e.Message);
                    Console.WriteLine("Attempting to download " + link + "Packages.gz");
                    webClient.DownloadFile(new Uri(link + "Packages.gz"), "Packages.gz");
                    FileStream packagesGz = new FileInfo("Packages.gz").OpenRead();
                    FileStream packagesGzDecompressed = File.Create("Packages");
                    BZip2.Decompress(packagesGz, packagesGzDecompressed, true);
                }
                catch (Exception _e)
                {
                    try
                    {
                        Console.WriteLine("Could not download " + link + "Packages.gz: " + _e.Message);
                        Console.WriteLine("Attempting to download " + link + "Packages");
                        webClient.DownloadFile(new Uri(link + "Packages"), "Packages");
                    }
                    catch (Exception __e)
                    {
                        Console.WriteLine("Could not download " + link + "Packages: " + __e.Message);
                        Console.WriteLine("Could not locate packages file in " + link);
                        Console.WriteLine(__e.Message);
                        throw;
                    }
                }
            }
            Thread.Sleep(500);
            // Clean list of package links, names, and versions
            List<CydiaPackage> packages = new List<CydiaPackage>();
            foreach (string s in File.ReadAllText("Packages").Split("\n\n"))
            {
                string name = "";
                string version = "";
                string _link = "";
                foreach(string s2 in s.Split('\n'))
                {
                    if (s2.StartsWith("Package: "))
                    {
                        name = s2.Remove(0, 8).Trim();
                    }
                    else if (s2.StartsWith("Version: "))
                    {
                        version = s2.Remove(0, 8).Trim();
                    }
                    else if (s2.StartsWith("Filename: "))
                    {
                        _link = s2.Remove(0, 9).Trim();
                    }
                }
                packages.Add(new CydiaPackage(_link, name, version));
            }
            packages.RemoveAt(packages.Count - 1);
            foreach(CydiaPackage p in packages)
            {
                // Download all packages on repo
                try
                {
                    Random r = new Random();
                    try
                    {
                        string[] choppedUp = p.link.Split('/');
                        string fileToDownload = keepOg ? "./output/" + choppedUp[choppedUp.Length - 1] : "./output/" + p.name + "-" + p.version + ".deb";
                        if (File.Exists(fileToDownload))
                        {
                            fileToDownload += "_" + r.Next(0000, 9999);
                        }
                        webClient.DownloadFile(new Uri(link + p.link), fileToDownload);
                        Console.WriteLine("Successfully downloaded " + link + p.link + " as " + fileToDownload);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Could not download " + link + p.link);
                        Console.WriteLine(e.Message);
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("Finished downloading " + link);
                }
            }
        }

        static void DownloadInstallerRepo(string link)
        {
            WebClient webClient = new WebClient();
            try
            {
                Console.WriteLine("Attempting to download installer repo " + link);
                webClient.DownloadFile(new Uri(link), "packages.xml");
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not download package list from " + link + ": " + e.Message);
                return;
            }
            List<string> plist = new List<string>(File.ReadAllLines("packages.xml"));
            List<string> packages = new List<string>();
            int i = 1;
            foreach (string s in plist)
            {
                if (s.Contains("ocation</key>"))
                {
                    packages.Add(plist[i].Split('<')[1].Remove(0, 7));
                }
                i++;
            }
            foreach (string s in packages)
            {
                Random r = new Random();
                try
                {
                    string[] choppedUp = s.Split('/');
                    string fileToDownload = "./output/" + choppedUp[choppedUp.Length - 1];
                    if (File.Exists(fileToDownload))
                    {
                        fileToDownload += "_" + r.Next(0000, 9999);
                    }
                    webClient.DownloadFile(new Uri(s), fileToDownload);
                    Console.WriteLine("Successfully downloaded " + s);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Could not download " + s);
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
