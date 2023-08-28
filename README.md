# DVOwnership

## Development Environment Setup

### Game Install Path Symlink

The Visual Studio project is configured to use references to the DLLs in the game installation directory via a symbolic link. This is done to maximize the portability of the project. To set up this symlink:

1. Run Command Prompt as Administrator
    - This is required to use `mklink`
2. Change directory to the root of this repository
3. Run `mklink /D dv "<Derail_Valley_install_path>"`
    - For many, the Derail Valley install path will be `C:\Program Files (x86)\Steam\steamapps\common\DerailValley`
