// Mouse Travel - a tiny tray app that turns your mouse movement into meters/kilometers.
// Written for the built-in .NET Framework 4.x compiler (C# 5), so no extra runtime is needed.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Native.MakeDpiAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length == 2 && args[0] == "--make-icon")
        {
            File.WriteAllBytes(args[1], Art.MakeIcoBytes());
            return 0;
        }
        if (args.Length == 1 && args[0] == "--demo-popups")
        {
            DemoPopups();
            return 0;
        }
        if (args.Length == 2 && args[0] == "--selftest")
        {
            SelfTest.Run(args[1]);
            return 0;
        }

        bool created;
        using (var mutex = new Mutex(true, "MouseTravel_SingleInstance", out created))
        {
            if (!created) return 0;
            Application.Run(new TrayApp());
        }
        return 0;
    }

    // Shows sample milestone pop-ups one after another, without touching any saved data.
    static void DemoPopups()
    {
        string[][] samples =
        {
            new[] { "500 m today!", "Your mouse just jogged ~5 football fields." },
            new[] { "1 km today!", "Your mouse officially went for a walk." },
            new[] { "5 km today!", "Timeline-scrubbing champion." },
            new[] { "5 km all-time.", "That's a parkrun!" },
        };
        int i = 0;
        Action next = null;
        next = delegate
        {
            if (i >= samples.Length) { Application.ExitThread(); return; }
            string[] s = samples[i++];
            Toast.Pop(s[0], s[1], null, next);
        };
        var invoker = new Control();
        invoker.CreateControl();
        invoker.BeginInvoke(next);
        Application.Run();
    }
}

// ---------------------------------------------------------------- helpers

static class Fun
{
    public static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    // Lump-sum formatting: "650 m", "1.4 km", "212 km"
    public static string Dist(double m)
    {
        if (m < 1000) return (Math.Round(m / 10) * 10).ToString("0", CI) + " m";
        double km = m / 1000;
        if (km < 100) return km.ToString("0.0", CI) + " km";
        return km.ToString("#,0", CI) + " km";
    }

    static readonly string[] CompareNames = {
        "a tennis court", "a football field", "the Eiffel Tower", "the Burj Khalifa",
        "the Golden Gate Bridge", "Mount Everest", "a half marathon", "a marathon" };
    static readonly double[] CompareLens = { 23.8, 105, 330, 828, 2737, 8849, 21097, 42195 };

    // "2.3× the Eiffel Tower", or null when there's barely any distance yet
    public static string Compare(double m)
    {
        for (int i = CompareLens.Length - 1; i >= 0; i--)
        {
            if (m >= CompareLens[i])
            {
                double x = m / CompareLens[i];
                return x.ToString(x < 10 ? "0.#" : "0", CI) + "\u00D7 " + CompareNames[i];
            }
        }
        return null;
    }

    public static string DayKey(DateTime d) { return d.ToString("yyyy-MM-dd", CI); }

    public static DateTime WeekStart(DateTime d)
    {
        int diff = ((int)d.DayOfWeek + 6) % 7; // Monday = start of week
        return d.Date.AddDays(-diff);
    }

    // Today's milestones: level 1 = 500 m, level 2 = 1 km, level 3 = 2 km, ...
    public static int Level(double m)
    {
        if (m < 500) return 0;
        if (m < 1000) return 1;
        return 1 + (int)Math.Floor(m / 1000);
    }

    public static double NextStop(double m)
    {
        if (m < 500) return 500;
        return (Math.Floor(m / 1000) + 1) * 1000;
    }

    public static double PrevStop(double m)
    {
        if (m < 500) return 0;
        if (m < 1000) return 500;
        return Math.Floor(m / 1000) * 1000;
    }

    static readonly string[] KmLines = {
        "{0} km today! Timeline-scrubbing champion.",
        "{0} km today! Your mouse wants a snack break.",
        "{0} km today! Somebody's on a deadline...",
        "{0} km today! Stretch those fingers, editor.",
        "{0} km today! That mouse pad is getting worn in.",
    };

    public static string MilestoneText(int level)
    {
        if (level == 1) return "500 m today! Your mouse just jogged ~5 football fields.";
        if (level == 2) return "1 km today! Your mouse officially went for a walk.";
        int km = level - 1;
        return string.Format(KmLines[km % KmLines.Length], km);
    }

    public static readonly double[] LifeStops = { 5000, 10000, 21097, 42195, 100000, 250000, 500000, 1000000 };
    public static readonly string[] LifeTexts = {
        "5 km all-time. That's a parkrun!",
        "10 km all-time. A proper 10K race!",
        "Half marathon distance, all-time!",
        "A full marathon! Your mouse deserves a medal.",
        "100 km all-time. Ultra-marathon mouse!",
        "250 km all-time. That's a road trip.",
        "500 km all-time. Seriously impressive.",
        "1,000 km all-time. Absolute legend.",
    };

    public static string MoonPercent(double m)
    {
        double pct = m / 384400000.0 * 100.0;
        return pct.ToString(pct < 0.01 ? "0.####" : "0.##", CI) + "%";
    }
}

static class Native
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(string device, uint devNum, ref DISPLAY_DEVICE dd, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr process);
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

    // True while a full-screen app, presentation or the lock screen is up: hold pop-ups until it ends.
    public static bool UserBusy()
    {
        int s;
        try { if (SHQueryUserNotificationState(out s) != 0) return false; }
        catch { return false; }
        return s == 1 || s == 2 || s == 3 || s == 4 || s == 7;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    public static void MakeDpiAware()
    {
        // Per-monitor aware, so cursor positions are real pixels on every screen.
        try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
        try { SetProcessDPIAware(); } catch { }
    }

    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint winIni);

    public static void RoundCorners(IntPtr hwnd)
    {
        try { int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, 4); } catch { }
    }

    public static void DarkTitleBar(IntPtr hwnd, bool dark)
    {
        try { int on = dark ? 1 : 0; DwmSetWindowAttribute(hwnd, 20, ref on, 4); } catch { }
    }

    // Settings > Accessibility > Visual effects > Animation effects
    public static bool ReducedMotion()
    {
        bool animate = true;
        try { SystemParametersInfo(0x1042, 0, ref animate, 0); } catch { }
        return !animate;
    }

    // Keep Task Manager happy: hand unused memory back to Windows.
    public static void TrimMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { EmptyWorkingSet(Process.GetCurrentProcess().Handle); } catch { }
    }
}

// ---------------------------------------------------------------- storage

class Store
{
    public readonly string Dir;
    readonly string dataPath, settingsPath;
    readonly Dictionary<string, double> days = new Dictionary<string, double>();
    readonly Dictionary<string, string> settings = new Dictionary<string, string>();

    public Store()
    {
        Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseTravel");
        dataPath = Path.Combine(Dir, "days.csv");
        settingsPath = Path.Combine(Dir, "settings.ini");
    }

