using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OledTaskbar
{
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] public struct Source { public Luid Adapter; public uint Id, Mode, Status; }
        [StructLayout(LayoutKind.Sequential)] public struct Target
        {
            public Luid Adapter;
            public uint Id, Mode;
            public int Technology;
            public uint Rotation, Scaling, Numerator, Denominator, Scan;
            public int Available;
            public uint Status;
        }
        [StructLayout(LayoutKind.Sequential)] public struct DisplayPath { public Source Source; public Target Target; public uint Flags; }
        [StructLayout(LayoutKind.Explicit, Size = 64)] public struct ModeInfo { [FieldOffset(0)] public uint Type; }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct AppBar
        {
            public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Rect; public IntPtr Param;
        }
        [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] DisplayPath[] pathArray, ref uint modes, [Out] ModeInfo[] modeArray, IntPtr topology);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string name, string title);
        [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint message, ref AppBar data);

        static AppBar Bar()
        {
            AppBar bar = new AppBar();
            bar.Size = (uint)Marshal.SizeOf(typeof(AppBar));
            bar.Window = FindWindow("Shell_TrayWnd", null);
            if (bar.Window == IntPtr.Zero) throw new InvalidOperationException("等待 Windows 任务栏就绪");
            return bar;
        }
        public static bool IsHidden()
        {
            AppBar bar = Bar();
            return (SHAppBarMessage(4, ref bar).ToUInt64() & 1) != 0;
        }
        public static void SetHidden(bool hidden)
        {
            AppBar bar = Bar();
            ulong state = SHAppBarMessage(4, ref bar).ToUInt64();
            if (((state & 1) != 0) == hidden) return;
            bar.Param = new IntPtr((long)(hidden ? state | 1UL : state & ~1UL));
            SHAppBarMessage(10, ref bar);
            if (IsHidden() != hidden) throw new InvalidOperationException("Windows 尚未应用任务栏设置，将稍后重试");
        }
        public static bool IsInternal(int technology)
        {
            return technology == 6 || technology == 11 || technology == 13 || technology == int.MinValue;
        }
        public static bool IsExternal(int technology)
        {
            // Unknown/virtual output is deliberately not treated as a physical external monitor.
            return technology == 0 || technology == 1 || technology == 2 || technology == 3 ||
                technology == 4 || technology == 5 || technology == 8 || technology == 9 ||
                technology == 10 || technology == 12 || technology == 14 || technology == 15 ||
                technology == 16 || technology == 18;
        }
        public static bool? Decide(int[] technologies)
        {
            bool external = false, unknown = false;
            foreach (int tech in technologies)
            {
                if (IsInternal(tech)) return true;
                if (IsExternal(tech)) external = true; else unknown = true;
            }
            if (external && !unknown) return false;
            return null;
        }
        public static int[] Technologies()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                uint pc, mc;
                int error = GetDisplayConfigBufferSizes(2, out pc, out mc);
                if (error != 0) throw new Win32Exception(error);
                DisplayPath[] paths = new DisplayPath[pc];
                ModeInfo[] modes = new ModeInfo[mc];
                error = QueryDisplayConfig(2, ref pc, paths, ref mc, modes, IntPtr.Zero);
                if (error == 122) continue; // Topology changed between sizing and query.
                if (error != 0) throw new Win32Exception(error);
                List<int> result = new List<int>();
                for (int i = 0; i < pc; i++)
                {
                    if ((paths[i].Flags & 1) == 0) continue;
                    if (paths[i].Target.Available == 0) throw new InvalidOperationException("显示器正在切换，等待连接稳定");
                    result.Add(paths[i].Target.Technology);
                }
                result.Sort();
                return result.ToArray();
            }
            throw new InvalidOperationException("显示器连接尚未稳定");
        }
        public static string Diagnose()
        {
            int[] tech = Technologies();
            bool? decision = Decide(tech);
            return "Active connector types: " + string.Join(", ", Array.ConvertAll(tech, x => x.ToString())) +
                "\r\nAuto-hide now: " + IsHidden() + "\r\nDesired auto-hide: " +
                (decision.HasValue ? decision.Value.ToString() : "Unknown; keep current setting") + "\r\n";
        }
        public static string SelfTest()
        {
            if (Marshal.SizeOf(typeof(DisplayPath)) != 72 || Marshal.SizeOf(typeof(ModeInfo)) != 64 ||
                Marshal.SizeOf(typeof(Target)) != 48 || Marshal.SizeOf(typeof(Source)) != 20 ||
                Marshal.SizeOf(typeof(AppBar)) != (IntPtr.Size == 8 ? 48 : 36)) throw new Exception("Interop layout failed");
            int[][] cases = { new[] {11}, new[] {6}, new[] {13}, new[] {int.MinValue}, new[] {5}, new[] {10},
                new[] {11,5}, new[] {11,10}, new[] {5,10}, new int[0], new[] {-1}, new[] {17}, new[] {5,-1}, new[] {11,-1} };
            bool?[] expected = {true,true,true,true,false,false,true,true,false,null,null,null,null,true};
            for (int i = 0; i < cases.Length; i++)
                if (Decide(cases[i]) != expected[i]) throw new Exception("Policy case failed: " + i);
            return "PASS: native layouts and 14 display-policy cases.";
        }
    }

    static class Program
    {
        internal const string Title = "OLED 任务栏助手";
        internal static readonly string Prefix = "Local\\OledTaskbar_" + WindowsIdentity.GetCurrent().User.Value;
        internal static readonly string[] Commands = { "toggle", "hide", "show", "auto", "exit" };
        [STAThread] static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string command = args.Length == 0 ? "auto" : args[0].TrimStart('-').ToLowerInvariant();
                if (command == "diagnose" || command == "self-test")
                {
                    string result = command == "diagnose" ? Native.Diagnose() : Native.SelfTest();
                    if (args.Length > 1) File.WriteAllText(args[1], result); else MessageBox.Show(result, Title);
                    return 0;
                }
                if (Array.IndexOf(Commands, command) < 0) throw new ArgumentException("未知命令：" + command);
                try
                {
                    using (EventWaitHandle signal = EventWaitHandle.OpenExisting(Prefix + "_" + command)) signal.Set();
                    return 0;
                }
                catch (WaitHandleCannotBeOpenedException) { }
                if (command == "exit") return 0;
                if (command != "auto")
                {
                    Native.SetHidden(command == "toggle" ? !Native.IsHidden() : command == "hide");
                    return 0;
                }
                bool created;
                using (Mutex mutex = new Mutex(true, Prefix + "_mutex", out created))
                {
                    if (!created) return 0;
                    try { using (TrayContext context = new TrayContext()) Application.Run(context); }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }

    sealed class TrayContext : ApplicationContext
    {
        readonly NotifyIcon icon;
        readonly System.Drawing.Icon artwork;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer timer;
        readonly List<EventWaitHandle> signals = new List<EventWaitHandle>();
        readonly ToolStripMenuItem status, automatic, startup;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "OledTaskbarController";
        bool auto = true;
        bool? original;
        string candidate, lastError;
        int stable;
        public TrayContext()
        {
            foreach (string command in Program.Commands)
                signals.Add(new EventWaitHandle(false, EventResetMode.AutoReset, Program.Prefix + "_" + command));
            menu = new ContextMenuStrip();
            status = new ToolStripMenuItem("正在检测显示器…") { Enabled = false };
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());
            automatic = new ToolStripMenuItem("自动模式：内屏启用时隐藏", null, (s,e) => Handle("auto"));
            menu.Items.Add(automatic);
            menu.Items.Add("手动：自动隐藏", null, (s,e) => Handle("hide"));
            menu.Items.Add("手动：始终显示", null, (s,e) => Handle("show"));
            menu.Items.Add("切换显示 / 隐藏（暂停自动）", null, (s,e) => Handle("toggle"));
            menu.Items.Add(new ToolStripSeparator());
            startup = new ToolStripMenuItem("登录 Windows 后自动启动", null, ToggleStartup);
            menu.Items.Add(startup);
            menu.Items.Add("查看检测信息", null, (s,e) => { try { MessageBox.Show(Native.Diagnose(), Program.Title); } catch(Exception ex) { Error(ex); } });
            menu.Items.Add("退出并保留当前设置", null, (s,e) => ExitThread());
            menu.Items.Add("退出并恢复启动前设置", null, (s,e) => Handle("exit"));
            menu.Opening += (s,e) => {
                try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey)) startup.Checked = key != null && key.GetValue(RunName) != null; }
                catch (Exception ex) { Error(ex); }
            };
            using (Stream stream = typeof(Program).Assembly.GetManifestResourceStream("TaskbarController.ico"))
                artwork = new System.Drawing.Icon(stream, SystemInformation.SmallIconSize);
            icon = new NotifyIcon { Icon = artwork, Text = Program.Title, ContextMenuStrip = menu, Visible = true };
            icon.DoubleClick += (s,e) => Handle("toggle");
            timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += Tick;
            timer.Start();
            Tick(null, EventArgs.Empty);
        }
        void Error(Exception ex)
        {
            status.Text = "等待重试：" + ex.Message;
            icon.Text = "OLED 任务栏助手：等待重试";
            if (lastError != ex.Message)
            {
                icon.ShowBalloonTip(4000, Program.Title, ex.Message, ToolTipIcon.Warning);
                lastError = ex.Message;
            }
        }
        void Handle(string command)
        {
            try
            {
                if (command == "auto") { auto = true; candidate = null; stable = 0; }
                else if (command == "exit") { if (original.HasValue) Native.SetHidden(original.Value); ExitThread(); return; }
                else
                {
                    if (!original.HasValue) original = Native.IsHidden();
                    auto = false;
                    Native.SetHidden(command == "toggle" ? !Native.IsHidden() : command == "hide");
                }
                UpdateStatus();
            }
            catch (Exception ex) { Error(ex); }
        }
        void Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < signals.Count; i++)
                if (signals[i].WaitOne(0)) { Handle(Program.Commands[i]); if (Program.Commands[i] == "exit") return; }
            try
            {
                if (!original.HasValue) original = Native.IsHidden();
                if (auto)
                {
                    int[] tech = Native.Technologies();
                    string key = string.Join(",", Array.ConvertAll(tech, x => x.ToString()));
                    if (candidate != key) { candidate = key; stable = 1; }
                    else stable = Math.Min(stable + 1, 2);
                    bool? decision = Native.Decide(tech);
                    if (!decision.HasValue) throw new InvalidOperationException("未识别出可靠的屏幕类型，保留当前设置");
                    if (stable < 2) { status.Text = "等待显示器连接稳定…"; return; }
                    Native.SetHidden(decision.Value);
                }
                UpdateStatus();
                lastError = null;
            }
            catch (Exception ex) { candidate = null; stable = 0; Error(ex); }
        }
        void UpdateStatus()
        {
            automatic.Checked = auto;
            string text = (auto ? "自动模式" : "手动模式（自动已暂停）") + " · " + (Native.IsHidden() ? "任务栏自动隐藏" : "任务栏始终显示");
            status.Text = text;
            icon.Text = text;
        }
        void ToggleStartup(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key.GetValue(RunName) == null) key.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --auto");
                    else key.DeleteValue(RunName, false);
                }
            }
            catch(Exception ex) { Error(ex); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop(); timer.Dispose(); icon.Visible = false; icon.Dispose(); artwork.Dispose(); menu.Dispose();
                foreach (EventWaitHandle signal in signals) signal.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
