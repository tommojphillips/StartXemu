using System;
using System.IO;
using System.Text;

namespace StartXemu
{
    public class Program
    {
        // Author, tommojphillips. 06.04.2024
        // Github, https://github.com/tommojphillips

        private static StartXEMU config;
        private static XSwitch switches;

        private static void Main(string[] args)
        {
            init();

            config = new StartXEMU();

            try
            {
                parseArgs(args);
                config.run(switches);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
                Environment.ExitCode = 1;
            }

            cleanup();
        }

        private static void init()
        {
            Console.Title = "XEMU Console";
            Console.CancelKeyPress += onCancelKeyPress;
        }

        private static void parseArgs(string[] args)
        {
            switches = new XSwitch();
            bool hasSecondParam = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                hasSecondParam = args.Length > i + 1 && !args[i + 1].StartsWith("-");

                switch (arg)
                {
                    case "-?":
                        printHelp();
                        Environment.Exit(0);
                        return;

                    case "-ls":
                        if (hasSecondParam)
                        {
                            switches.ls = true;
                            switches.lsSection = args[i + 1];
                        }

                        return;
                    
                    case "-cmd":
                        switches.openPrompt = true;
                        if (hasSecondParam)
                        {
                            switches.cmd = args[i + 1];
                            i++;
                        }

                        break;

                    case "-qemu_cli":
                        switches.outputQEMU_cli = true;
                        break;

                    default:
                        Console.WriteLine($"Unknown switch: {arg}");
                        goto case "-?";
                }
            }
        }

        private static void printHelp()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("XEMU Configuration Utility");
            sb.AppendLine("# Overrides xemu settings prior to starting XEMU");
            sb.AppendLine("# Settings are only updated if the associated environment variable is defined.");
            sb.AppendLine("# Settings you want changed should be defined in the calling environment.");
            sb.AppendLine($"\nUsage --> {Path.GetFileName(Environment.GetCommandLineArgs()[0])}");
            sb.AppendLine("\nEnvironment variables:");
            sb.AppendLine("  XEMU_DIR:      - The directory where xemu is located");
            sb.AppendLine("  XEMU_CFG:      - The XEMU toml file to use");

            sb.AppendLine("  XEMU_MCPX:     - MCPX bin");
            sb.AppendLine("  XEMU_BIOS:     - BIOS bin");
            sb.AppendLine("  XEMU_EEPROM:   - eeprom bin");
            sb.AppendLine("  XEMU_HDD:      - hdd image; ( .qcow2, .img )");
            sb.AppendLine("  XEMU_DVD:      - dvd image; ( .iso )");
            
            sb.AppendLine("  XEMU_MEM:      - memory limit, 64 or 128");
            sb.AppendLine("  XEMU_VIDEO:    - video; ( hdtv, composite, vga, scart, svideo, rfu, none )");
            sb.AppendLine("  XEMU_SIZE:     - startup size; ( 640x480, 1280x720, etc )");
            sb.AppendLine("  XEMU_POS:      - position; ( CENTER=0, TOPL=1, TOPR=2, BOTL=3, BOTR=4 )");
            sb.AppendLine("  XEMU_SKIP_ANI: - skip the boot animation, 0=false, 1=true");

            sb.AppendLine("\nOptions:");
            sb.AppendLine("  -?                  - Display this help message");
            sb.AppendLine("  -ls                 - List all toml entries in the config file.");
            sb.AppendLine("  -ls <section>       - List all toml entries for a specific section");

            sb.AppendLine("  -cmd <command>      - Open a command prompt with the specified\n" +
                          "                        command while XEMU is running");

            sb.AppendLine("  -qemu_cli           - Output the QEMU command line to the console");

            Console.WriteLine(sb.ToString());
        }

        // EVENT HANDLERS
        private static void onCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cleanup();
        }

        private static void cleanup()
        {
            Console.CancelKeyPress -= null;
            
        }
    }
}