    // A small diagnostics log (MouseTravel\log.txt), so a load or save problem leaves a trace.
    public void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, "log.txt");
            if (File.Exists(path) && new FileInfo(path).Length > 200 * 1024) File.Delete(path);
            File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Fun.CI) + "  " + msg + "\r\n");
        }
        catch { }
    }

    public void Load()
    {
        try
        {
            KeepAll(ReadCsv(dataPath));
            int before = days.Count;
            double totalBefore = Total();
            KeepAll(ReadHistory(HistoryPath)); // restores days missing (or lower) in days.csv
            if (days.Count > before || Total() > totalBefore + 1)
                Log("restored from the history file: " + (days.Count - before) + " new day(s), +" + (Total() - totalBefore).ToString("0", Fun.CI) + " m");
            if (File.Exists(settingsPath))
            {
                foreach (string line in File.ReadAllLines(settingsPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) settings[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
            if (File.Exists(dataPath)) lastWritten = File.ReadAllText(dataPath);
            Log("load: " + days.Count + " days, " + Total().ToString("0", Fun.CI) + " m (pid " + Process.GetCurrentProcess().Id + ")");
        }
        catch (Exception ex) { Log("load FAILED: " + ex); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            // Never write less than the files already hold: a day's distance only ever grows.
            var onDisk = ReadCsv(dataPath);
            if (lastWritten != null && File.Exists(dataPath) && File.ReadAllText(dataPath) != lastWritten)
                Log("WARNING days.csv changed outside this app: " + File.ReadAllText(dataPath).Replace("\n", " | "));
            KeepAll(onDisk);
            KeepAll(ReadHistory(HistoryPath)); // the history file is shared by every copy of the app, so take its highs too
            DailyBackup();

            var sb = new StringBuilder();
            foreach (string k in SortedDays()) sb.Append(k).Append(',').Append(days[k].ToString("0.###", Fun.CI)).Append('\n');
            WriteAtomic(dataPath, sb.ToString());
            lastWritten = sb.ToString();
            Log("saved: " + days.Count + " days, " + Total().ToString("0", Fun.CI) + " m (pid " + Process.GetCurrentProcess().Id + ")");

            sb.Length = 0;
            foreach (var kv in settings) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            WriteAtomic(settingsPath, sb.ToString());
        }
        catch (Exception ex) { Log("save FAILED: " + ex); }
        WriteHistory();
    }

    string lastWritten;

    // One copy of days.csv per day in MouseTravel\backup, keeping the last 7.
    void DailyBackup()
    {
        try
        {
            if (!File.Exists(dataPath)) return;
            string dir = Path.Combine(Dir, "backup");
            Directory.CreateDirectory(dir);
            string today = Path.Combine(dir, "days-" + Fun.DayKey(DateTime.Now) + ".csv");
            if (File.Exists(today)) return;
            File.Copy(dataPath, today);
            var old = new List<string>(Directory.GetFiles(dir, "days-*.csv"));
            old.Sort(StringComparer.Ordinal);
            for (int i = 0; i < old.Count - 7; i++) File.Delete(old[i]);
        }
        catch (Exception ex) { Log("backup FAILED: " + ex.Message); }
    }

    List<string> SortedDays()
    {
        var keys = new List<string>(days.Keys);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    void KeepAll(Dictionary<string, double> found)
    {
        foreach (var kv in found)
        {
            double cur;
            if (!days.TryGetValue(kv.Key, out cur) || kv.Value > cur) days[kv.Key] = kv.Value;
        }
    }

    static Dictionary<string, double> ReadCsv(string path)
    {
        var d = new Dictionary<string, double>();
        if (!File.Exists(path)) return d;
        foreach (string line in File.ReadAllLines(path))
        {
            string[] p = line.Split(',');
            double m;
            if (p.Length == 2 && double.TryParse(p[1], NumberStyles.Float, Fun.CI, out m)) d[p[0]] = m;
        }
        return d;
    }

    // ---- the human-readable history next to MouseTravel.exe (also a backup the app reads back)

    static string HistoryPath
    {
        get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Mouse Travel history.txt"); }
    }

    static readonly System.Text.RegularExpressions.Regex HistoryRow =
        new System.Text.RegularExpressions.Regex(@"^(\d{4}-\d{2}-\d{2})\s+\w{3}\s+([\d.]+) m\s*$");

    static Dictionary<string, double> ReadHistory(string path)
    {
        var d = new Dictionary<string, double>();
        try
        {
            if (!File.Exists(path)) return d;
            foreach (string line in File.ReadAllLines(path))
            {
                var m = HistoryRow.Match(line);
                double v;
                if (m.Success && double.TryParse(m.Groups[2].Value, NumberStyles.Float, Fun.CI, out v)) d[m.Groups[1].Value] = v;
            }
        }
        catch { }
        return d;
    }

    void WriteHistory()
    {
        try
        {
            DateTime now = DateTime.Now;
            double total = Total(), week = Range(Fun.WeekStart(now), now.Date.AddDays(1));
            var sb = new StringBuilder();
            sb.Append("MOUSE TRAVEL - distance history\r\n");
            sb.Append("Updated " + now.ToString("ddd d MMM yyyy, HH:mm", Fun.CI) + "\r\n\r\n");
            sb.Append("Lifetime    " + Fun.Dist(total) + "   (since " + FirstDay().ToString("d MMM yyyy", Fun.CI) + ")\r\n");
            sb.Append("This week   " + Fun.Dist(week) + "\r\n\r\n");
            sb.Append("Date        Day   Distance\r\n");
            foreach (string k in SortedDays())
            {
                DateTime d;
                string dow = DateTime.TryParseExact(k, "yyyy-MM-dd", Fun.CI, DateTimeStyles.None, out d) ? d.ToString("ddd", Fun.CI) : "   ";
                sb.Append(k + "  " + dow + "   " + days[k].ToString("0.###", Fun.CI) + " m\r\n"); // exact, so merging never drifts
            }
            sb.Append("\r\nThis file is updated automatically. Mouse Travel also reads it back as a backup.\r\n");
            WriteAtomic(HistoryPath, sb.ToString());
        }
        catch (Exception ex) { Log("history write FAILED: " + ex.Message); }
    }

    static void WriteAtomic(string path, string content)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        if (!File.Exists(path)) { File.Move(tmp, path); return; }
        try { File.Replace(tmp, path, null); }
        catch
        {
            // fallback when an atomic swap isn't possible (a stale temp file, a scanner holding the file)
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }
    }

    public double Get(DateTime day)
    {
        double m;
        return days.TryGetValue(Fun.DayKey(day), out m) ? m : 0;
    }

    public void Set(string dayKey, double meters) { days[dayKey] = meters; }

    public double Range(DateTime from, DateTime toExclusive)
    {
        double sum = 0;
        for (DateTime d = from.Date; d < toExclusive.Date; d = d.AddDays(1)) sum += Get(d);
        return sum;
    }

    // The first day anything was recorded (when the lifetime count started).
    public DateTime FirstDay()
    {
        DateTime first = DateTime.Now.Date, d;
        foreach (string k in days.Keys)
            if (DateTime.TryParseExact(k, "yyyy-MM-dd", Fun.CI, DateTimeStyles.None, out d) && d < first) first = d;
        return first;
    }

    public double Total()
    {
        double sum = 0;
        foreach (double m in days.Values) sum += m;
        return sum;
    }

    public string GetS(string key, string def)
    {
        string v;
        return settings.TryGetValue(key, out v) ? v : def;
    }

    public double GetD(string key, double def)
    {
        double d;
        return double.TryParse(GetS(key, ""), NumberStyles.Float, Fun.CI, out d) ? d : def;
    }

    public void SetS(string key, string value) { settings[key] = value; }
}

// ---------------------------------------------------------------- monitors

class MonitorInfo
{
    public string Key, Name;
    public Rectangle Bounds;
    public double AutoInches;   // 0 when Windows couldn't tell us
    public double Inches;       // what we actually use
    public double MmPerPx;

    public void Recalc()
    {
        double diagPx = Math.Sqrt((double)Bounds.Width * Bounds.Width + (double)Bounds.Height * Bounds.Height);
        MmPerPx = diagPx > 0 ? Inches * 25.4 / diagPx : 0.27;
    }
}

static class Monitors
{
    public const double DefaultInches = 24;

    public static List<MonitorInfo> Detect(Store store)
    {
        // Physical sizes (cm) and names straight from each monitor's EDID, keyed by WMI instance name.
        var sizes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var q = new ManagementObjectSearcher(@"root\wmi",
                "SELECT InstanceName, MaxHorizontalImageSize, MaxVerticalImageSize FROM WmiMonitorBasicDisplayParams"))
            {
                foreach (ManagementObject mo in q.Get())
                {
                    double h = Convert.ToDouble(mo["MaxHorizontalImageSize"]);
                    double v = Convert.ToDouble(mo["MaxVerticalImageSize"]);
                    if (h > 0 && v > 0) sizes[(string)mo["InstanceName"]] = Math.Sqrt(h * h + v * v) / 2.54;
                }
            }
        }
        catch { }
        try
        {
            using (var q = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID"))
            {
                foreach (ManagementObject mo in q.Get())
                {
                    var raw = mo["UserFriendlyName"] as ushort[];
                    if (raw == null) continue;
                    var sb = new StringBuilder();
                    foreach (ushort c in raw) if (c != 0) sb.Append((char)c);
                    if (sb.Length > 0) names[(string)mo["InstanceName"]] = sb.ToString().Trim();
                }
            }
        }
        catch { }

        var list = new List<MonitorInfo>();
        int n = 1;
        foreach (Screen sc in Screen.AllScreens)
        {
            var mi = new MonitorInfo { Bounds = sc.Bounds, Name = "Monitor " + n++, Key = sc.DeviceName };
            var dd = new Native.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(dd);
            if (Native.EnumDisplayDevices(sc.DeviceName, 0, ref dd, 1) && !string.IsNullOrEmpty(dd.DeviceID))
            {
                mi.Key = dd.DeviceID;
                string prefix = WmiPrefix(dd.DeviceID);
                if (prefix != null)
                {
                    foreach (var kv in sizes) if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) mi.AutoInches = kv.Value;
                    foreach (var kv in names) if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) mi.Name += " \u00B7 " + kv.Value;
                }
            }
            list.Add(mi);
        }
        // Single screen that we couldn't match by ID: use the only size Windows reported.
        if (list.Count == 1 && list[0].AutoInches == 0 && sizes.Count == 1)
            foreach (double v in sizes.Values) list[0].AutoInches = v;

        foreach (var mi in list)
        {
            double saved = store.GetD("monitor." + mi.Key, 0);
            if (saved > 0) mi.Inches = saved;
            else if (mi.AutoInches > 0) mi.Inches = Math.Round(mi.AutoInches * 2) / 2;
            else mi.Inches = DefaultInches;
            mi.Recalc();
        }
        return list;
    }

    // \\?\DISPLAY#GSM5B7F#5&abc&0&UID4352#{guid}  ->  DISPLAY\GSM5B7F\5&abc&0&UID4352
    static string WmiPrefix(string interfaceId)
    {
        string s = interfaceId.StartsWith(@"\\?\") ? interfaceId.Substring(4) : interfaceId;
        string[] p = s.Split('#');
        return p.Length < 3 ? null : p[0] + "\\" + p[1] + "\\" + p[2];
    }

    public static bool HasSaved(Store store, MonitorInfo mi) { return store.GetD("monitor." + mi.Key, 0) > 0; }
}

// ---------------------------------------------------------------- the tray app

class Stats
{
    public double Today, Week, AllTime;
    public DateTime Since = DateTime.Now.Date;
}

