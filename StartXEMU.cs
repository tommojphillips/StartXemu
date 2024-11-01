using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace StartXemu
{
    public class StartXEMU
    {
        // Author, tommojphillips. 06.04.2024
        // Github, https://github.com/tommojphillips

        public const string PATTERN = @"^(\w+)\s*=\s*'([^']*)'$";
        public const string PATTERN_NO_QUOTES = @"^(\w+)\s*=\s*(\w+)$";
        public const string PATTERN_SECTION_TITLE = @"^\[(.+)\]$";
        public const string PATTERN_RESOLUTION = @"'(\d+)x(\d+)'";
        public const string PATTERN_TOM_ENTRY = @"({0}\s*=\s*).+";

        private string dir;
        private string exe;
        private string cfg;
        private string dvd;

        private int startupPosition;
        private int startupWidth;
        private int startupHeight;

        private int serial;
        private Process xemuProc;
        private Process cmdProc;

        private bool startupSizeOk = false;
        private XSwitch sw;

        private readonly Dictionary<string, XSetting> defines = new Dictionary<string, XSetting>()
        {
            // SYS FILES
            { "bootrom_path",  new XSetting("sys.files", "XEMU_MCPX") },
            { "flashrom_path", new XSetting("sys.files", "XEMU_BIOS") },
            { "eeprom_path",   new XSetting("sys.files", "XEMU_EEPROM") },
            { "hdd_path",      new XSetting("sys.files", "XEMU_HDD") },
            { "dvd_path",      new XSetting("sys.files", "XEMU_DVD") },

            { "mem_limit",           new XSetting("sys", "XEMU_MEM") },
            
            // DISPLAY
            { "avpack",         new XSetting("sys", "XEMU_VIDEO")},
            { "startup_size",  new XSetting("display.window", "XEMU_SIZE") },

            { "skip_boot_anim", new XSetting("general", "XEMU_SKIP_ANI", false, "false") }
        };

        public void run(XSwitch sw)
        {            

            this.sw = sw;
            getEnv();

            string[] lines = File.ReadAllLines(cfg);

            // list xemu.toml file settings
            if (sw.ls)
            {
                listCfg(lines, sw.lsSection);
                return;
            }
                        
            string lineBreak = new string('-', Console.WindowWidth);

            Console.Write("XEMU_DIR: {0}\n", dir);
            Console.Write("XEMU_CFG: {0}\n", cfg);
            Console.WriteLine(lineBreak);

            // update xemu.toml with environment variables
            updateConfig(lines);

            Console.WriteLine(lineBreak);

            Console.WriteLine("xemu.toml updated.");

            // run command.
            if (sw.openPrompt)
            {
                runCmd(sw.cmd);
            }

            startXEMU();
        }

        public void startXEMU()
        {
            StringBuilder args = new StringBuilder();
            xemuProc = new Process();

            Environment.CurrentDirectory = dir;

            // add config path override switch
            args.Append($"-config_path {cfg}");

            switch (serial)
            {
                case 1:
                    args.Append(" -s -device lpc47m157 -serial stdio");
                    break;
                case 2:
                    args.Append(" -s -device lpc47m157 -serial COM2");
                    break;
                case 3:
                    args.Append(" -s -device lpc47m157 -serial  tcp::4444,server,nowait");
                    break;
            }

            xemuProc.StartInfo.FileName = exe;
            xemuProc.StartInfo.Arguments = args.ToString();
            xemuProc.StartInfo.UseShellExecute = false;
            xemuProc.StartInfo.RedirectStandardOutput = true;
            xemuProc.StartInfo.RedirectStandardError = true;
            xemuProc.OutputDataReceived += xemu_outputReceived;

            xemuProc.EnableRaisingEvents = true;
            xemuProc.Exited += xemuExited;

            xemuProc.Start();

            // wait for xemu window to be created.
            xemuProc.WaitForInputIdle();
            
            // force focus on xemu window
            NativeMethods.SetForegroundWindow(xemuProc.MainWindowHandle);

            if (startupSizeOk && startupPosition > 0)
            {
                updateWindowPosition(xemuProc, startupPosition);
            }

            xemuProc.BeginOutputReadLine();

            Console.WriteLine("xemu has Started\n");

            xemuProc.WaitForExit();

            Console.WriteLine("xemu has Exited");
        }

        public void runCmd(string cmd)
        {
            string[] cmds = new string[]
            {
                "Title XEMU Interactive",
                "echo XEMU Interactive",
                "echo.",
                "PROMPT=^>",
                cmd ?? "",
            };

            cmdProc = new Process();
            cmdProc.StartInfo.UseShellExecute = true;
            cmdProc.StartInfo.FileName = "cmd";
            cmdProc.StartInfo.Arguments = $"/k " + string.Join("&", cmds);
            cmdProc.EnableRaisingEvents = true;
            cmdProc.Exited += cmdExited;
            cmdProc.Start();
        }

        public bool updateWindowPosition(Process proc, int pos)
        {
            int count = 0;
            IntPtr handle;
                        
            // loop until process window is found.
            while ((handle = proc.MainWindowHandle) == IntPtr.Zero)
            {
                count++;

                if (count > 100 || proc.HasExited)
                    return false;

                Thread.Yield();
            }

            int x;
            int y;

            Screen screen = Screen.FromHandle(handle);

            int width = screen.Bounds.Width - 10;
            int height = screen.Bounds.Height - 80;

            switch (pos)
            {
                case 1: // top left
                    x = 0;
                    y = 0;
                    break;

                case 2: // top right
                    x = width - startupWidth;
                    y = 0;
                    break;

                case 3: // bottom left
                    x = 0;
                    y = height - startupHeight;
                    break;

                case 4: // bottom right
                    x = width - startupWidth;
                    y = height - startupHeight;
                    break;

                case 0: // center
                default:
                    return true;
            }

            NativeMethods.SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0);

            return true;
        }

        public void updateConfig(string[] lines)
        {
            List<string> newLines = new List<string>(lines);

            foreach (KeyValuePair<string, XSetting> item in defines)
            {                
                // check if key exists in xemu.toml
                int index = newLines.FindIndex(x => x.StartsWith(item.Key));
                string varStr = null;

                // key not found, add it.
                if (index == -1)
                {
                    // find the section                    
                    string section = $"[{item.Value.sectionName}]";

                    // find the section index
                    index = newLines.FindIndex(x => x.StartsWith(section));

                    // section not found, add it.
                    if (index == -1)
                    {
                        // add section
                        newLines.Add(section);
                        index = newLines.Count - 1;
                    }

                    // add toml entry to section
                    index++;
                    if (item.Value.quoted)
                        newLines.Insert(index, $"{item.Key} = '{item.Value.defaultValue}'");
                    else
                        newLines.Insert(index, $"{item.Key} = {item.Value.defaultValue}");
                }

                // Check if the environment variable is defined.
                bool defined = checkVar(item.Value.variableName, out string var);

                // check if the setting is defined in xemu.toml
                if (!defined && !matchSetting(newLines[index], PATTERN, out _, out var) && !matchSetting(newLines[index], PATTERN_NO_QUOTES, out _, out var))
                        continue;

                varStr = var;

                // Validate setting; xemu.toml entry-specific checks
                switch (item.Key)
                {
                    case "mem_limit":
                        // only 64 and 128.

                        if (!int.TryParse(var.Trim(), out int mem) || (mem != 64 && mem != 128))
                            var = "64";
                        varStr = var + "MB";
                        break;

                    case "avpack":
                        // check if video output is valid, if not, default to HDTV
                        switch (var)
                        {
                            case "hdtv":
                            case "composite":
                            case "vga":
                            case "scart":
                            case "svideo":
                            case "rfu":
                            case "none":
                                break;

                            default:
                                var = "hdtv";
                                varStr = var;
                                break;
                        }

                        break;

                    case "bootrom_path":
                    case "flashrom_path":
                    case "eeprom_path":
                    case "hdd_path":
                        // check file exists.
                        if (!fileExists(var))
                            throw new Exception($"System file not found: {item.Key} = '{var}'");                        
                        break;

                    case "dvd_path":
                        // dvd_path is optional
                        if (!fileExists(var))
                        {
                            var = string.Empty;
                            varStr += " (not found)";
                        }
                        
                        break;

                    case "skip_boot_anim":
                        // only true or false
                        int.TryParse(var, out int skipAni);
                        var = skipAni > 0 ? "true" : "false";
                        varStr = var;
                        break;
                }

                if (defined)
                    Console.Write("{0}:\t{1}\n", item.Value.variableName, varStr);

                // update the toml entry.
                newLines[index] = Regex.Replace(newLines[index], string.Format(PATTERN_TOM_ENTRY, item.Key), item.Value.quoted ? $"$1'{var}'" : $"$1{var}");
            }

            File.WriteAllLines(cfg, newLines);
        }

        public void listCfg(string[] lines, string searchSect)
        {
            string curSection = null;

            Console.WriteLine("Listing xemu.toml settings..");

            if (!string.IsNullOrEmpty(searchSect))
                Console.WriteLine($"Searching for section: {searchSect}");

            foreach (string line in lines)
            {
                // try match line with section title
                if (matchSetting(line, PATTERN_SECTION_TITLE, out string section, out _))
                {
                    if (searchSect == null || section.StartsWith(searchSect))
                        Console.WriteLine($"\n[{section}]");

                    curSection = section;

                    continue;
                }

                if (searchSect != null && (!curSection?.StartsWith(searchSect) ?? true))
                    continue;

                string entry;
                string value;

                // try match line with quotes around value
                if (matchSetting(line, PATTERN, out entry, out value))
                {
                    Console.WriteLine($"{entry} = '{value}'");
                    continue;
                }

                // try match line without quotes around value
                if (matchSetting(line, PATTERN_NO_QUOTES, out entry, out value))
                {
                    Console.WriteLine($"{entry} = {value}");
                    continue;
                }
            }
        }
        
        public bool matchSetting(string input, string pattern, out string setting, out string value)
        {
            Match match = Regex.Match(input, pattern);

            if (match.Success)
            {
                setting = match.Groups[1].Value;
                value = match.Groups[2].Value;
                return true;
            }

            setting = null;
            value = null;
            return false;
        }
        
        public bool checkVar(string env, out string var)
        {
            var = Environment.GetEnvironmentVariable(env);
            return var != null;
        }
        public void getEnv()
        {
            // XEMU_DIR
            if (!checkVar("XEMU_DIR", out dir))
                dir = Environment.CurrentDirectory;

            // XEMU_EXE
            if (!checkVar("XEMU_EXE", out exe))
                exe = "xemu.exe";

            if (!fileExists(exe))
                throw new Exception($"{exe} not found.");

            // XEMU_CFG
            if (!checkVar("XEMU_CFG", out cfg))
                throw new Exception("XEMU_CFG not defined.");

            if (!fileExists(cfg))
            {
                getDefaultConfig();
            }

            // XEMU_SERIAL
            if (checkVar("XEMU_SERIAL", out string serialStr))
            {
                if (!int.TryParse(serialStr.Trim(), out serial))
                {
                    serial = 0;
                }

                serial = Math.Min(Math.Max(serial, 0), 3);
            }

            // XEMU_DVD
            if (checkVar("XEMU_DVD", out dvd))
            {
                if (dvd != "null" && !fileExists(dvd))
                    dvd = null;
            }

            // XEMU_POS
            if (checkVar("XEMU_POS", out string pos))
            {
                if (!int.TryParse(pos, out startupPosition))
                {
                    startupPosition = 0;
                }

                startupPosition = Math.Min(Math.Max(startupPosition, 0), 4);

            }

            // XEMU_SIZE
            startupSizeOk = checkVar("XEMU_SIZE", out string size);

            if (startupSizeOk)
            {
                if (!Regex.IsMatch(size, PATTERN_RESOLUTION))
                    size = "640x480";

                string[] res = size.Split('x');
                startupWidth = int.Parse(res[0]);
                startupHeight = int.Parse(res[1]);
            }
        }
        public void getDefaultConfig()
        {
            // xemu.toml not found, copy from roaming\xemu\xemu\xeum.toml
            string defaultCfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "xemu\\xemu\\xemu.toml");

            if (!File.Exists(defaultCfg))
            {
                File.Create(cfg).Close();
                Console.WriteLine("Created new cfg file");
                return;
            }

            File.Copy(defaultCfg, cfg);
            Console.WriteLine("Copied cfg file");
        }

        public bool fileExists(string path)
        {
            return File.Exists(path) || File.Exists(Path.Combine(dir, path));
        }

        public void closeCmd()
        {
            if (cmdProc != null)
            {
                if (!cmdProc.HasExited)
                    cmdProc.Kill();

                cmdProc.Dispose();
                cmdProc = null;
            }
        }
        public void closeXemu()
        {
            if (xemuProc != null)
            {
                xemuProc.OutputDataReceived -= null;

                if (!xemuProc.CloseMainWindow())
                {
                    if (!xemuProc.HasExited)
                        xemuProc.Kill();
                }

                xemuProc.Dispose();
                xemuProc = null;
            }
        }

        // EVENT HANDLERS
        private void xemu_outputReceived(object sender, DataReceivedEventArgs e)
        {
            string data = e.Data;

            if (data == null)
            {
                return;
            }

            if (!sw.outputQEMU_cli && data.StartsWith("Created QEMU launch parameters"))
            {
                return;
            }

            Console.WriteLine(data);
        }
        public void cmdExited(object sender, EventArgs e)
        {
            // cmd process has exited, close xemu if it's still open.
            closeXemu();
        }
        private void xemuExited(object sender, EventArgs e)
        {
            // xemu has exited, close the cmd window if it's still open.
            closeCmd();
        }
    }
}
