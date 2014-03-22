using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdHostPatch
{
    public partial class Form1 : Form
    {
        private static readonly List<string> Sites = new List<string>
        {
            "http://hosts-file.net/ad_servers.asp",
            "http://pgl.yoyo.org/adservers/serverlist.php?hostformat=hosts&showintro=0&mimetype=plaintext",
            "http://winhelp2002.mvps.org/hosts.txt",
            "http://someonewhocares.org/hosts/hosts",
            "http://hostsfile.mine.nu/Hosts"
        };

        private static readonly string Hostslocal = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // Setup tooltip
            var toolTip = new ToolTip
            {
                AutomaticDelay = 500,
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 100,
                ShowAlways = true,
                ToolTipTitle = "Create local hosts file",
                ToolTipIcon = ToolTipIcon.Info
            };
            toolTip.SetToolTip(checkBox1, "If checked the system's hosts file will not be changed.");

            // Setup listbox
            siteListBox.Items.AddRange(Sites.ToArray());

            // Count available updates
            button1.Enabled = false;
            var filedit = GetFileEditTime(Hostslocal);
            var tasksQuery = Sites.Select(GetPageEdittime);
            var tasks = tasksQuery.ToList();
            var numberOfUpdates = 0;
            label2.Text = string.Format("No available updates (0/{0})", Sites.Count);
            while (tasks.Count > 0)
            {
                var firstFinishedTask = await Task.WhenAny(tasks);
                tasks.Remove(firstFinishedTask);

                var siteEditDate = await firstFinishedTask;
                if (siteEditDate.HasValue && filedit < siteEditDate)
                {
                    numberOfUpdates++;
                    label2.Text = string.Format("Available updates ({0}/{1})", numberOfUpdates, Sites.Count);
                    button1.Enabled = true;
                }
                else if (!siteEditDate.HasValue)
                {
                    label2.Text = "No internet connection :(";
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var oldValue = button1.Text;
            button1.Enabled = false;
            button1.Text = "Running";

            progressBar1.Value = 1;

            var downloadTasksQuery = Sites.Select(s => Download(s, null));
            var downloadTasks = downloadTasksQuery.ToList();

            using (var w = File.CreateText("hosts_temp"))
            {
                while (downloadTasks.Count > 0)
                {
                    var firstFinishedTask = await Task.WhenAny(downloadTasks);
                    downloadTasks.Remove(firstFinishedTask);
                    var str = await firstFinishedTask;
                    if (str != null)
                        Log(str, w);
                    progressBar1.Value += 80/Sites.Count;
                }
            }

            toolStripStatusLabel.Text = "Done downloading hosts file.";

            progressBar1.Value = 85;
            try
            {
                RemDuplicate();
            }
            catch (Exception ex)
            {
                ShowErrorBox(ex.ToString());
            }
            progressBar1.Value = 95;

            var localHostsChecked = checkBox1.Checked;
            if (!localHostsChecked)
            {
                try
                {
                    File.Copy("hosts", Hostslocal, true);
                }
                catch (UnauthorizedAccessException)
                {
                    ShowErrorBox("Could not change system's hosts file.\nRun this program in Administrator mode.");
                }
                catch (Exception ex)
                {
                    ShowErrorBox(ex.ToString());
                }
            }
            progressBar1.Value = 100;
            if (!localHostsChecked)
            {
                try
                {
                    File.Delete("hosts");
                }
                catch (Exception ex)
                {
                    ShowErrorBox(ex.ToString());
                }
            }

            try
            {
                File.Delete("hosts_temp");
            }
            catch (Exception ex)
            {
                ShowErrorBox(ex.ToString());
            }

            button1.Text = oldValue;
            button1.Enabled = true;
        }

        private static void Log(string logMessage, TextWriter w)
        {
            if (logMessage != null)
                w.WriteLine(logMessage);
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

        private static async Task<String> Download(string url, IProgress<long> progress)
        {
            try
            {
                var webClient = new WebClient();

                webClient.DownloadProgressChanged += (sender, args) => progress.Report(args.ProgressPercentage);

                return await webClient.DownloadStringTaskAsync(url);
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return null;
            }
        }

        private static DateTime GetFileEditTime(string filePath)
        {
            return new FileInfo(filePath).LastWriteTime;
        }

        private static async Task<DateTime?> GetPageEdittime(string url)
        {
            try
            {
                var req = (HttpWebRequest) WebRequest.Create(url);
                var res = (HttpWebResponse) await req.GetResponseAsync();
                return res.LastModified;
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex);
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                    return null;
                return DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return DateTime.Now;
            }
        }

        private static void ShowErrorBox(string text)
        {
            MessageBox.Show(text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void addSiteButton_Click(object sender, EventArgs e)
        {
            var uriStr = siteTextBox.Text;
            if (string.IsNullOrWhiteSpace(uriStr))
                return;

            Uri _;
            if (!Uri.TryCreate(uriStr, UriKind.Absolute, out _))
            {
                ShowErrorBox(string.Format("{0} is not a valid URL.", uriStr));
                return;
            }

            siteTextBox.Text = string.Empty;
            Sites.Add(uriStr);
            siteListBox.Items.Add(uriStr);

            // scroll to bottom of the site list box
            var visibleItems = siteListBox.ClientSize.Height / siteListBox.ItemHeight;
            siteListBox.TopIndex = Math.Max(siteListBox.Items.Count - visibleItems + 1, 0);
        }

        private void siteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                addSiteButton.PerformClick();
        }
    }
}