class TrayApp : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly Control invoker = new Control();
    readonly Store store = new Store();
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer pollTimer, houseTimer;
    MonitorInfo[] monitors = new MonitorInfo[0];

    string todayKey;
    double todayMeters, allTime;
    int todayLevel, lifeLevel, houseTicks;
    bool dirty, hasLast, setupOpen;
    Native.POINT last;
    Popup popup;
    DateTime popupClosedAt;
    double lastShownKm;
    string lastShownDay;
    readonly List<object[]> queue = new List<object[]>(); // pending pop-ups: title, text, click action
    ToolStripMenuItem startupItem;

    public TrayApp()
    {
        invoker.CreateControl(); // installs the UI sync context so system events land on this thread
        RepairStartup();
        store.Load();
        allTime = store.Total();
        RollDay();
        lifeLevel = (int)store.GetD("ms.life", 0);
        LoadMonitors();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Mouse Travel", null, delegate { ShowPopup(); });
        menu.Items.Add("Share this week", null, delegate { ShareWeek(Fun.WeekStart(DateTime.Now), true); });
        menu.Items.Add("Share last week", null, delegate { ShareWeek(Fun.WeekStart(DateTime.Now).AddDays(-7), false); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Screens…", null, delegate { ShowSetup(false); });
        startupItem = new ToolStripMenuItem("Start with Windows", null, delegate { SetStartup(!IsStartup()); });
        menu.Items.Add(startupItem);
        menu.Items.Add("Open data folder", null, delegate { try { Process.Start(store.Dir); } catch { } });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, delegate { Quit(); });
        menu.Opening += delegate { startupItem.Checked = IsStartup(); };

        tray = new NotifyIcon { Icon = Art.MakeTrayIcon(), ContextMenuStrip = menu, Visible = true, Text = "Mouse Travel" };
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowPopup(); };

        pollTimer = new System.Windows.Forms.Timer { Interval = 40 };
        pollTimer.Tick += Poll;
        pollTimer.Start();

        houseTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        houseTimer.Tick += delegate { Housekeep(); };
        houseTimer.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.SessionEnding += OnSessionEnding;

        UpdateTooltip();
        invoker.BeginInvoke(new Action(delegate
        {
            if (store.GetS("setup.done", "") != "1") FirstRun();
            else CheckWeekly();
            Flush(); // writes the history file right away on every start
            store.Log("started: today " + todayKey + " = " + todayMeters.ToString("0", Fun.CI) + " m, lifetime " + allTime.ToString("0", Fun.CI) + " m");
            Native.TrimMemory();
        }));
    }

    void LoadMonitors()
    {
        monitors = Monitors.Detect(store).ToArray();
        hasLast = false;
    }

    // --- tracking: sample the cursor 25x a second; cheap and needs no global hooks
    void Poll(object sender, EventArgs e)
    {
        Native.POINT p;
        if (!Native.GetCursorPos(out p)) { hasLast = false; return; }
        if (!hasLast) { last = p; hasLast = true; return; }
        int dx = p.X - last.X, dy = p.Y - last.Y;
        if (dx == 0 && dy == 0) return;
        last = p;

        double px = Math.Sqrt((double)dx * dx + (double)dy * dy);
        double meters = px * MmPerPxAt(p.X, p.Y) / 1000.0;
        todayMeters += meters;
        allTime += meters;
        store.Set(todayKey, todayMeters);
        dirty = true;

        int level = Fun.Level(todayMeters);
        if (level > todayLevel)
        {
            todayLevel = level;
            store.SetS("ms.day", todayKey);
            store.SetS("ms.level", level.ToString(Fun.CI));
            NotifyMilestone(Fun.MilestoneText(level));
        }
        if (lifeLevel < Fun.LifeStops.Length && allTime >= Fun.LifeStops[lifeLevel])
        {
            while (lifeLevel < Fun.LifeStops.Length && allTime >= Fun.LifeStops[lifeLevel]) lifeLevel++;
            store.SetS("ms.life", lifeLevel.ToString(Fun.CI));
            NotifyMilestone(Fun.LifeTexts[lifeLevel - 1]);
        }
    }

    double MmPerPxAt(int x, int y)
    {
        var ms = monitors;
        for (int i = 0; i < ms.Length; i++) if (ms[i].Bounds.Contains(x, y)) return ms[i].MmPerPx;
        return ms.Length > 0 ? ms[0].MmPerPx : 0.27;
    }

    void RollDay()
    {
        string k = Fun.DayKey(DateTime.Now);
        if (k == todayKey) return;
        todayKey = k;
        todayMeters = store.Get(DateTime.Now);
        todayLevel = store.GetS("ms.day", "") == k ? (int)store.GetD("ms.level", 0) : 0;
    }

    void Housekeep()
    {
        RollDay();
        UpdateTooltip();
        CheckWeekly();
        FlushNotification(); // shows a pop-up held back during full-screen use
        if (++houseTicks % 12 == 0 && dirty) Flush(); // save once a minute
    }

    void Flush()
    {
        store.Save();
        dirty = false;
        // the save merges in anything already on disk; pick that up so totals never go backwards
        todayMeters = Math.Max(todayMeters, store.Get(DateTime.Now));
        allTime = Math.Max(allTime, store.Total());
    }

    void UpdateTooltip()
    {
        if (tray != null) tray.Text = "Mouse Travel \u00B7 today " + Fun.Dist(todayMeters) + " \u00B7 lifetime " + Fun.Dist(allTime);
    }

    Stats GetStats()
    {
        return new Stats
        {
            Today = todayMeters, Week = store.Range(Fun.WeekStart(DateTime.Now), DateTime.Now.Date.AddDays(1)),
            AllTime = allTime, Since = store.FirstDay(),
        };
    }

    void CheckWeekly()
    {
        DateTime ws = Fun.WeekStart(DateTime.Now);
        string wk = Fun.DayKey(ws);
        if (store.GetS("week.shown", "") == wk) return;
        store.SetS("week.shown", wk);
        dirty = true;
        DateTime lastWs = ws.AddDays(-7);
        double lw = store.Range(lastWs, ws);
        if (lw < 50) return;
        string cmp = Fun.Compare(lw);
        Notify("Last week: " + Fun.Dist(lw) + " of mousing!",
            (cmp != null ? "That's about " + cmp + ". " : "") + "Click here to make your share card.",
            delegate { ShareWeek(lastWs, false); });
    }

    // Our own pop-up (Windows notifications may be switched off). Queued: one at a time, held while busy.
    void Notify(string title, string text, Action onClick)
    {
        queue.Add(new object[] { title, text, onClick });
        if (queue.Count > 5) queue.RemoveAt(0);
        FlushNotification();
    }

    // "1 km today! Your mouse went for a walk." -> headline "1 km today!" + the fun line
    void NotifyMilestone(string text)
    {
        int cut = text.IndexOfAny(new[] { '!', '.' });
        if (cut > 0 && cut < text.Length - 1) Notify(text.Substring(0, cut + 1), text.Substring(cut + 1).Trim(), null);
        else Notify(text, "", null);
    }

    void FlushNotification()
    {
        if (queue.Count == 0 || Toast.Showing || Native.UserBusy()) return;
        object[] n = queue[0];
        queue.RemoveAt(0);
        Toast.Pop((string)n[0], (string)n[1], (Action)n[2], FlushNotification);
    }

    // --- UI
    void ShowPopup()
    {
        if (popup != null && !popup.IsDisposed) { popup.Close(); return; }
        if ((DateTime.Now - popupClosedAt).TotalMilliseconds < 300) return; // the click that closed it
        // The odometer rolls up from wherever you last looked today.
        var p = new Popup(GetStats, lastShownDay == todayKey ? lastShownKm : 0);
        p.ShareClicked += delegate { p.Close(); ShareWeek(Fun.WeekStart(DateTime.Now), true); };
        p.SetupClicked += delegate { p.Close(); ShowSetup(false); };
        p.FormClosed += delegate
        {
            lastShownKm = p.ShownKm;
            lastShownDay = todayKey;
            popupClosedAt = DateTime.Now;
            popup = null;
            Native.TrimMemory();
        };
        popup = p;
        p.ShowNearTray();
    }

    // First run: if Windows told us every screen's size, just start counting and say so.
    void FirstRun()
    {
        var sizes = new List<string>();
        foreach (var mi in monitors)
        {
            if (mi.AutoInches <= 0) { ShowSetup(true); return; }
            sizes.Add(mi.Inches.ToString("0.#", Fun.CI) + "″");
        }
        SaveMonitors();
        store.SetS("setup.done", "1");
        Flush();
        Notify("Mouse Travel is on the road 🚗",
            "Found your screens (" + string.Join(" + ", sizes.ToArray()) + "). Counting starts now. Click here if the sizes look off.",
            delegate { ShowSetup(false); });
    }

    void SaveMonitors()
    {
        foreach (var mi in monitors) store.SetS("monitor." + mi.Key, mi.Inches.ToString("0.#", Fun.CI));
    }

    void ShowSetup(bool firstRun)
    {
        if (setupOpen) return;
        setupOpen = true;
        try
        {
            using (var f = new SetupForm(monitors, IsStartup(), firstRun))
            {
                if (f.ShowDialog() == DialogResult.OK)
                {
                    for (int i = 0; i < monitors.Length; i++)
                    {
                        monitors[i].Inches = f.Inches[i];
                        monitors[i].Recalc();
                    }
                    SetStartup(f.StartWithWindows);
                }
                SaveMonitors();
                store.SetS("setup.done", "1");
                Flush();
                if (firstRun) Notify("You're all set 🚗", "I live down here in the tray. Click the little mouse anytime to check today's trip.", null);
            }
        }
        finally { setupOpen = false; }
    }

    void ShareWeek(DateTime weekStart, bool thisWeek)
    {
        var days = new double[7];
        for (int i = 0; i < 7; i++) days[i] = store.Get(weekStart.AddDays(i));
        double total = 0;
        foreach (double d in days) total += d;
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Mouse Travel");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mouse-travel-week-" + Fun.DayKey(weekStart) + ".png");
            using (Bitmap card = Art.MakeCard(days, weekStart, allTime, thisWeek))
            {
                card.Save(path, ImageFormat.Png);
                var data = new DataObject();
                data.SetImage(card);
                data.SetText((thisWeek ? "This week" : "Last week") + " I drove my mouse " + Fun.Dist(total) + "! \uD83D\uDDB1\uFE0F #MouseTravel");
                try { Clipboard.SetDataObject(data, true); } catch { }
            }
            try { Process.Start(path); } catch { }
            Notify("Your share card is ready", "Saved to Pictures\\Mouse Travel and copied. Just paste it anywhere.", null);
        }
        catch (Exception ex)
        {
            Notify("Couldn't make the card", ex.Message, null);
        }
    }

    // --- system
    // Start with Windows = a shortcut in the user's Startup folder. (Some PCs skip HKCU\...\Run
    // entries even when approved; the Startup folder is launched reliably.) Task Manager's
    // Startup apps toggle for it lives under StartupApproved\StartupFolder: odd first byte = disabled.
    const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string ApprovedFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    const string ShortcutName = "Mouse Travel.lnk";

    static string ShortcutPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName); } }

    static bool Disabled(string approvedKey, string name)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(approvedKey))
            {
                var a = k == null ? null : k.GetValue(name) as byte[];
                return a != null && a.Length > 0 && (a[0] & 1) == 1;
            }
        }
        catch { return false; }
    }

    static void DeleteValue(string key, string name)
    {
        try { using (var k = Registry.CurrentUser.OpenSubKey(key, true)) if (k != null) k.DeleteValue(name, false); }
        catch { }
    }

    static bool IsStartup()
    {
        return File.Exists(ShortcutPath) && !Disabled(ApprovedFolderKey, ShortcutName);
    }

    static void SetStartup(bool on)
    {
        try
        {
            if (on)
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { Application.ExecutablePath });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(Application.ExecutablePath) });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Mouse Travel" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
                Marshal.FinalReleaseComObject(lnk);
                Marshal.FinalReleaseComObject(shell);
            }
            else if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            DeleteValue(ApprovedFolderKey, ShortcutName);
        }
        catch { }
    }

    // Move older installs from the Run key to the Startup folder (unless switched off in Task Manager),
    // and keep the shortcut pointing at this .exe if it was moved.
    static void RepairStartup()
    {
        try
        {
            bool hadRun;
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) hadRun = k != null && k.GetValue("MouseTravel") != null;
            if (hadRun)
            {
                bool wasOff = Disabled(ApprovedRunKey, "MouseTravel");
                DeleteValue(RunKey, "MouseTravel");
                DeleteValue(ApprovedRunKey, "MouseTravel");
                if (!wasOff) SetStartup(true);
            }
            else if (File.Exists(ShortcutPath) && !Disabled(ApprovedFolderKey, ShortcutName))
            {
                SetStartup(true); // cheap, and fixes a stale target path
            }
        }
        catch { }
    }

    void OnDisplayChanged(object sender, EventArgs e)
    {
        invoker.BeginInvoke(new Action(delegate
        {
            LoadMonitors();
            foreach (var mi in monitors)
            {
                if (mi.AutoInches == 0 && !Monitors.HasSaved(store, mi)) { ShowSetup(false); break; }
            }
        }));
    }

    void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        Flush();
        store.Log("saved at Windows " + e.Reason.ToString().ToLowerInvariant());
    }

    void Quit()
    {
        Flush();
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.SessionEnding -= OnSessionEnding;
        pollTimer.Stop();
        houseTimer.Stop();
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }
}

