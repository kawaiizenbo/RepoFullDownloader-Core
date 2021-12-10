using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;

using PlistCS;

using RepoFullDownloader;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace RepoFullDownloader_Core
{
    class Program
    {
        private static Options options = new Options();

        static void Main(string[] args)
        {
            Console.WriteLine("RepoFullDownloader by KawaiiZenbo");

            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) =>
            {
                // security
                return true;
            };
            // Initial Checks
            if (!Directory.Exists("./output/"))
            {
                Directory.CreateDirectory("./output/");
            }
            // Load Options from 'options.json'
            if (!File.Exists("./options.json"))
            {
                Console.WriteLine("Could not find options.json");
                Console.WriteLine("Generating example...");
                // generate example options
                File.WriteAllText("./options.json", JsonSerializer.Serialize(new Options()));
                return;
            }
            string optionsJson = File.ReadAllText("./options.json");
            options = JsonSerializer.Deserialize<Options>(optionsJson);
            if (args.Length != 0)
            {
                string url = args[0];
                if(!args[0].StartsWith("https://") && !args[0].StartsWith("http://"))
                {
                    url = "http://" + url;
                }
                if (!args[0].EndsWith("/"))
                {
                    url += "/";
                }
                try
                {
                    DownloadRepo(url);
                }
                catch(Exception)
                {
                    Console.WriteLine("No APT repo was found, trying as Installer repo");
                    DownloadInstallerRepo(url);
                }
            }
            else
            {

                foreach (Repo r in options.repos)
                {
                    if (r.type.ToLower() == "installer")
                    {
                        DownloadInstallerRepo(r.url);
                    }
                    else if (r.type.ToLower() == "cydia")
                    {
                        DownloadRepo(r.url);
                    }
                    else if (r.type.ToLower() == "dist")
                    {
                        if(r.distAttributes == null)
                        {
                            Console.WriteLine($"No dist attributes were defined for {r.url} :(");
                            return;
                        }
                        DownloadDistRepo(r.url, r.distAttributes);
                    }
                    else
                    {
                        Console.WriteLine($"Invalid repo type {r.type} on {r.url}");
                    }
                }
            }

            // this is the part where it is over
            Console.WriteLine("done :)");
        }

        static void DownloadRepo(string link)
        {
            if (!link.StartsWith("https://") && !link.StartsWith("http://"))
            {
                link = "http://" + link;
            }
            if (!link.EndsWith("/"))
            {
                link += "/";
            }
            string cleanLink = link.TrimEnd('/').Replace("http://", "").Replace("https://", "").Replace("/", "_").Replace(":", "_");
            Directory.CreateDirectory($"./output/{cleanLink}");
            HttpClient webClient = new HttpClient();
            // headers because some repos are 'interesting'
            webClient.DefaultRequestHeaders.Add("X-Machine", "iPod4,1");
            webClient.DefaultRequestHeaders.Add("X-Unique-ID", "0000000000000000000000000000000000000000");
            webClient.DefaultRequestHeaders.Add("X-Firmware", "6.1");
            webClient.DefaultRequestHeaders.Add("User-Agent", "Telesphoreo APT-HTTP/1.0.999");
            // Attempt to download packages file (try/catch hell)
            try
            {
                Console.WriteLine("Attempting to download " + link + "Packages.bz2");
                Stream packagesBz2 = webClient.GetStreamAsync(link + "Packages.bz2").Result;
                FileStream packagesBz2Decompressed = File.Create($"./output/{cleanLink}/Packages");
                BZip2.Decompress(packagesBz2, packagesBz2Decompressed, true);
            }
            catch (Exception e)
            {
                try
                {
                    Console.WriteLine("Could not download " + link + "Packages.bz2: " + e.Message);
                    Console.WriteLine("Attempting to download " + link + "Packages.gz");
                    Stream packagesGz = webClient.GetStreamAsync(link + "Packages.bz2").Result;
                    FileStream packagesGzDecompressed = File.Create($"./output/{cleanLink}/Packages");
                    GZip.Decompress(packagesGz, packagesGzDecompressed, true);
                }
                catch (Exception _e)
                {
                    try
                    {
                        Console.WriteLine("Could not download " + link + "Packages.gz: " + _e.Message);
                        Console.WriteLine("Attempting to download " + link + "Packages");
                        using (StreamWriter outputFile = new StreamWriter($"./output/{cleanLink}/Packages"))
                        {
                            outputFile.WriteLine(webClient.GetStreamAsync(link + "Packages").Result);
                        }
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
            foreach (string s in File.ReadAllText($"./output/{cleanLink}/Packages").Split("\n\n"))
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
            // remove last one because ????
            packages.RemoveAt(packages.Count - 1);
            List<string> failed = new List<string>();
            foreach(CydiaPackage p in packages)
            {
                // Download all packages on repo
                Random r = new Random();
                try
                {
                    string[] choppedUp = p.link.Split('/');
                    string fileToDownload = options.originalFilenames ? $"./output/{cleanLink}/" + choppedUp[choppedUp.Length - 1].Replace("/", "_").Replace(":", "_") : $"./output/{cleanLink}/" + p.name.Replace("/", "_").Replace(":", "_") + "-" + p.version.Replace("/", "_").Replace(":", "_") + ".deb";
                    if (File.Exists(fileToDownload))
                    {
                        fileToDownload += "_" + r.Next(0000, 9999);
                    }
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFileAsync(new Uri(link + p.link), fileToDownload);
                    }
                    Console.WriteLine("Successfully downloaded " + link + p.link + " as " + fileToDownload);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Could not download " + link + p.link);
                    Console.WriteLine(e.Message);
                    failed.Add(link + p.link);
                }
                Thread.Sleep(options.delay);
            }
            Console.WriteLine("Finished downloading " + link);
            if(failed.Count != 0) File.WriteAllLines($"./output/{cleanLink}/failed.txt", failed);
        }

        static void DownloadInstallerRepo(string link)
        {
            if (!link.StartsWith("https://") && !link.StartsWith("http://"))
            {
                link = "http://" + link;
            }
            if (!link.EndsWith("/"))
            {
                link += "/";
            }
            string cleanLink = link.TrimEnd('/').Replace("http://", "").Replace("https://", "").Replace("/", "_").Replace(":", "_");
            Directory.CreateDirectory($"./output/{cleanLink}");
            WebClient webClient = new WebClient();
            webClient.Headers.Add("User-Agent", "AppTapp Installer/3.0 (iPhone/1.1, like CFNetwork/100.0)");
            try
            {
                Console.WriteLine("Attempting to download installer repo " + link);
                webClient.DownloadFile(link, $"./output/{cleanLink}/packages.plist");
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not download package list from " + link + ": " + e.Message);
                return;
            }
            Dictionary<string, object> plist = (Dictionary<string, object>)Plist.readPlist($"./output/{cleanLink}/packages.plist");
            foreach (Dictionary<string, object> d in (List<object>)plist["packages"])
            {
                Random r = new Random();
                try
                {
                    string[] choppedUp = d["location"].ToString().Split('/');
                    string fileToDownload = $"./output/{cleanLink}/" + choppedUp[choppedUp.Length - 1];
                    if (File.Exists(fileToDownload))
                    {
                        fileToDownload += "_" + r.Next(0000, 9999);
                    }
                    webClient.DownloadFile(new Uri((string)d["location"]), fileToDownload);
                    Console.WriteLine("Successfully downloaded " + (string)d["location"]);
                }
                catch (KeyNotFoundException)
                {
                    string[] choppedUp = d["url"].ToString().Split('/');
                    string fileToDownload = $"./output/{cleanLink}/" + choppedUp[choppedUp.Length - 1];
                    if (File.Exists(fileToDownload))
                    {
                        fileToDownload += "_" + r.Next(0000, 9999);
                    }
                    webClient.DownloadFile(new Uri((string)d["url"]), fileToDownload);
                    Console.WriteLine("Successfully downloaded " + (string)d["url"]);
                    Dictionary<string, object> pl = (Dictionary<string, object>)Plist.readPlist(fileToDownload);
                    {
                        string[] choppedUp2 = pl["location"].ToString().Split('/');
                        string fileToDownload2 = $"./output/{cleanLink}/" + choppedUp2[choppedUp2.Length - 1];
                        if (File.Exists(fileToDownload2))
                        {
                            fileToDownload2 += "_" + r.Next(0000, 9999);
                        }
                        webClient.DownloadFile(new Uri((string)pl["location"]), fileToDownload2);
                        Console.WriteLine("Successfully downloaded " + (string)pl["location"]);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Could not download " + (string)d["location"]);
                    Console.WriteLine(e.Message);
                }
            }
        }

        static void DownloadDistRepo(string link, DistAttributes da)
        {
            // clean up link so no issues can ever arise
            if (!link.StartsWith("https://") && !link.StartsWith("http://"))
            {
                link = "http://" + link;
            }
            if (!link.EndsWith("/"))
            {
                link += "/";
            }

            // make that good dist path
            string distPath;
            if(!da.suites.EndsWith("/"))
            {
                da.suites += "/";
            }
            if (!da.components.EndsWith("/"))
            {
                da.components += "/";
            }
            distPath = da.suites + da.components;
            string poolpfLink = link + "dists/" + distPath + "binary-iphoneos-arm/";

            string cleanLink = link.TrimEnd('/').Replace("http://", "").Replace("https://", "").Replace("/", "_").Replace(":", "_");
            Directory.CreateDirectory($"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})");
            HttpClient webClient = new HttpClient();
            // headers because some repos are 'interesting'
            webClient.DefaultRequestHeaders.Add("X-Machine", "iPod4,1");
            webClient.DefaultRequestHeaders.Add("X-Unique-ID", "0000000000000000000000000000000000000000");
            webClient.DefaultRequestHeaders.Add("X-Firmware", "6.1");
            webClient.DefaultRequestHeaders.Add("User-Agent", "Telesphoreo APT-HTTP/1.0.999");
            // Attempt to download packages file (try/catch hell)
            try
            {
                Console.WriteLine("Attempting to download " + poolpfLink + "Packages.bz2");
                Stream packagesBz2 = webClient.GetStreamAsync(poolpfLink + "Packages.bz2").Result;
                FileStream packagesBz2Decompressed = File.Create($"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})/Packages");
                BZip2.Decompress(packagesBz2, packagesBz2Decompressed, true);
            }
            catch (Exception e)
            {
                try
                {
                    Console.WriteLine("Could not download " + poolpfLink + "Packages.bz2: " + e.Message);
                    Console.WriteLine("Attempting to download " + poolpfLink + "Packages.gz");
                    Stream packagesGz = webClient.GetStreamAsync(poolpfLink + "Packages.gz").Result;
                    FileStream packagesGzDecompressed = File.Create($"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})/Packages");
                    GZip.Decompress(packagesGz, packagesGzDecompressed, true);
                }
                catch (Exception _e)
                {
                    try
                    {
                        Console.WriteLine("Could not download " + poolpfLink + "Packages.gz: " + _e.Message);
                        Console.WriteLine("Attempting to download " + poolpfLink + "Packages");
                        using (StreamWriter outputFile = new StreamWriter($"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})/Packages"))
                        {
                            outputFile.WriteLine(webClient.GetStreamAsync(poolpfLink + "Packages").Result);
                        }
                    }
                    catch (Exception __e)
                    {
                        Console.WriteLine("Could not download " + poolpfLink + "Packages: " + __e.Message);
                        Console.WriteLine("Could not locate packages file in " + poolpfLink);
                        Console.WriteLine(__e.Message);
                        throw;
                    }
                }
            }
            Thread.Sleep(500);
            // Clean list of package links, names, and versions
            List<CydiaPackage> packages = new List<CydiaPackage>();
            foreach (string s in File.ReadAllText($"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})/Packages").Split("\n\n"))
            {
                string name = "";
                string version = "";
                string _link = "";
                foreach (string s2 in s.Split('\n'))
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
            // remove last one because ????
            packages.RemoveAt(packages.Count - 1);
            List<string> failed = new List<string>();
            foreach (CydiaPackage p in packages)
            {
                // Download all packages on repo
                Random r = new Random();
                try
                {
                    string[] choppedUp = p.link.Split('/');
                    string fileToDownload = options.originalFilenames ? $"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})/" + choppedUp[choppedUp.Length - 1].Replace("/", "_").Replace(":", "_") : $"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})/" + p.name.Replace("/", "_").Replace(":", "_") + "-" + p.version.Replace("/", "_").Replace(":", "_") + ".deb";
                    if (File.Exists(fileToDownload))
                    {
                        fileToDownload += "_" + r.Next(0000, 9999);
                    }
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFileAsync(new Uri(link + p.link), fileToDownload);
                    }
                    Console.WriteLine("Successfully downloaded " + link + p.link + " as " + fileToDownload);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Could not download " + link + p.link);
                    Console.WriteLine(e.Message);
                    failed.Add(link + p.link);
                }
                Thread.Sleep(options.delay);
            }
            Console.WriteLine("Finished downloading " + link);
            if (failed.Count != 0) File.WriteAllLines($"./output/{cleanLink}-({distPath.Replace("/", "_").Replace(":", "_")})/failed.txt", failed);
        }
    }
}
