// PC Tune-Up - a safe, honest Windows speed-up helper.
// Only does things that actually help: frees disk space, shows what is using
// memory, manages startup apps and power mode. No "RAM boosters" or registry
// cleaners - those do not make PCs faster.
//
// Build: run build.bat (uses the C# compiler that ships with Windows).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("PC Tune-Up")]
[assembly: AssemblyProduct("PC Tune-Up")]
[assembly: AssemblyDescription("A safe, honest Windows speed-up helper")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace PCTuneUp
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                         ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX m);

        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        [DllImport("kernel32.dll")]
        public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);
    }

    class CleanTarget
    {
        public string Name, Path;
        public bool Admin, Recycle;
        public long Size;
        public bool Available;
        public string Reason;
    }

    class StartupItem
    {
        public string Name, Command, Where;
        public RegistryHive Hive;
        public string ApprovedKey, ApprovedValue;
        public bool Enabled, NeedsAdmin;
    }

    class MainForm : Form
    {
        static readonly bool IsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        static readonly float Dpi = GetDpiScale();
        static float GetDpiScale()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f; } catch { return 1f; }
        }
        const string Approved =@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        Label status;
        TabControl tabs;
        TabPage ovTab, clTab, stTab, prTab, twTab;
        Timer timer;
        bool busy;

        // overview
        Label cpuLabel, ramLabel, diskLabel, infoLabel;
        ProgressBar cpuBar, ramBar, diskBar;
        TextBox tipsBox;
        string cpuName;
        long prevIdle, prevKernel, prevUser;
        int startupCount;

        // tabs
        ListView clv, slv, plv;
        Label planLabel;
        List<CleanTarget> targets;

        static readonly string[] ProtectedNames = {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost",
            "dwm", "fontdrvhost", "sihost", "ctfmon", "MsMpEng", "NisSrv", "SecurityHealthService",
            "Memory Compression", "explorer", "audiodg", "spoolsv", "PCTuneUp" };

        public MainForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None; // layout is in 96-DPI pixels; scaled once at the end
            Text = "PC Tune-Up" + (IsAdmin ? "  (Administrator)" : "");
            ClientSize = new Size(840, 560);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            status = new Label { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft,
                                 BackColor = Color.FromArgb(235, 238, 242) };
            Controls.Add(status);
            tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);
            tabs.BringToFront();

            BuildOverview();
            BuildClean();
            BuildStartup();
            BuildProcesses();
            BuildTweaks();

            tabs.SelectedIndexChanged += delegate
            {
                if (tabs.SelectedTab == prTab && plv.Items.Count == 0) LoadProcesses();
                else if (tabs.SelectedTab == clTab && clv.Items.Count == 0) Scan();
                else if (tabs.SelectedTab == twTab) UpdatePowerPlan();
            };

            timer = new Timer { Interval = 2000 };
            timer.Tick += delegate { if (tabs.SelectedTab == ovTab) UpdateOverview(); };
            Shown += delegate
            {
                LoadStartup();
                CpuPercent(); // prime the counter
                UpdateOverview();
                SetStatus("Ready.");
                timer.Start();
            };
            FormClosing += delegate { timer.Stop(); };
            if (Dpi > 1.01f) Scale(new SizeF(Dpi, Dpi)); // grow window + controls for 125%/150% displays
            ResumeLayout(false);
        }

        // ------------------------------------------------------------ helpers
        static string Fmt(double b)
        {
            if (b >= 1073741824) return (b / 1073741824).ToString("N2") + " GB";
            if (b >= 1048576) return (b / 1048576).ToString("N1") + " MB";
            if (b >= 1024) return (b / 1024).ToString("N0") + " KB";
            return b.ToString("N0") + " B";
        }

        TabPage NewTab(string name) { var t = new TabPage(name); tabs.TabPages.Add(t); return t; }

        Button Btn(Control p, string text, int x, int y, int w)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 32) };
            p.Controls.Add(b);
            return b;
        }

        Label Lbl(Control p, string text, int x, int y, int w, int h = 22, bool bold = false)
        {
            var l = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
            if (bold) l.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            p.Controls.Add(l);
            return l;
        }

        ListView LV(Control p, int x, int y, int w, int h, bool checks, params object[] cols)
        {
            var lv = new ListView { View = View.Details, FullRowSelect = true, CheckBoxes = checks,
                                    Location = new Point(x, y), Size = new Size(w, h), HideSelection = false };
            for (int i = 0; i < cols.Length; i += 2) lv.Columns.Add((string)cols[i], (int)((int)cols[i + 1] * Dpi));
            p.Controls.Add(lv);
            return lv;
        }

        ProgressBar Bar(Control p, int x, int y)
        {
            var b = new ProgressBar { Location = new Point(x, y), Size = new Size(470, 20) };
            p.Controls.Add(b);
            return b;
        }

        void SetStatus(string s) { status.Text = "  " + s; }
        bool Confirm(string s) { return MessageBox.Show(this, s, "PC Tune-Up", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; }
        void Info(string s) { MessageBox.Show(this, s, "PC Tune-Up", MessageBoxButtons.OK, MessageBoxIcon.Information); }

        static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Could not open " + target + ": " + ex.Message, "PC Tune-Up"); }
        }

        static int Run(string file, string args, out string output)
        {
            var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        static int Clamp(int v) { return Math.Max(0, Math.Min(100, v)); }

        // ------------------------------------------------------------ Overview
        void BuildOverview()
        {
            ovTab = NewTab("Overview");
            Lbl(ovTab, "How your PC is doing right now", 20, 15, 500, 24, true);
            cpuLabel = Lbl(ovTab, "CPU", 20, 55, 300); cpuBar = Bar(ovTab, 330, 55);
            ramLabel = Lbl(ovTab, "Memory", 20, 90, 300); ramBar = Bar(ovTab, 330, 90);
            diskLabel = Lbl(ovTab, "Disk", 20, 125, 300); diskBar = Bar(ovTab, 330, 125);
            infoLabel = Lbl(ovTab, "", 20, 160, 780, 40);
            Lbl(ovTab, "Suggestions", 20, 210, 300, 24, true);
            tipsBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                                    Location = new Point(20, 240), Size = new Size(780, 240), Font = new Font("Segoe UI", 10F),
                                    BackColor = SystemColors.Window };
            ovTab.Controls.Add(tipsBox);

            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    cpuName = ((string)k.GetValue("ProcessorNameString")).Trim();
            }
            catch { cpuName = "Unknown CPU"; }
        }

        int CpuPercent()
        {
            long idle, kernel, user;
            if (!Native.GetSystemTimes(out idle, out kernel, out user)) return 0;
            long di = idle - prevIdle, total = (kernel - prevKernel) + (user - prevUser);
            prevIdle = idle; prevKernel = kernel; prevUser = user;
            if (total <= 0) return 0;
            return Clamp((int)((total - di) * 100 / total));
        }

        void UpdateOverview()
        {
            try
            {
                int cpu = CpuPercent();
                var mem = new Native.MEMORYSTATUSEX();
                Native.GlobalMemoryStatusEx(mem);
                int ramPct = (int)mem.dwMemoryLoad;
                var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                int diskPct = (int)(100 * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize);
                int freePct = 100 - diskPct;
                var up = TimeSpan.FromMilliseconds(Native.GetTickCount64());
                string drv = drive.Name.TrimEnd('\\');

                cpuLabel.Text = "CPU in use: " + cpu + "%";
                cpuBar.Value = cpu;
                ramLabel.Text = string.Format("Memory in use: {0}%  ({1} of {2})", ramPct,
                    Fmt(mem.ullTotalPhys - mem.ullAvailPhys), Fmt(mem.ullTotalPhys));
                ramBar.Value = Clamp(ramPct);
                diskLabel.Text = string.Format("{0} drive: {1} free of {2}", drv, Fmt(drive.TotalFreeSpace), Fmt(drive.TotalSize));
                diskBar.Value = Clamp(diskPct);
                infoLabel.Text = string.Format("{0}\r\nLast restart: {1} days, {2} hours ago   |   Startup apps turned on: {3}",
                    cpuName, (int)up.TotalDays, up.Hours, startupCount);

                var tips = new List<string>();
                if (freePct < 15) tips.Add(string.Format("* Your {0} drive is almost full ({1}% free). Windows slows down when the system drive is nearly full. Use the Clean Up tab, and uninstall programs you no longer use.", drv, freePct));
                if (ramPct >= 85) tips.Add(string.Format("* Memory is {0}% used. Close programs and browser tabs you are not using - the Processes tab shows what is using the most.", ramPct));
                if (cpu >= 80) tips.Add(string.Format("* The CPU is very busy ({0}%). Check the Processes tab, or Task Manager, for something running in the background.", cpu));
                if (up.TotalDays >= 3) tips.Add(string.Format("* This PC has not restarted in {0} days. A restart clears memory and finishes pending updates. (Shut down does not fully reset Windows when Fast Startup is on - use Restart.)", (int)up.TotalDays));
                if (startupCount > 8) tips.Add(string.Format("* {0} apps start with Windows. Turning off ones you do not need makes boot and sign-in faster (Startup tab).", startupCount));
                if (tips.Count == 0) tips.Add("* Everything looks healthy right now.");
                tips.Add("Good habits: keep Windows and drivers updated, plug in your laptop for heavy work, and use \"Best performance\" power mode (Tweaks tab) when speed matters more than battery.");
                string text = string.Join("\r\n\r\n", tips);
                if (tipsBox.Text != text) tipsBox.Text = text;
            }
            catch (Exception ex) { SetStatus("Could not read system stats: " + ex.Message); }
        }

        // ------------------------------------------------------------ Clean Up
        void BuildClean()
        {
            clTab = NewTab("Clean Up");
            Lbl(clTab, "Free up disk space by removing files Windows and apps can safely recreate", 20, 15, 780, 24, true);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            targets = new List<CleanTarget> {
                new CleanTarget { Name = "Your temporary files", Path = Path.GetTempPath() },
                new CleanTarget { Name = "Windows temporary files", Path = Path.Combine(win, "Temp"), Admin = true },
                new CleanTarget { Name = "Windows Update download cache", Path = Path.Combine(win, @"SoftwareDistribution\Download"), Admin = true },
                new CleanTarget { Name = "App crash dumps", Path = Path.Combine(local, "CrashDumps") },
                new CleanTarget { Name = "Microsoft Edge cache", Path = Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache") },
                new CleanTarget { Name = "Google Chrome cache", Path = Path.Combine(local, @"Google\Chrome\User Data\Default\Cache") },
                new CleanTarget { Name = "Recycle Bin", Path = "", Recycle = true },
            };

            clv = LV(clTab, 20, 50, 790, 300, true, "What", 240, "Size", 100, "Location", 430);
            clv.ItemCheck += (s, e) =>
            {
                var t = (CleanTarget)clv.Items[e.Index].Tag;
                if (t == null || !t.Available) e.NewValue = CheckState.Unchecked;
            };

            Btn(clTab, "Scan", 20, 365, 120).Click += delegate { Scan(); };
            Btn(clTab, "Clean selected", 150, 365, 150).Click += delegate { Clean(); };
            Btn(clTab, "Windows Disk Cleanup", 450, 365, 175).Click += delegate { Open("cleanmgr.exe"); };
            Btn(clTab, "Storage settings", 635, 365, 175).Click += delegate { Open("ms-settings:storagesense"); };
            if (!IsAdmin)
            {
                Btn(clTab, "Restart as administrator", 20, 410, 200).Click += delegate { RestartAsAdmin(); };
                Lbl(clTab, "Run as administrator to also clean Windows temp files and the update cache.", 230, 417, 580);
            }
            Lbl(clTab, "Files that are in use are skipped automatically. Close your browser first to clear its cache.", 20, 455, 790);
        }

        static long FolderSize(string dir)
        {
            long s = 0;
            try
            {
                foreach (var f in new DirectoryInfo(dir).EnumerateFileSystemInfos())
                {
                    try
                    {
                        if ((f.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        var d = f as DirectoryInfo;
                        s += d != null ? FolderSize(d.FullName) : ((FileInfo)f).Length;
                    }
                    catch { }
                }
            }
            catch { }
            return s;
        }

        // Deletes the contents of a folder, never the folder itself.
        // Links and junctions are removed as links; their targets are never touched.
        static void DeleteContents(string dir)
        {
            FileSystemInfo[] items;
            try { items = new DirectoryInfo(dir).GetFileSystemInfos(); } catch { return; }
            foreach (var f in items)
            {
                try
                {
                    bool link = (f.Attributes & FileAttributes.ReparsePoint) != 0;
                    if (f is DirectoryInfo)
                    {
                        if (!link) DeleteContents(f.FullName);
                        Directory.Delete(f.FullName, false);
                    }
                    else
                    {
                        f.Attributes = FileAttributes.Normal;
                        File.Delete(f.FullName);
                    }
                }
                catch { }
            }
        }

        static long RecycleBinSize()
        {
            var info = new Native.SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(Native.SHQUERYRBINFO)) };
            return Native.SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
        }

        static long TargetSize(CleanTarget t) { return t.Recycle ? RecycleBinSize() : FolderSize(t.Path); }

        static bool SafeFolder(string p)
        {
            if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p)) return false;
            string full = Path.GetFullPath(p).TrimEnd('\\');
            return full.Length > 3 && full.Split('\\').Length >= 3; // never a drive root or top-level folder
        }

        void SetBusy(bool b)
        {
            busy = b;
            Cursor = b ? Cursors.WaitCursor : Cursors.Default;
            clTab.Enabled = !b;
        }

        async void Scan()
        {
            if (busy) return;
            SetBusy(true);
            clv.Items.Clear();
            long total = 0;
            foreach (var t in targets)
            {
                SetStatus("Scanning " + t.Name + "...");
                t.Available = false;
                t.Size = 0;
                if (!t.Recycle && !Directory.Exists(t.Path)) t.Reason = "Not found on this PC";
                else if (t.Admin && !IsAdmin) t.Reason = "Needs administrator";
                else
                {
                    var tt = t;
                    t.Size = await Task.Run(() => TargetSize(tt));
                    t.Available = true;
                    total += t.Size;
                }
                var it = new ListViewItem(t.Name) { Tag = t };
                it.SubItems.Add(t.Available ? Fmt(t.Size) : "-");
                it.SubItems.Add(t.Available ? (t.Recycle ? "All drives" : t.Path) : t.Reason);
                if (!t.Available) it.ForeColor = Color.Gray;
                it.Checked = t.Available && t.Size > 0 && !t.Recycle;
                clv.Items.Add(it);
            }
            SetBusy(false);
            SetStatus("Scan done. About " + Fmt(total) + " can be cleaned.");
        }

        async void Clean()
        {
            if (busy) return;
            var picked = clv.CheckedItems.Cast<ListViewItem>().Select(i => (CleanTarget)i.Tag).Where(t => t.Available).ToList();
            if (picked.Count == 0) { Info("Run a scan and tick at least one item to clean."); return; }
            string names = string.Join("\r\n", picked.Select(t => "  - " + t.Name));
            string warn = picked.Any(t => t.Recycle) ? "\r\n\r\nItems in the Recycle Bin will be gone for good." : "";
            if (!Confirm("Delete the contents of:\r\n" + names + warn + "\r\n\r\nContinue?")) return;

            SetBusy(true);
            long freed = 0;
            foreach (var t in picked)
            {
                SetStatus("Cleaning " + t.Name + "...");
                var tt = t;
                freed += await Task.Run(() =>
                {
                    long before = TargetSize(tt);
                    if (tt.Recycle) Native.SHEmptyRecycleBin(IntPtr.Zero, null, 7); // no confirm, no progress, no sound
                    else if (SafeFolder(tt.Path)) DeleteContents(tt.Path);
                    return Math.Max(0, before - TargetSize(tt));
                });
            }
            SetBusy(false);
            Scan();
            Info("Done! Freed " + Fmt(freed) + " of disk space.");
        }

        void RestartAsAdmin()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" });
                Close();
            }
            catch (Win32Exception) { /* user said no to the prompt */ }
        }

        // ------------------------------------------------------------ Startup
        void BuildStartup()
        {
            stTab = NewTab("Startup");
            Lbl(stTab, "Apps that start automatically when you sign in", 20, 15, 780, 24, true);
            slv = LV(stTab, 20, 50, 790, 330, false, "App", 200, "Status", 70, "Set by", 150, "Command", 350);
            Btn(stTab, "Turn on / off", 20, 395, 140).Click += delegate { ToggleStartup(); };
            Btn(stTab, "Refresh", 170, 395, 110).Click += delegate { LoadStartup(); };
            Btn(stTab, "Startup settings...", 290, 395, 170).Click += delegate { Open("ms-settings:startupapps"); };
            Btn(stTab, "Task Manager", 470, 395, 140).Click += delegate { Open("taskmgr.exe"); };
            slv.DoubleClick += delegate { ToggleStartup(); };
            Lbl(stTab, "Fewer startup apps = faster boot. Turning one off is safe and reversible: it just will not open by itself any more. Keep security software, touchpad/audio drivers and cloud sync you rely on.", 20, 440, 790, 50);
        }

        void AddRunKey(List<StartupItem> list, RegistryHive hive, RegistryView view, string approvedName, string where)
        {
            try
            {
                using (var b = RegistryKey.OpenBaseKey(hive, view))
                using (var k = b.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k == null) return;
                    foreach (var n in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(n)) continue;
                        list.Add(new StartupItem { Name = n, Command = Convert.ToString(k.GetValue(n)), Where = where, Hive = hive,
                                                   ApprovedKey = Approved + approvedName, ApprovedValue = n,
                                                   NeedsAdmin = hive == RegistryHive.LocalMachine });
                    }
                }
            }
            catch { }
        }

        void AddFolder(List<StartupItem> list, string folder, RegistryHive hive, string where)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var f in Directory.GetFiles(folder))
                {
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    list.Add(new StartupItem { Name = Path.GetFileNameWithoutExtension(f), Command = f, Where = where, Hive = hive,
                                               ApprovedKey = Approved + "StartupFolder", ApprovedValue = fn,
                                               NeedsAdmin = hive == RegistryHive.LocalMachine });
                }
            }
            catch { }
        }

        static bool ReadEnabled(StartupItem s)
        {
            try
            {
                using (var b = RegistryKey.OpenBaseKey(s.Hive, RegistryView.Registry64))
                using (var k = b.OpenSubKey(s.ApprovedKey))
                {
                    var v = k == null ? null : k.GetValue(s.ApprovedValue) as byte[];
                    return v == null || v.Length == 0 || (v[0] & 1) == 0; // 02 = on, 03 = off
                }
            }
            catch { return true; }
        }

        void LoadStartup()
        {
            var list = new List<StartupItem>();
            AddRunKey(list, RegistryHive.CurrentUser, RegistryView.Registry64, "Run", "You (registry)");
            AddRunKey(list, RegistryHive.LocalMachine, RegistryView.Registry64, "Run", "All users (registry)");
            if (Environment.Is64BitOperatingSystem)
                AddRunKey(list, RegistryHive.LocalMachine, RegistryView.Registry32, "Run32", "All users (32-bit)");
            AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, "You (Startup folder)");
            AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, "All users (Startup folder)");

            slv.BeginUpdate();
            slv.Items.Clear();
            foreach (var s in list)
            {
                s.Enabled = ReadEnabled(s);
                var it = new ListViewItem(s.Name) { Tag = s };
                it.SubItems.Add(s.Enabled ? "On" : "Off");
                it.SubItems.Add(s.Where);
                it.SubItems.Add(s.Command);
                if (!s.Enabled) it.ForeColor = Color.Gray;
                slv.Items.Add(it);
            }
            slv.EndUpdate();
            startupCount = list.Count(s => s.Enabled);
            SetStatus(string.Format("{0} startup apps, {1} turned on.", list.Count, startupCount));
        }

        // Uses the same switch as Settings > Apps > Startup, so it is fully reversible there too.
        void ToggleStartup()
        {
            if (slv.SelectedItems.Count == 0) { Info("Select an app in the list first."); return; }
            var s = (StartupItem)slv.SelectedItems[0].Tag;
            if (s.NeedsAdmin && !IsAdmin)
            {
                if (Confirm(s.Name + " is set up for all users, so changing it needs administrator rights.\r\n\r\nRestart PC Tune-Up as administrator?"))
                    RestartAsAdmin();
                return;
            }
            bool turnOn = !s.Enabled;
            if (!Confirm(string.Format("Turn {0} {1} at startup?", s.Name, turnOn ? "ON" : "OFF"))) return;
            try
            {
                var data = new byte[12];
                data[0] = (byte)(turnOn ? 2 : 3);
                if (!turnOn) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
                using (var b = RegistryKey.OpenBaseKey(s.Hive, RegistryView.Registry64))
                using (var k = b.CreateSubKey(s.ApprovedKey))
                    k.SetValue(s.ApprovedValue, data, RegistryValueKind.Binary);
                LoadStartup();
                UpdateOverview();
            }
            catch (Exception ex) { Info("Could not change " + s.Name + ": " + ex.Message); }
        }

        // ------------------------------------------------------------ Processes
        void BuildProcesses()
        {
            prTab = NewTab("Processes");
            Lbl(prTab, "Programs using the most memory", 20, 15, 780, 24, true);
            plv = LV(prTab, 20, 50, 790, 360, false, "Program", 260, "Memory", 120, "CPU time (s)", 120, "Process ID", 100);
            Btn(prTab, "Refresh", 20, 425, 120).Click += delegate { LoadProcesses(); };
            Btn(prTab, "Close selected program", 150, 425, 200).Click += delegate { EndProcess(); };
            Lbl(prTab, "Closing a program discards unsaved work in it. Windows system processes are protected.", 370, 425, 440, 40);
        }

        void LoadProcesses()
        {
            plv.BeginUpdate();
            plv.Items.Clear();
            foreach (var p in Process.GetProcesses().OrderByDescending(p => p.WorkingSet64).Take(30))
            {
                string cpu;
                try { cpu = p.TotalProcessorTime.TotalSeconds.ToString("N0"); } catch { cpu = "-"; }
                var it = new ListViewItem(p.ProcessName) { Tag = p.Id };
                it.SubItems.Add(Fmt(p.WorkingSet64));
                it.SubItems.Add(cpu);
                it.SubItems.Add(p.Id.ToString());
                plv.Items.Add(it);
                p.Dispose();
            }
            plv.EndUpdate();
        }

        void EndProcess()
        {
            if (plv.SelectedItems.Count == 0) { Info("Select a program in the list first."); return; }
            var sel = plv.SelectedItems[0];
            int id = (int)sel.Tag;
            if (ProtectedNames.Contains(sel.Text, StringComparer.OrdinalIgnoreCase) || id == Process.GetCurrentProcess().Id)
            {
                Info(sel.Text + " is part of Windows (or this app) and should not be closed.");
                return;
            }
            if (!Confirm(string.Format("Close {0} (PID {1})? Any unsaved work in it will be lost.", sel.Text, id))) return;
            try
            {
                using (var p = Process.GetProcessById(id)) { p.Kill(); p.WaitForExit(3000); }
                SetStatus("Closed " + sel.Text + ".");
            }
            catch (Exception ex) { Info("Could not close " + sel.Text + ": " + ex.Message); }
            LoadProcesses();
        }

        // ------------------------------------------------------------ Tweaks
        void BuildTweaks()
        {
            twTab = NewTab("Tweaks");
            Lbl(twTab, "Settings that make a real difference", 20, 15, 780, 24, true);
            Lbl(twTab, "Power", 20, 55, 300, 22, true);
            planLabel = Lbl(twTab, "", 20, 80, 780);
            Btn(twTab, "High performance plan", 20, 108, 190).Click += delegate { SetPlan("SCHEME_MIN"); };
            Btn(twTab, "Balanced plan", 220, 108, 150).Click += delegate { SetPlan("SCHEME_BALANCED"); };
            Btn(twTab, "Power mode settings...", 380, 108, 190).Click += delegate { Open("ms-settings:powersleep"); };
            Lbl(twTab, "On a laptop, higher performance uses more battery and runs warmer. On Windows 11, Settings > Power mode > \"Best performance\" is the main switch.", 20, 148, 790, 40);

            Lbl(twTab, "Windows", 20, 200, 300, 22, true);
            Btn(twTab, "Visual effects...", 20, 228, 190).Click += delegate { Open("SystemPropertiesPerformance.exe"); };
            Lbl(twTab, "Choose \"Adjust for best performance\" to turn off animations. Helps most on older PCs.", 220, 235, 590);
            Btn(twTab, "Optimize drives...", 20, 270, 190).Click += delegate { Open("dfrgui.exe"); };
            Lbl(twTab, "Runs TRIM on SSDs / defrag on hard drives. Windows usually does this weekly by itself.", 220, 277, 590);
            Btn(twTab, "Windows Update...", 20, 312, 190).Click += delegate { Open("ms-settings:windowsupdate"); };
            Lbl(twTab, "Updates include performance fixes and new drivers.", 220, 319, 590);
            Btn(twTab, "Virus scan...", 20, 354, 190).Click += delegate { Open("windowsdefender://threat"); };
            Lbl(twTab, "Malware is a common cause of a slow PC. Run a quick scan in Windows Security.", 220, 361, 590);
            Btn(twTab, "Restart PC now", 20, 410, 190).Click += delegate
            {
                if (Confirm("Restart the PC now? Make sure your work is saved."))
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
            };
            Lbl(twTab, "Save your work first.", 220, 417, 590);
        }

        void UpdatePowerPlan()
        {
            try
            {
                string o;
                Run("powercfg.exe", "/getactivescheme", out o);
                var m = Regex.Match(o, @"\(([^)]+)\)");
                planLabel.Text = "Current power plan: " + (m.Success ? m.Groups[1].Value : "Unknown");
            }
            catch { planLabel.Text = "Current power plan: Unknown"; }
        }

        void SetPlan(string scheme)
        {
            string o;
            int code = Run("powercfg.exe", "/setactive " + scheme, out o);
            if (code != 0)
            {
                Info("This PC does not have that power plan (normal for many modern laptops). Opening Power mode settings instead - choose \"Best performance\" there.");
                Open("ms-settings:powersleep");
            }
            UpdatePowerPlan();
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