// ---------------------------------------------------------------- look & feel

// Prefer Windows 11's Segoe UI Variable; it renders reliably in both GDI and GDI+.
static class Fonts
{
    static string text, display;

    static string Find(string name)
    {
        foreach (FontFamily f in FontFamily.Families) if (f.Name == name) return name;
        return "Segoe UI";
    }

    public static string Family { get { if (text == null) text = Find("Segoe UI Variable Text"); return text; } }
    static string DisplayFamily { get { if (display == null) display = Find("Segoe UI Variable Display"); return display; } }

    public static Font Ui(float points, FontStyle style) { return new Font(Family, points, style); }
    public static Font Px(float pixels, FontStyle style) { return new Font(Family, pixels, style, GraphicsUnit.Pixel); }
    public static Font DisplayPx(float pixels, FontStyle style) { return new Font(DisplayFamily, pixels, style, GraphicsUnit.Pixel); }
}

// Colors follow the Windows light/dark setting (and High Contrast). The odometer is a physical
// object, so it stays dark in both. Contrast, worst case per palette:
//   dark:  secondary #A1A1A6 on raised #222222 = 6.2:1, ink on road-yellow = 11.8:1
//   light: secondary #6B6B70 on white = 5.3:1, accent text #8A5A00 on #F6F6F4 = 5.5:1
// Neutral grays with one accent: the yellow of a road's lane markings. No gradients.
class Palette
{
    public bool Dark;
    public Color Surface, Raised, Content, Secondary, Hairline, Accent, AccentText, OnAccent, Signal, Road, RoadLine, TrackOff, Bezel;
    public static bool? ForceDark; // self-test only

    public static Palette Current()
    {
        if (SystemInformation.HighContrast && !ForceDark.HasValue) return HighContrast();
        bool dark = ForceDark.HasValue ? ForceDark.Value : !SystemLight();
        return dark ? MakeDark() : MakeLight();
    }

    static bool SystemLight()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                return v == null || !(v is int) || (int)v != 0;
            }
        }
        catch { return true; }
    }

    public static Color Hex(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

    static Palette MakeDark()
    {
        return new Palette
        {
            Dark = true, Surface = Hex(0x161616), Raised = Hex(0x222222), Content = Hex(0xF5F5F3), Secondary = Hex(0xA1A1A6),
            Hairline = Hex(0x2E2E30), Accent = Hex(0xFFC93C), AccentText = Hex(0xFFC93C), OnAccent = Hex(0x161616),
            Signal = Hex(0xFFC93C), Road = Hex(0x262626), RoadLine = Hex(0x5A5A5E), TrackOff = Hex(0x636366), Bezel = Hex(0x0A0A0A),
        };
    }

    static Palette MakeLight()
    {
        return new Palette
        {
            Dark = false, Surface = Hex(0xF6F6F4), Raised = Hex(0xFFFFFF), Content = Hex(0x161616), Secondary = Hex(0x6B6B70),
            Hairline = Hex(0xE4E4E1), Accent = Hex(0xFFC93C), AccentText = Hex(0x8A5A00), OnAccent = Hex(0x161616),
            Signal = Hex(0x8A5A00), Road = Hex(0xEAEAE7), RoadLine = Hex(0xB0B0B3), TrackOff = Hex(0x8E8E93), Bezel = Hex(0x222222),
        };
    }

    static Palette HighContrast()
    {
        return new Palette
        {
            Dark = SystemColors.Window.GetBrightness() < 0.5f, Surface = SystemColors.Window, Raised = SystemColors.Window,
            Content = SystemColors.WindowText, Secondary = SystemColors.WindowText, Hairline = SystemColors.WindowText,
            Accent = SystemColors.Highlight, AccentText = SystemColors.WindowText, OnAccent = SystemColors.HighlightText,
            Signal = SystemColors.Highlight, Road = SystemColors.Window, RoadLine = SystemColors.WindowText,
            TrackOff = SystemColors.GrayText, Bezel = SystemColors.WindowText,
        };
    }

    // amount > 0 mixes toward white, < 0 toward black
    public static Color Shade(Color c, float amount)
    {
        Color t = amount >= 0 ? Color.White : Color.Black;
        float a = Math.Abs(amount);
        return Color.FromArgb(c.A, (int)(c.R + (t.R - c.R) * a), (int)(c.G + (t.G - c.G) * a), (int)(c.B + (t.B - c.B) * a));
    }
}

// A real Button (keyboard, Enter/Esc, screen readers) with a rounded look, hover/press states and a focus ring.
class PillButton : Button
{
    readonly Palette pal;
    readonly bool primary;
    bool hover, down;

    public PillButton(Palette pal, bool primary, float points)
    {
        this.pal = pal;
        this.primary = primary;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Font = Fonts.Ui(points, FontStyle.Bold);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : pal.Surface);

        Color fill = primary ? pal.Accent : pal.Raised;
        if (down) fill = Palette.Shade(fill, -0.14f);
        else if (hover) fill = Palette.Shade(fill, pal.Dark ? 0.08f : -0.06f);

        var body = new RectangleF(3, 3, Width - 6, Height - 6);
        float radius = Math.Min(body.Height / 2, Height * 0.32f);
        using (var path = Art.Round(body, radius))
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (!primary) using (var p = new Pen(pal.Hairline)) g.DrawPath(p, path);
        }
        if (Focused && ShowFocusCues)
            using (var path = Art.Round(new RectangleF(1, 1, Width - 2, Height - 2), radius + 2))
            using (var p = new Pen(pal.Content, 2f)) g.DrawPath(p, path);

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, primary ? pal.OnAccent : pal.Content,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

// A CheckBox drawn as a switch.
class Toggle : CheckBox
{
    readonly Palette pal;
    readonly float sc;

