# XEMU Configuration Utility
- Overrides xemu settings prior to starting XEMU.
- Settings are only updated if the associated environment variable is defined.
- Settings you want changed should be defined in the calling environment.

### Environment Variables
- XEMU_DIR:   - The directory where xemu is located" (REQUIRED)
- XEMU_CFG:   - The XEMU toml file to use"           (REQUIRED)

- XEMU_MCPX      - The path to the mcpx .bin
- XEMU_BIOS      - The path to the bios .bin
- XEMU_EEPROM    - The path to the eeprom .bin
- XEMU_HDD       - The path to the hdd file ( .qcow2, .img )
- XEMU_DVD       - The path to the iso .img. (force xemu to boot with a disk)
- XEMU_MEM:      - memory limit, ( 64, 128 )
- XEMU_VIDEO:    - video output; ( hdtv, composite, vga, scart, svideo, rfu, none )
- XEMU_SIZE:     - startup size; ( 640x480, 1280x720, etc )
- XEMU_POS:      - position; ( 0=CENTER, 1=TOPL, 2=TOPR, 3=BOTL, 4=BOTR )
- XEMU_SKIP_ANI: - skip the boot animation; ( 0=false, 1=true )

### Command-Line Switches
- -?                  - Display help message
- -ls                 - List all toml entries in the config file.
- -ls {section} - List all toml entries for a specific section.
- -cmd {command}      - Open a command prompt with the specified command while XEMU is running.

- -qemu_cli           - Output the QEMU command line to the console");
