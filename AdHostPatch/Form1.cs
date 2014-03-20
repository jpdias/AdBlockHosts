using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdHostPatch
{
    public partial class Form1 : Form
    {
        private static readonly String[] Sites =
        {
            "http://hosts-file.net/ad_servers.asp",
            "http://pgl.yoyo.org/adservers/serverlist.php?hostformat=hosts&showintro=0&mimetype=plaintext",
            "http://winhelp2002.mvps.org/hosts.txt",
            "http://someonewhocares.org/hosts/hosts"
        };

        private static readonly string Hostslocal = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            button1.Enabled = false;
            Task.Factory.StartNew(() =>
            {
                var filedit = getFileEditTime(hostslocal);
                Task<DateTime?>[] tasks = sites.Select(getPageEdittime).ToArray();

                this.BeginInvoke(new MethodInvoker(delegate
                {
                    if (Task.WaitAll(tasks, 20000))
                    {
                        var numberofupdates = tasks.Count(site =>
                        {
                            if (site.Result.HasValue)
                                return filedit < site.Result.Value;
                            else
                            {
                                label2.Text = "No internet connection :(";
                                return false;
                            }
                        });

                        if (tasks.Any(t => t.Result.HasValue))
                        {
                            label2.Text = string.Format("Available updates {0}/{1}", numberofupdates, sites.Length);
                            button1.Enabled = true;
                        }
                    }
                    else
                    {
                        label2.Text = "No internet connection :(";
                    }
                }));
            });
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var oldValue = button1.Text;
            button1.Enabled = false;
            button1.Text = "Running";

            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.Maximum = 100;
            progressBar1.Value = 1;

            var sitesTask = sites.Select(download).ToList();

            using (StreamWriter w = File.CreateText("hosts_temp"))
            {
                foreach (var site in sitesTask)
                {
                    Log(site.Result, w);
                    progressBar1.Value += 80/sites.Length;
                }
                //Log(adaway, w);
              
            }

            progressBar1.Value = 85;
            RemDuplicate();
            progressBar1.Value = 95;
           
            File.Copy("hosts",  hostslocal, true);
            progressBar1.Value = 100;
            File.Delete("hosts");
            File.Delete("hosts_temp");
            button1.Text = oldValue;
            button1.Enabled = true;
        }
        public static void Log(string logMessage, TextWriter w)
        {
            if (logMessage != null)
            {
                 w.WriteLine(logMessage);
            }
           
        }

        private static void RemDuplicate()
        {
            using (var sr = File.OpenText("hosts_temp"))
            {
                using (var sw = new StreamWriter(File.OpenWrite("hosts")))
                {
                    var domains = new HashSet<string>();
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                            continue;
                        var arr = line.Split(' ', '\t');
                        if (arr.Length < 2)
                            continue;
                        if (!domains.Add(arr[1]))
                            continue;
                        var i = line.IndexOf('#');
                        if (i != -1)
                            line = line.Substring(0, i);
                        line = line.Trim();
                        sw.WriteLine(line);
                    }
                    sw.Flush();
                }
            }
        }

        public static Task<String> download(string URL)
        {
            var webClient = new WebClient();
            return  Task<String>.Factory.StartNew(() =>
            {
                try
                {
                   return webClient.DownloadString(URL);
                }
                catch (Exception ex)
                {
                    Console.Write(ex);
                    return null;
                }
            });
        }

        public static DateTime getFileEditTime(string filePath)
        {
            return new FileInfo(filePath).LastWriteTime;
        }

        public static Task<DateTime?> getPageEdittime(string url)
        {
            return Task<DateTime?>.Factory.StartNew(() =>
            {
                try
                {
                    var req = (HttpWebRequest) WebRequest.Create(url);
                    var res = (HttpWebResponse) req.GetResponse();

                    return res.LastModified;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                        return null;
                    return DateTime.Now;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return DateTime.Now;
                }
            });
        }


    }
}