    public Toggle(Palette pal, float sc)
    {
        this.pal = pal;
        this.sc = sc;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Cursor = Cursors.Hand;
        Font = Fonts.Ui(10f, FontStyle.Regular);
    }

    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : pal.Surface);

        float tw = 40 * sc, th = 22 * sc;
        var track = new RectangleF(3 * sc, (Height - th) / 2, tw, th);
        using (var path = Art.Round(track, th / 2))
        {
            using (var b = new SolidBrush(Checked ? pal.Accent : pal.TrackOff)) g.FillPath(b, path);
            if (Checked && !pal.Dark) using (var p = new Pen(pal.AccentText)) g.DrawPath(p, path); // yellow alone is too faint on light gray
        }
        float d = th - 6 * sc;
        float kx = Checked ? track.Right - 3 * sc - d : track.X + 3 * sc;
        using (var b = new SolidBrush(Checked ? pal.OnAccent : Color.White)) g.FillEllipse(b, kx, track.Y + 3 * sc, d, d);
        if (Focused && ShowFocusCues)
        {
            var ring = RectangleF.Inflate(track, 2 * sc, 2 * sc);
            using (var path = Art.Round(ring, ring.Height / 2)) using (var p = new Pen(pal.Content, 2f)) g.DrawPath(p, path);
        }
        int tx = (int)(track.Right + 12 * sc);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, 0, Width - tx, Height), pal.Content,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

// ---------------------------------------------------------------- popup

class Popup : Form
{
    public event EventHandler ShareClicked, SetupClicked;
    public double ShownKm { get { return shownKm; } }

    const double RollMs = 900, FadeMs = 160;
    readonly Func<Stats> stats;
    readonly Palette pal;
    readonly float sc;
    readonly bool animate;
    readonly System.Windows.Forms.Timer timer;
    double fromKm, shownKm;
    DateTime opened;
    int restTop;
    bool settled;

    public Popup(Func<Stats> stats, double startKm)
    {
        this.stats = stats;
        pal = Palette.Current();
        animate = !Native.ReducedMotion();
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = pal.Surface;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        using (Graphics g = CreateGraphics()) sc = g.DpiX / 96f;
        ClientSize = new Size(S(320), S(404));

        double now = stats().Today / 1000.0;
        fromKm = animate ? Math.Min(startKm, now) : now;
        shownKm = fromKm;

        var share = new PillButton(pal, true, 10f) { Text = "Share my week", Bounds = new Rectangle(S(13), S(348), S(186), S(44)) };
        share.Click += delegate { if (ShareClicked != null) ShareClicked(this, EventArgs.Empty); };
        var screens = new PillButton(pal, false, 10f) { Text = "Screens", Bounds = new Rectangle(S(201), S(348), S(106), S(44)) };
        screens.Click += delegate { if (SetupClicked != null) SetupClicked(this, EventArgs.Empty); };
        Controls.Add(share);
        Controls.Add(screens);

        timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += delegate { Step(); };
        Deactivate += delegate { Close(); };
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); else SkipAnimation(); };
        MouseDown += delegate { SkipAnimation(); };
    }

    int S(float v) { return (int)Math.Round(v * sc); }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // drop shadow
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        timer.Dispose();
        base.OnFormClosed(e);
    }

    public void ShowNearTray()
    {
        Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        restTop = wa.Bottom - Height - S(12);
        Location = new Point(wa.Right - Width - S(12), restTop + (animate ? S(10) : 0));
        if (animate) Opacity = 0;
        opened = DateTime.Now;
        Show();
        Activate();
        timer.Start();
    }

    // Any click or key ends the intro right away.
    void SkipAnimation() { opened = DateTime.MinValue; }

    void Step()
    {
        double target = stats().Today / 1000.0;
        double t = (DateTime.Now - opened).TotalMilliseconds;
        if (!settled && animate && t < RollMs)
        {
            shownKm = fromKm + (target - fromKm) * (1 - Math.Pow(1 - t / RollMs, 3));
            double a = Math.Min(1, t / FadeMs);
            Opacity = a;
            Top = restTop + (int)Math.Round(S(10) * Math.Pow(1 - a, 3));
        }
        else
        {
            if (!settled)
            {
                settled = true;
                Opacity = 1;
                Top = restTop;
                timer.Interval = 100;
            }
            shownKm += (target - shownKm) * 0.3; // the wheels keep creeping while you move
            if (Math.Abs(target - shownKm) < 0.0005) shownKm = target;
        }
        Invalidate(new Rectangle(0, S(50), Width, S(290)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Stats st = stats();
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (var pen = new Pen(pal.Hairline)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        using (var title = Fonts.Ui(13f, FontStyle.Bold))
        using (var date = Fonts.Ui(9f, FontStyle.Regular))
        using (var body = Fonts.Ui(10f, FontStyle.Regular))
        using (var small = Fonts.Ui(8.5f, FontStyle.Bold))
        using (var unit = Fonts.Ui(15f, FontStyle.Bold))
        using (var tile = Fonts.Ui(15f, FontStyle.Bold))
        using (var caption = Fonts.Ui(8.5f, FontStyle.Regular))
        {
            TextRenderer.DrawText(g, "Today's trip", title, new Point(S(13), S(14)), pal.Content);
            TextRenderer.DrawText(g, DateTime.Now.ToString("ddd d MMM", Fun.CI), date,
                new Rectangle(S(150), S(20), S(154), S(20)), pal.Secondary, TextFormatFlags.Right);

            // the signature: a rolling odometer
            float ww = S(40), wh = S(56);
            SizeF odo = Art.OdometerSize(ww, wh, 3);
            Art.DrawOdometer(g, new PointF(S(16), S(54)), ww, wh, 3, shownKm);
            TextRenderer.DrawText(g, "km", unit, new Point((int)(S(16) + odo.Width + S(8)), (int)(S(54) + odo.Height - S(34))), pal.Secondary);

            string cmp = Fun.Compare(st.Today);
            TextRenderer.DrawText(g, cmp != null ? "≈ " + cmp : "Engine's warm. Give it a wiggle!", body, new Point(S(15), S(136)), pal.Content);

            // the road to the next milestone
            double prev = Fun.PrevStop(st.Today), next = Fun.NextStop(st.Today);
            float frac = (float)Math.Max(0, Math.Min(1, (st.Today - prev) / (next - prev)));
            DrawRoad(g, new RectangleF(S(16), S(170), S(288), S(26)), frac);
            TextRenderer.DrawText(g, "Next stop " + Fun.Dist(next), small, new Point(S(15), S(204)), pal.Secondary);
            TextRenderer.DrawText(g, Fun.Dist(next - st.Today) + " to go", small,
                new Rectangle(S(150), S(204), S(154), S(18)), pal.Secondary, TextFormatFlags.Right);

            DrawTile(g, new RectangleF(S(16), S(238), S(140), S(94)), "THIS WEEK", Fun.Dist(st.Week), "since Monday", small, tile, caption);
            DrawTile(g, new RectangleF(S(164), S(238), S(140), S(94)), "LIFETIME", Fun.Dist(st.AllTime),
                "since " + st.Since.ToString("d MMM yyyy", Fun.CI), small, tile, caption);
        }
    }

    void DrawRoad(Graphics g, RectangleF r, float frac)
    {
        float rad = r.Height / 2;
        using (var path = Art.Round(r, rad)) using (var b = new SolidBrush(pal.Road)) g.FillPath(b, path);
        float carX = r.X + r.Height * 0.6f + (r.Width - r.Height * 1.2f) * frac;
        // a solid lane line behind the mouse (distance driven), dashes on the road ahead, then a checkered flag
        float laneStart = r.X + rad * 0.8f;
        if (carX > laneStart)
            using (var pen = new Pen(pal.Dark ? pal.Accent : pal.AccentText, S(3)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, laneStart, r.Y + rad, carX, r.Y + rad);

        float flag = r.Height * 0.48f, flagX = r.Right - rad - flag / 2;
        if (flagX - S(6) > carX + rad)
            using (var pen = new Pen(pal.RoadLine, S(2))) { pen.DashPattern = new float[] { 3, 3 }; g.DrawLine(pen, carX + rad, r.Y + rad, flagX - S(6), r.Y + rad); }
        float cell = flag / 3, fy = r.Y + (r.Height - flag) / 2;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                using (var b = new SolidBrush((i + j) % 2 == 0 ? pal.Content : pal.Road)) g.FillRectangle(b, flagX + i * cell, fy + j * cell, cell, cell);

        // the mouse, driving right
        var state = g.Save();
        g.TranslateTransform(carX, r.Y + rad);
        g.RotateTransform(90);
        float mw = r.Height * 0.8f, mh = mw * 1.4f;
        Art.DrawMouse(g, new RectangleF(-mw / 2, -mh / 2, mw, mh), Art.Yellow);
        g.Restore(state);
    }

    void DrawTile(Graphics g, RectangleF r, string label, string value, string note, Font small, Font big, Font noteFont)
    {
        using (var path = Art.Round(r, S(12)))
        {
            using (var b = new SolidBrush(pal.Raised)) g.FillPath(b, path);
            if (!pal.Dark) using (var p = new Pen(pal.Hairline)) g.DrawPath(p, path);
        }
        TextRenderer.DrawText(g, label, small, new Point((int)r.X + S(12), (int)r.Y + S(12)), pal.Secondary);
        TextRenderer.DrawText(g, value, big, new Point((int)r.X + S(10), (int)r.Y + S(32)), pal.Content);
        TextRenderer.DrawText(g, note, noteFont, new Point((int)r.X + S(12), (int)r.Y + S(64)), pal.Secondary);
    }
}

// ---------------------------------------------------------------- milestone pop-up

// Mouse Travel's own notification card. It works even when Windows notifications are off,
// never takes keyboard focus, pauses while hovered, and dismisses on click or after 5 seconds.
class Toast : Form
{
    static Toast current;
    const double LifeMs = 5000, InMs = 200, OutMs = 180;
    readonly Palette pal;
    readonly float sc;
    readonly bool animate;
    readonly string title, message;
    readonly Action onClick;
    readonly System.Windows.Forms.Timer timer;
    readonly int msgH;
    double remaining = LifeMs;
    DateTime shownAt, lastTick, fadeStart;
    bool hover, closing, clicked;
    int restLeft;

    public static bool Showing { get { return current != null && !current.IsDisposed; } }

    // afterClose runs once this card is gone, so the next queued one can follow.
    public static void Pop(string title, string message, Action onClick, Action afterClose)
    {
        if (Showing) current.Close();
        current = new Toast(title, message, onClick);
        if (afterClose != null) current.FormClosed += delegate { current = null; afterClose(); };
        current.ShowInCorner();
    }

    public Toast(string title, string message, Action onClick)
    {
        this.title = title;
        this.message = message;
        this.onClick = onClick;
        pal = Palette.Current();
        animate = !Native.ReducedMotion();
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = pal.Surface;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        Cursor = Cursors.Hand;
        AccessibleName = title + ". " + message;
        using (Graphics g = CreateGraphics()) sc = g.DpiX / 96f;

        using (var f = Fonts.Ui(9.5f, FontStyle.Regular))
            msgH = TextRenderer.MeasureText(message, f, new Size(TextWidth, 0), TextFormatFlags.WordBreak).Height;
        ClientSize = new Size(S(360), Math.Max(S(80), S(16) + S(22) + msgH + S(18)));

        timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += delegate { Step(); };
        MouseEnter += delegate { hover = true; };
        MouseLeave += delegate { hover = false; };
        MouseUp += (s, e) => { clicked = e.Button == MouseButtons.Left; Dismiss(); };
    }

    int S(float v) { return (int)Math.Round(v * sc); }
    int TextWidth { get { return S(360) - S(72) - S(40); } }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x80; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW: never steals focus, no Alt+Tab entry
            cp.ClassStyle |= 0x20000;        // drop shadow
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        timer.Dispose();
        base.OnFormClosed(e);
        if (clicked && onClick != null) onClick();
    }

    void ShowInCorner()
    {
        Rectangle wa = Screen.PrimaryScreen.WorkingArea; // the tray lives on the primary screen
        restLeft = wa.Right - Width - S(12);
        Location = new Point(restLeft + (animate ? S(24) : 0), wa.Bottom - Height - S(12));
        if (animate) Opacity = 0;
        shownAt = lastTick = DateTime.Now;
        Show();
        timer.Start();
    }

    void Dismiss()
    {
        if (closing) return;
        closing = true;
        fadeStart = DateTime.Now;
        if (!animate) Close();
    }

    void Step()
    {
        DateTime now = DateTime.Now;
        double dt = (now - lastTick).TotalMilliseconds, t = (now - shownAt).TotalMilliseconds;
        lastTick = now;
        if (closing)
        {
            double k = (now - fadeStart).TotalMilliseconds / OutMs;
            if (k >= 1) { Close(); return; }
            Opacity = 1 - k;
            return;
        }
        if (animate && t < InMs)
        {
            double e = 1 - Math.Pow(1 - t / InMs, 3);
            Opacity = e;
            Left = restLeft + (int)Math.Round(S(24) * (1 - e));
            return;
        }
        if (Left != restLeft) { Opacity = 1; Left = restLeft; }
        if (!hover) remaining -= dt;
        if (remaining <= 0) Dismiss();
        if (animate) Invalidate(new Rectangle(0, Height - S(4), Width, S(4)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(pal.Hairline)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        Art.DrawLogo(g, new RectangleF(S(16), (Height - S(44)) / 2f, S(44), S(44)));
        using (var head = Fonts.Ui(10.5f, FontStyle.Bold))
        using (var body = Fonts.Ui(9.5f, FontStyle.Regular))
        using (var close = Fonts.Ui(11f, FontStyle.Regular))
        {
            TextRenderer.DrawText(g, title, head, new Rectangle(S(72), S(16), TextWidth, S(22)), pal.Content, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, message, body, new Rectangle(S(72), S(16) + S(22), TextWidth, msgH), pal.Secondary, TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, "×", close, new Rectangle(Width - S(34), S(8), S(24), S(24)), pal.Secondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        // a thin countdown line along the bottom edge
        if (animate)
        {
            float frac = (float)Math.Max(0, Math.Min(1, remaining / LifeMs));
            using (var b = new SolidBrush(pal.Dark ? pal.Accent : pal.AccentText))
                g.FillRectangle(b, 0, Height - S(3), Width * frac, S(3));
        }
    }
}

// ---------------------------------------------------------------- screens window

class SetupForm : Form
{
    public readonly double[] Inches;
    public bool StartWithWindows { get { return startup.Checked; } }

    const string BodyText = "I read these sizes from your monitors. If one looks off, nudge it. It's the diagonal, like a “27-inch” screen.";
    const int RowH = 58;
    readonly MonitorInfo[] mons;
    readonly Palette pal;
    readonly float sc;
    readonly Toggle startup;
    readonly Label[] values;
    readonly Rectangle diagram;
    readonly int pad, width, bodyTop, bodyH, rowsTop, ruleY;

    public SetupForm(MonitorInfo[] monitors, bool startWithWindows, bool firstRun)
    {
        mons = monitors;
        pal = Palette.Current();
        Text = "Mouse Travel · Screens";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = pal.Surface;
        ForeColor = pal.Content;
        Font = Fonts.Ui(10f, FontStyle.Regular);
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        Icon = Art.MakeTrayIcon();
        TopMost = true;
        using (Graphics g = CreateGraphics()) sc = g.DpiX / 96f;
        pad = S(24);
        width = S(480);

        Inches = new double[mons.Length];
        for (int i = 0; i < mons.Length; i++) Inches[i] = mons[i].Inches;

        int y = pad + S(38);
        bodyTop = y;
        bodyH = TextRenderer.MeasureText(BodyText, Font, new Size(width - 2 * pad, 0), TextFormatFlags.WordBreak).Height;
        y += bodyH + S(16);
        diagram = new Rectangle(pad, y, width - 2 * pad, S(150));
        y += diagram.Height + S(8);

        rowsTop = y;
        values = new Label[mons.Length];
        for (int i = 0; i < mons.Length; i++)
        {
            int idx = i, cy = y + S(RowH) / 2;
            var plus = new PillButton(pal, false, 13f) { Text = "+", AccessibleName = "Bigger: screen " + (i + 1), Bounds = new Rectangle(width - pad - S(36), cy - S(18), S(36), S(36)) };
            var val = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = Fonts.Ui(12f, FontStyle.Bold),
                ForeColor = pal.Content, BackColor = pal.Surface, Bounds = new Rectangle(plus.Left - S(68), cy - S(18), S(68), S(36)),
            };
            var minus = new PillButton(pal, false, 13f) { Text = "−", AccessibleName = "Smaller: screen " + (i + 1), Bounds = new Rectangle(val.Left - S(36), cy - S(18), S(36), S(36)) };
            minus.Click += delegate { Nudge(idx, -0.5); };
            plus.Click += delegate { Nudge(idx, 0.5); };
            values[i] = val;
            Controls.Add(minus);
            Controls.Add(val);
            Controls.Add(plus);
            y += S(RowH);
        }
        UpdateValues();

        ruleY = y + S(8);
        y = ruleY + S(14);
        startup = new Toggle(pal, sc) { Text = "Start Mouse Travel with Windows", Checked = startWithWindows, Bounds = new Rectangle(pad - S(3), y, width - 2 * pad, S(32)) };
        Controls.Add(startup);
        y += S(32) + S(22);

        var save = new PillButton(pal, true, 10f) { Text = firstRun ? "Start counting" : "Save", DialogResult = DialogResult.OK, Bounds = new Rectangle(width - pad - S(150) + S(3), y, S(150), S(44)) };
        var cancel = new PillButton(pal, false, 10f) { Text = "Cancel", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(save.Left - S(104), y, S(100), S(44)) };
        Controls.Add(cancel);
        Controls.Add(save);
        AcceptButton = save;
        CancelButton = cancel;
        y += S(44) + pad;
        ClientSize = new Size(width, y);
    }

    int S(float v) { return (int)Math.Round(v * sc); }

    void Nudge(int i, double d)
    {
        Inches[i] = Math.Max(10, Math.Min(120, Inches[i] + d));
        UpdateValues();
        Invalidate();
    }

    void UpdateValues()
    {
        for (int i = 0; i < values.Length; i++) values[i].Text = Inches[i].ToString("0.#", Fun.CI) + "″";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.DarkTitleBar(Handle, pal.Dark);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var title = Fonts.Ui(16f, FontStyle.Bold))
        using (var name = Fonts.Ui(10f, FontStyle.Bold))
        using (var sub = Fonts.Ui(9f, FontStyle.Regular))
        using (var badge = Fonts.Ui(9.5f, FontStyle.Bold))
        {
            TextRenderer.DrawText(g, "Your screens", title, new Point(pad - S(3), pad - S(4)), pal.Content);
            TextRenderer.DrawText(g, BodyText, Font, new Rectangle(pad, bodyTop, width - 2 * pad, bodyH), pal.Secondary, TextFormatFlags.WordBreak);
            DrawDiagram(g);

            for (int i = 0; i < mons.Length; i++)
            {
                var m = mons[i];
                int cy = rowsTop + i * S(RowH) + S(RowH) / 2;
                var b = new Rectangle(pad, cy - S(14), S(28), S(28));
                using (var br = new SolidBrush(pal.Bezel)) g.FillEllipse(br, b);
                TextRenderer.DrawText(g, (i + 1).ToString(Fun.CI), badge, b, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                string hint = m.AutoInches > 0
                    ? "detected " + (Math.Round(m.AutoInches * 2) / 2).ToString("0.#", Fun.CI) + "″"
                    : "couldn't detect, please check";
                int tx = pad + S(40), tw = width - pad - S(36 + 68 + 36 + 12) - tx;
                TextRenderer.DrawText(g, m.Name, name, new Rectangle(tx, cy - S(20), tw, S(20)), pal.Content, TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, m.Bounds.Width + " × " + m.Bounds.Height + " · " + hint, sub,
                    new Rectangle(tx, cy + S(1), tw, S(20)), pal.Secondary, TextFormatFlags.EndEllipsis);
            }
            using (var p = new Pen(pal.Hairline)) g.DrawLine(p, pad, ruleY, width - pad, ruleY);
        }
    }

    // The screens drawn to scale by their real physical size, left to right as Windows arranges them.
    void DrawDiagram(Graphics g)
    {
        using (var path = Art.Round(diagram, S(14)))
        {
            using (var b = new SolidBrush(pal.Raised)) g.FillPath(b, path);
            if (!pal.Dark) using (var p = new Pen(pal.Hairline)) g.DrawPath(p, path);
        }
        int n = mons.Length;
        if (n == 0) return;
        var order = new List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) => mons[a].Bounds.X.CompareTo(mons[b].Bounds.X));

        var w = new float[n];
        var h = new float[n];
        float totalW = 60f * (n - 1), maxH = 0;
        for (int i = 0; i < n; i++)
        {
            Rectangle bd = mons[i].Bounds;
            double mm = Inches[i] * 25.4 / Math.Sqrt((double)bd.Width * bd.Width + (double)bd.Height * bd.Height);
            w[i] = (float)(bd.Width * mm);
            h[i] = (float)(bd.Height * mm);
            totalW += w[i];
            maxH = Math.Max(maxH, h[i]);
        }
        float stand = maxH * 0.16f;
        float scale = Math.Min((diagram.Width - S(48)) / totalW, (diagram.Height - S(28)) / (maxH + stand));
        float x = diagram.X + (diagram.Width - totalW * scale) / 2;
        float baseY = diagram.Y + (diagram.Height - (maxH + stand) * scale) / 2 + maxH * scale;

        using (var bezel = new SolidBrush(pal.Bezel))
        using (var big = Fonts.Ui(13f, FontStyle.Bold))
        using (var small = Fonts.Ui(8.5f, FontStyle.Regular))
        {
            foreach (int i in order)
            {
                var r = new RectangleF(x, baseY - h[i] * scale, w[i] * scale, h[i] * scale);
                float cx = r.X + r.Width / 2, neck = stand * scale * 0.7f;
                g.FillRectangle(bezel, cx - S(5), baseY - 1, S(10), neck);
                using (var foot = Art.Round(new RectangleF(cx - r.Width * 0.15f, baseY + neck - 1, r.Width * 0.3f, S(5)), S(2))) g.FillPath(bezel, foot);
                using (var body = Art.Round(r, S(5))) g.FillPath(bezel, body);

                var screen = new RectangleF(r.X + S(4), r.Y + S(4), r.Width - S(8), r.Height - S(8));
                using (var glass = new SolidBrush(Palette.Shade(pal.Bezel, 0.14f)))
                using (var sp = Art.Round(screen, S(2))) g.FillPath(glass, sp);

                var label = Rectangle.Round(screen);
                var top = new Rectangle(label.X, label.Y, label.Width, label.Height / 2 + S(4));
                var bottom = new Rectangle(label.X, label.Y + label.Height / 2 + S(2), label.Width, label.Height / 2);
                TextRenderer.DrawText(g, (i + 1).ToString(Fun.CI), big, top, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom);
                TextRenderer.DrawText(g, Inches[i].ToString("0.#", Fun.CI) + "″", small, bottom, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                x += (w[i] + 60f) * scale;
            }
        }
    }
}

// ---------------------------------------------------------------- drawing

static class Art
{
    public static readonly Color Yellow = Palette.Hex(0xFFC93C);
    static readonly Color Ink = Palette.Hex(0x161616);
    static readonly Color Paper = Palette.Hex(0xF5F5F3);

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // A little pill-shaped mouse with a scroll wheel.
    public static void DrawMouse(Graphics g, RectangleF r, Color body)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float stroke = Math.Max(1f, r.Width * 0.09f);
        var inner = new RectangleF(r.X + stroke / 2, r.Y + stroke / 2, r.Width - stroke, r.Height - stroke);
        using (var path = Round(inner, inner.Width / 2))
        using (var fill = new SolidBrush(body))
        using (var pen = new Pen(Ink, stroke))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
            float split = inner.Y + inner.Height * 0.42f;
            g.DrawLine(pen, inner.X + inner.Width / 2, inner.Y, inner.X + inner.Width / 2, split);
            g.DrawLine(pen, inner.X + stroke * 0.3f, split, inner.Right - stroke * 0.3f, split);
        }
        float ww = r.Width * 0.2f, wh = r.Height * 0.2f;
        var wheel = new RectangleF(r.X + (r.Width - ww) / 2, r.Y + r.Height * 0.16f, ww, wh);
        using (var path = Round(wheel, ww / 2)) using (var b = new SolidBrush(Ink)) g.FillPath(b, path);
        g.SmoothingMode = old;
    }

    // ---- odometer: whole km on dark wheels, tenths on a mint wheel. Wheels roll like a car's.

    public static SizeF OdometerSize(float w, float h, int intDigits)
    {
        float pad = w * 0.2f, padV = w * 0.16f, gap = w * 0.1f, dot = w * 0.3f;
        return new SizeF(pad * 2 + w * (intDigits + 1) + gap * (intDigits - 1) + dot, h + padV * 2);
    }

    public static void DrawOdometer(Graphics g, PointF at, float w, float h, int intDigits, double km)
    {
        float pad = w * 0.2f, padV = w * 0.16f, gap = w * 0.1f, dot = w * 0.3f;
        SizeF size = OdometerSize(w, h, intDigits);
        var housing = new RectangleF(at.X, at.Y, size.Width, size.Height);
        using (var path = Round(housing, w * 0.24f))
        using (var b = new SolidBrush(Palette.Hex(0x0A0A0A)))
        using (var p = new Pen(Palette.Hex(0x2E2E30)))
        {
            g.FillPath(b, path);
            g.DrawPath(p, path);
        }

        double units = Math.Max(0, km) * 10; // in tenths of a km
        int n = intDigits + 1;
        float x = at.X + pad, y = at.Y + padV;
        using (var font = Fonts.DisplayPx(h * 0.6f, FontStyle.Bold))
        {
            for (int col = 0; col < n; col++)
            {
                int place = n - 1 - col; // 0 = tenths
                if (place == 0)
                {
                    x += dot - gap;
                    float r = w * 0.075f;
                    using (var b = new SolidBrush(Paper)) g.FillEllipse(b, x - dot / 2 - r, y + h * 0.76f - r, r * 2, r * 2);
                }
                double pow = Math.Pow(10, place);
                double whole = Math.Floor(units / pow);
                double rem = units - whole * pow;
                // a wheel turns with the one to its right only while that one rolls from 9 to 0
                double f = place == 0 ? rem : (rem > pow - 1 ? rem - (pow - 1) : 0);
                DrawWheel(g, new RectangleF(x, y, w, h), (int)(whole % 10), (float)f, place == 0, font);
                x += w + gap;
            }
        }
    }

    static void DrawWheel(Graphics g, RectangleF r, int digit, float f, bool tenths, Font font)
    {
        var state = g.Save();
        using (var path = Round(r, r.Width * 0.14f))
        {
            g.SetClip(path, CombineMode.Intersect);
            using (var b = new SolidBrush(tenths ? Yellow : Palette.Hex(0x1F1F1F))) g.FillRectangle(b, r);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var ink = new SolidBrush(tenths ? Ink : Paper))
            {
                float off = f * r.Height, nudge = r.Height * 0.03f;
                g.DrawString(digit.ToString(Fun.CI), font, ink, new RectangleF(r.X, r.Y - off + nudge, r.Width, r.Height), sf);
                if (f > 0) g.DrawString(((digit + 1) % 10).ToString(Fun.CI), font, ink, new RectangleF(r.X, r.Y - off + r.Height + nudge, r.Width, r.Height), sf);
            }
        }
        g.Restore(state);
    }

    // ---- logo and icons

    // The logo ("lane driver"): the mouse driving right on a dark tile, lane dashes fading out behind it.
    // The gap between dashes and mouse keeps it reading as motion rather than a cable.
    // compact: for 24 px and below, a bigger mouse and a single dash so it still reads in the tray.
    public static void DrawLogo(Graphics g, RectangleF t, bool compact = false)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float u = t.Width, cy = t.Y + u * 0.5f;
        using (var p = Round(t, u * 0.24f)) using (var b = new SolidBrush(Ink)) g.FillPath(b, p);
        using (var dash = new Pen(Yellow, u * (compact ? 0.11f : 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (compact) g.DrawLine(dash, t.X + u * 0.16f, cy, t.X + u * 0.26f, cy);
            else
            {
                g.DrawLine(dash, t.X + u * 0.30f, cy, t.X + u * 0.38f, cy);
                dash.Color = Color.FromArgb(150, Yellow);
                g.DrawLine(dash, t.X + u * 0.15f, cy, t.X + u * 0.20f, cy);
            }
        }
        float mw = u * (compact ? 0.42f : 0.30f), mh = u * (compact ? 0.56f : 0.44f);
        var state = g.Save();
        g.TranslateTransform(t.X + u * (compact ? 0.60f : 0.64f), cy);
        g.RotateTransform(90);
        DrawMouse(g, new RectangleF(-mw / 2, -mh / 2, mw, mh), Yellow);
        g.Restore(state);
        g.SmoothingMode = old;
    }

    public static Bitmap IconBitmap(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            DrawLogo(g, new RectangleF(0.5f, 0.5f, s - 1, s - 1), s <= 24);
        }
        return bmp;
    }

    // A multi-size .ico (PNG-compressed entries), used for both the tray and the .exe.
    public static byte[] MakeIcoBytes()
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        var pngs = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (var bmp = IconBitmap(s)) using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }
        }
        using (var ms = new MemoryStream()) using (var w = new BinaryWriter(ms))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (byte[] p in pngs) w.Write(p);
            w.Flush();
            return ms.ToArray();
        }
    }

    public static Icon MakeTrayIcon()
    {
        int size;
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) size = (int)Math.Round(16 * g.DpiX / 96f);
        using (var ms = new MemoryStream(MakeIcoBytes())) return new Icon(ms, size, size);
    }

    // ---- the 1080x1080 weekly share card ("night drive")

    public static Bitmap MakeCard(double[] days, DateTime weekStart, double lifetime, bool thisWeek)
    {
        const int W = 1080, H = 1080;
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        double total = 0, max = 1;
        int best = -1;
        for (int i = 0; i < 7; i++)
        {
            total += days[i];
            if (days[i] > max) { max = days[i]; best = i; }
        }

        using (Graphics g = Graphics.FromImage(bmp))
        using (var white = new SolidBrush(Paper))
        using (var dim = new SolidBrush(Palette.Hex(0xA1A1A6)))
        using (var accent = new SolidBrush(Yellow))
        using (var bar = new SolidBrush(Palette.Hex(0x3A3A3C)))
        using (var fEyebrow = Fonts.Px(26, FontStyle.Bold))
        using (var fHead = Fonts.Px(56, FontStyle.Regular))
        using (var fUnit = Fonts.DisplayPx(72, FontStyle.Bold))
        using (var fCmp = Fonts.Px(44, FontStyle.Bold))
        using (var fVal = Fonts.Px(26, FontStyle.Bold))
        using (var fTag = Fonts.Px(20, FontStyle.Bold))
        using (var fDay = Fonts.Px(28, FontStyle.Bold))
        using (var fFoot = Fonts.Px(26, FontStyle.Regular))
        using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (var right = new StringFormat { Alignment = StringAlignment.Far })
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Palette.Hex(0x161616));

            string range = weekStart.ToString("d MMM", Fun.CI) + " – " + weekStart.AddDays(6).ToString("d MMM yyyy", Fun.CI);
            DrawLogo(g, new RectangleF(72, 70, 50, 50));
            g.DrawString("MOUSE TRAVEL · WEEKLY TRIP", fEyebrow, accent, 128, 80);
            g.DrawString(range, fEyebrow, dim, new RectangleF(500, 80, 500, 40), right);
            g.DrawString(thisWeek ? "This week I drove my mouse" : "Last week I drove my mouse", fHead, white, 74, 140);

            int digits = Math.Max(3, ((long)Math.Floor(total / 1000)).ToString(Fun.CI).Length);
            float ww = digits > 3 ? 128f * 4 / (digits + 1) : 128f, wh = ww * 1.4f;
            SizeF odo = OdometerSize(ww, wh, digits);
            DrawOdometer(g, new PointF(80, 238), ww, wh, digits, Math.Round(total / 1000, 1)); // settled wheels for a still image
            g.DrawString("km", fUnit, dim, 80 + odo.Width + 18, 238 + odo.Height - 104);

            string cmp = Fun.Compare(total);
            g.DrawString(cmp != null ? "That's about " + cmp + "!" : "Just pulling out of the driveway!", fCmp, white, 76, 238 + odo.Height + 30);

            // daily bars standing along a road, the mouse driving off to the right
            float left = 80, rightEdge = 940, top = 650, roadTop = 868, roadH = 64, bottom = roadTop - 14, bw = 88;
            float gap = (rightEdge - left - 7 * bw) / 6;
            using (var road = Round(new RectangleF(40, roadTop, 1000, roadH), roadH / 2))
            using (var rb = new SolidBrush(Palette.Hex(0x232323))) g.FillPath(rb, road);
            string[] labels = { "M", "T", "W", "T", "F", "S", "S" };
            for (int i = 0; i < 7; i++)
            {
                float x = left + i * (bw + gap);
                float h = (float)Math.Max(10, (bottom - top) * days[i] / max);
                using (var path = Round(new RectangleF(x, bottom - h, bw, h), Math.Min(16, h / 2)))
                    g.FillPath(i == best ? accent : bar, path);
                if (days[i] >= 50)
                    g.DrawString((days[i] / 1000).ToString("0.0", Fun.CI), fVal, i == best ? accent : dim, new RectangleF(x - 20, bottom - h - 44, bw + 40, 36), center);
                if (i == best)
                    g.DrawString("BEST", fTag, accent, new RectangleF(x - 20, bottom - h - 74, bw + 40, 30), center);
                g.DrawString(labels[i], fDay, white, new RectangleF(x, roadTop, bw, roadH), center);
                if (i < 6)
                    using (var dash = new Pen(Palette.Hex(0x5A5A5E), 5f)) g.DrawLine(dash, x + bw + gap * 0.3f, roadTop + roadH / 2, x + bw + gap * 0.7f, roadTop + roadH / 2);
            }
            var state = g.Save();
            g.TranslateTransform(1000, roadTop + roadH / 2);
            g.RotateTransform(90);
            DrawMouse(g, new RectangleF(-24, -35, 48, 70), Yellow);
            g.Restore(state);

            g.DrawString("Lifetime " + Fun.Dist(lifetime) + " · " + Fun.MoonPercent(lifetime) + " of the way to the Moon", fFoot, dim, 80, 978);
            g.DrawString("#MouseTravel", fFoot, accent, new RectangleF(600, 978, 400, 40), right);
        }
        return bmp;
    }
}

// ---------------------------------------------------------------- self test (no UI): writes a report + sample images

static class SelfTest
{
    public static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        var store = new Store();
        var mons = Monitors.Detect(store);
        foreach (var m in mons)
        {
            sb.AppendLine(m.Name + " | " + m.Bounds + " | auto=" + m.AutoInches.ToString("0.0", Fun.CI) +
                "in | using=" + m.Inches.ToString("0.0", Fun.CI) + "in | mm/px=" + m.MmPerPx.ToString("0.0000", Fun.CI) + " | key=" + m.Key);
        }
        foreach (double v in new double[] { 3, 42, 487, 1404, 23456, 212000 })
            sb.AppendLine(v + " m -> " + Fun.Dist(v) + " | " + Fun.Compare(v) + " | level " + Fun.Level(v));
        sb.AppendLine("reduced motion: " + Native.ReducedMotion());
        File.WriteAllText(Path.Combine(outDir, "report.txt"), sb.ToString());

        // the icon at its real sizes on a dark and a light taskbar
        int[] sizes = { 256, 48, 32, 24, 16 };
        using (var strip = new Bitmap(640, 560))
        using (var g = Graphics.FromImage(strip))
        {
            g.Clear(Palette.Hex(0x202020));
            using (var light = new SolidBrush(Palette.Hex(0xEEEEEC))) g.FillRectangle(light, 0, 280, 640, 280);
            for (int row = 0; row < 2; row++)
            {
                int x = 12;
                foreach (int s in sizes)
                {
                    using (var icon = Art.IconBitmap(s)) g.DrawImageUnscaled(icon, x, row * 280 + (280 - s) / 2);
                    x += s + 28;
                }
            }
            strip.Save(Path.Combine(outDir, "logo.png"), ImageFormat.Png);
        }

        double[] days = { 820, 1430, 960, 2210, 1780, 310, 120 };
        using (var card = Art.MakeCard(days, Fun.WeekStart(DateTime.Now), 48210, true)) card.Save(Path.Combine(outDir, "card.png"), ImageFormat.Png);

        foreach (bool dark in new[] { true, false })
        {
            Palette.ForceDark = dark;
            string tag = dark ? "dark" : "light";
            // 1.456 km: the tenths wheel is caught mid-roll
            using (var p = new Popup(delegate { return new Stats { Today = 1456, Week = 5230, AllTime = 48210, Since = new DateTime(2026, 11, 2) }; }, 1.456))
                Snap(p, Path.Combine(outDir, "popup-" + tag + ".png"));
            using (var f = new SetupForm(mons.ToArray(), true, false))
                Snap(f, Path.Combine(outDir, "setup-" + tag + ".png"));
            using (var t = new Toast("1 km today!", "Your mouse officially went for a walk.", null))
                Snap(t, Path.Combine(outDir, "toast-" + tag + ".png"));
        }
    }

    static void Snap(Form f, string path)
    {
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(-5000, -5000);
        f.Show();
        Application.DoEvents();
        using (var bmp = new Bitmap(f.Width, f.Height))
        {
            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
            bmp.Save(path, ImageFormat.Png);
        }
        f.Hide();
    }
}
