using Ba2Repacker.IniParser;
using GameFinder.Common;
using Microsoft.Extensions.Options;
using Mutagen.Bethesda.Plugins;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Ba2Repacker.FileSystem
{
    internal class MO2FileSystem : RealFileSystem
    {
        private readonly Environment.SpecialFolder[] SpecialFolders = {
            Environment.SpecialFolder.LocalApplicationData, // local
            Environment.SpecialFolder.ApplicationData,      // roaming
            Environment.SpecialFolder.CommonApplicationData // common
        };

        private readonly Section_MO2Settings cfg;

        // "%BASE_DIR%" from the settings
        private string mo2BaseDirectory = "";
        private string mo2ProfileDirectory = "";
        private string mo2ModsDirectory = "";
        private string mo2OverwriteDirectory = "";
        private readonly string gameDataPath;
        private readonly string mo2Path;

        // this is in the reverse order already
        private readonly List<string> EnabledModNames = new();

        public MO2FileSystem(string mo2Path, string gameDataPath, Section_MO2Settings cfg)
        {
            this.mo2Path = mo2Path;
            this.gameDataPath = gameDataPath;
            this.cfg = cfg;
            Bootstrap();
        }

        /// <summary>
        /// Here, we recieve a full path valid in MO2's virtual FS, and resolve it to a real path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public override string ResolvePath(string path)
        {
            if (!path.StartsWith(gameDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            var relPath = Path.GetRelativePath(gameDataPath, path);
            if (relPath == null)
            {
                return path;
            }

            var modBaseDir = FindBasedirContaining(relPath);
            if(modBaseDir != "")
            {
                return Path.GetFullPath(relPath, modBaseDir);
            }

            return path;
        }

        private string FindBasedirContaining(string relPath)
        {
            // first, check in Override
            // var baseDir = Path.Combine(mo2OverwriteDirectory, relPath);
            var modPath = Path.GetFullPath(relPath, mo2OverwriteDirectory);
            if (File.Exists(modPath))
            {
                return mo2OverwriteDirectory;
            }

            foreach (var modName in EnabledModNames)
            {
                // var modPath = Path.Combine(mo2ModsDirectory, modName, relPath);
                var baseDir = Path.Combine(mo2ModsDirectory, modName);
                modPath = Path.GetFullPath(relPath, baseDir);
                if (File.Exists(modPath))
                {
                    return baseDir;
                }
            }

            // finally, check the actual data
            modPath = Path.GetFullPath(relPath, gameDataPath);
            if (File.Exists(modPath))
            {
                return gameDataPath;
            }

            return "";
        }

        public override bool RenameFile(string src, string dst, bool overwrite = false)
        {
            if (
                src.StartsWith(gameDataPath, StringComparison.OrdinalIgnoreCase) &&
                dst.StartsWith(gameDataPath, StringComparison.OrdinalIgnoreCase)
                )
            {
                var relPathSrc = Path.GetRelativePath(gameDataPath, src);
                var relPathDst = Path.GetRelativePath(gameDataPath, dst);
                if (relPathSrc != null && relPathDst != null)
                {
                    var modBaseDir = FindBasedirContaining(relPathSrc);
                    if(modBaseDir != "")
                    {
                        try
                        {
                            File.Move(Path.Combine(modBaseDir, relPathSrc), Path.Combine(modBaseDir, relPathDst));
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                        return true;
                    }
                }
            }

            return base.RenameFile(src, dst, overwrite);
        }

        public override List<string> GetDirectoryFiles(string inPath, string filter, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            var result = new HashSet<string>();

            if (!inPath.StartsWith(gameDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return base.GetDirectoryFiles(inPath, filter, options);
            }

            var relPath = Path.GetRelativePath(gameDataPath, inPath);
            if (relPath == null)
            {
                return base.GetDirectoryFiles(inPath, filter, options);
            }

            // first, check overwrite
            GetDirectoryFilesInternal(result, mo2OverwriteDirectory, inPath, filter, options);

            foreach (var modName in EnabledModNames)
            {
                var modPath = Path.GetFullPath(relPath, Path.Combine(mo2ModsDirectory, modName));
                if (Directory.Exists(modPath))
                {
                    GetDirectoryFilesInternal(result, modPath, inPath, filter, options);
                }
            }

            // then, check the actual data
            GetDirectoryFilesInternal(result, gameDataPath, inPath, filter, options);

            return result.ToList();
        }

        private static void GetDirectoryFilesInternal(HashSet<string> outList, string basePath, string inPath, string filter, SearchOption options)
        {
            var curStrings = Directory.GetFiles(basePath, filter, options);

            foreach (var str in curStrings)
            {
                // now, we need to subtract basePath again
                var relStr = Path.GetRelativePath(basePath, str);
                // and absolutize it for inPath
                outList.Add(Path.Combine(inPath, relStr));
            }
        }

        public override bool CopyFile(string src, string dst, bool overwrite = false)
        {
            if (
                src.StartsWith(gameDataPath, StringComparison.OrdinalIgnoreCase) &&
                dst.StartsWith(gameDataPath, StringComparison.OrdinalIgnoreCase)
                )
            {
                var relPathSrc = Path.GetRelativePath(gameDataPath, src);
                var relPathDst = Path.GetRelativePath(gameDataPath, dst);
                if (relPathSrc != null && relPathDst != null)
                {
                    var modBaseDir = FindBasedirContaining(relPathSrc);
                    if(modBaseDir != "")
                    {
                        try
                        {
                            File.Copy(Path.Combine(modBaseDir, relPathSrc), Path.Combine(modBaseDir, relPathDst));
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                        return true;
                    }
                }
            }

            return base.CopyFile(src, dst, overwrite);
        }

        private void Bootstrap()
        { 
            if(!Directory.Exists(mo2Path))
            {
                throw new FileNotFoundException("MO2's path doesn't actually exist? This shouldn't happen", mo2Path);
            }

            // here are the rules which I am going to do now:
            /*
             * - if we have an ini override, use that, no questions asked.
             * - if we have a portable.txt in mo2path, assume portable.
             * - otherwise:
             *  - check if HKEY_CURRENT_USER\Software\Mod Organizer Team\Mod Organizer\CurrentInstance a) exists and b) is not emptystring
             *      - if yes, we are in APPDATA mode
             *      - if not, we are in portable mode
             */

            // first, the override setting
            if (cfg.iniOverride != "")
            {
                // just do it, no matter what
                Console.WriteLine("Reading settings from overridden INI " + cfg.iniOverride);
                BootstrapGeneric(cfg.iniOverride, mo2Path);
                return;
            }

            // are we portable?
            if (File.Exists(Path.Combine(mo2Path, "portable.txt")))
            {
                Console.WriteLine("Reading settings from portable MO2, forced through portable.txt");
                BootstrapPortable();
                return;
            }

            // now, this
            if(HasMo2RegistryEntry())
            {
                // assume we are non-portable
                Console.WriteLine("Reading settings from MO2's global AppData folder");
                BootstrapAppData();
                return;
            }


            // finally, just assume we are portable
            Console.WriteLine("Reading settings from portable MO2");
            BootstrapPortable();
        }

        private static bool HasMo2RegistryEntry()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Mod Organizer Team\\Mod Organizer");
                if (key != null)
                {
                    Object? o = key.GetValue("CurrentInstance");
                    if (o is String currentInstanceName)
                    {
                        return currentInstanceName != "";
                    }
                }
            }
            catch (Exception)  //just for demonstration...it's always best to handle specific exceptions
            {
                return false;
            }
            return false;
        }


        /// <summary>
        /// bootstraps the MO2 filesystem from a path to a ModOrganizer.ini
        /// 
        /// </summary>
        /// <param name="mainIniPath"></param>
        /// <param name="baseDirFallback"></param>
        /// <exception cref="InvalidDataException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        private void BootstrapGeneric(string mainIniPath, string baseDirFallback = "")
        {
            var mainIniReader = new IniReader(mainIniPath);

            var gameName = mainIniReader.GetValue("General", "gameName");
            if (gameName != "Fallout 4")
            {
                throw new InvalidDataException("Cannot use " + mainIniPath + ": this patcher only supports Fallout 4, gameName = " + gameName);
            }

            mo2BaseDirectory = NormalizePath(mainIniReader.GetValueMO2("Settings", "base_directory", baseDirFallback));// for portable mode, this defaults to MO2 dir

            if (mo2BaseDirectory == "" || !Directory.Exists(mo2BaseDirectory))
            {
                throw new FileNotFoundException("MO2's base directory dir is not set or doesn't exist", mo2BaseDirectory);
            }

            LoadMO2ini(mainIniReader);
        }

        private void BootstrapPortable()
        {
            // here, we start with ModOrganizer.ini
            var mainIni = Path.Combine(mo2Path, "ModOrganizer.ini");
            if (!File.Exists(mainIni))
            {
                throw new FileNotFoundException("Failed to find MO2's configuration file", mainIni);
            }

            BootstrapGeneric(mainIni, mo2Path);
        }

        private void BootstrapAppData()
        {
            var mo2dir = FindMO2AppData();

            var f4dir = Path.Combine(mo2dir, "Fallout 4");
            if (!Directory.Exists(f4dir))
            {
                throw new FileNotFoundException("Failed to find MO2's configuration folder for Fallout 4", f4dir);
            }

            var mainIni = Path.Combine(f4dir, "ModOrganizer.ini");
            if (!File.Exists(mainIni))
            {
                throw new FileNotFoundException("Failed to find MO2's configuration file for Fallout 4", mainIni);
            }

            BootstrapGeneric(mainIni);
        }

        private void LoadMO2ini(IniReader mainIniReader)
        {
            /*
            var mainIniReader = new IniReader(mainIniPath);

            var gameName = mainIniReader.GetValue("General", "gameName");
            if(gameName != "Fallout 4")
            {
                throw new InvalidDataException("Cannot use "+mainIniPath+": this patcher only supports Fallout 4, gameName = "+gameName);
            }

            mo2BaseDirectory = NormalizePath(mainIniReader.GetValueMO2("Settings", "base_directory"));// for portable mode, this defaults to MO2 dir

            if (mo2BaseDirectory == "" || !Directory.Exists(mo2BaseDirectory))
            {
                throw new FileNotFoundException("MO2's Fallout 4 dir is not set or doesn't exist", mo2BaseDirectory);
            }
            */
            mo2ModsDirectory = NormalizePath(mainIniReader.GetValueMO2("Settings", "mod_directory", "%BASE_DIR%/mods"));
            mo2ModsDirectory = mo2ModsDirectory.Replace("%BASE_DIR%", mo2BaseDirectory);
            if (mo2ModsDirectory == "" || !Directory.Exists(mo2ModsDirectory))
            {
                throw new FileNotFoundException("MO2's Fallout 4 mod dir is not set or doesn't exist", mo2ModsDirectory);
            }

            var profilesDir = NormalizePath(mainIniReader.GetValueMO2("Settings", "profiles_directory", "%BASE_DIR%/profiles"));
            profilesDir = profilesDir.Replace("%BASE_DIR%", mo2BaseDirectory);
            if (profilesDir == "" || !Directory.Exists(profilesDir))
            {
                throw new FileNotFoundException("MO2's Fallout 4 profiles dir is not set or doesn't exist", profilesDir);
            }

            // overwrite_directory //%BASE_DIR%/overwrite
            mo2OverwriteDirectory = NormalizePath(mainIniReader.GetValueMO2("Settings", "overwrite_directory", "%BASE_DIR%/overwrite"));
            mo2OverwriteDirectory = mo2OverwriteDirectory.Replace("%BASE_DIR%", mo2BaseDirectory);
            if (mo2OverwriteDirectory == "" || !Directory.Exists(mo2OverwriteDirectory))
            {
                throw new FileNotFoundException("MO2's Fallout 4 overwrite dir is not set or doesn't exist", mo2OverwriteDirectory);
            }


            var profileName = cfg.profileOverride;
            if (cfg.profileOverride == "")
            {
                profileName = mainIniReader.GetValueMO2("General", "selected_profile");
            }
            if (profileName == "")
            {
                throw new InvalidDataException("Failed to read the currently selected MO2 profile");
            }

            // finally the actual profile
            mo2ProfileDirectory = Path.Combine(profilesDir, profileName);
            if (!Directory.Exists(mo2ProfileDirectory))
            {
                throw new FileNotFoundException("Profile folder for " + profileName + " doesn't exist", mo2ProfileDirectory);
            }

            var modListPath = Path.Combine(mo2ProfileDirectory, "modlist.txt");
            if (!File.Exists(modListPath))
            {
                throw new FileNotFoundException("modlist.txt doesn't exist!", modListPath);
            }
            ReadModList(modListPath);
        }

        

        private void ReadModList(string modListPath)
        {
            //EnabledModNames
            const Int32 BufferSize = 128;
            using var fileStream = File.OpenRead(modListPath);
            using var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize);
            String? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line == "") continue;
                if (line.StartsWith('+'))
                {
                    var modName = line[1..];
                    EnabledModNames.Add(modName);
                }
            }
        }

        public static string NormalizePath(string inputPath)
        {
            return inputPath
                .Replace("\\\\", "\\")
                .Replace('/', '\\');
        }

        private string FindMO2AppData()
        {
            foreach (var folderEntry in SpecialFolders)
            {
                var appData = Environment.GetFolderPath(folderEntry);
                if (!Directory.Exists(appData))
                {
                    continue;
                }

                var mo2path = Path.Combine(appData, "ModOrganizer");
                if (Directory.Exists(mo2path))
                {
                    return mo2path;
                }
            }

            return "";
        }

        public override long GetFileSize(string path)
        {
            return new FileInfo(ResolvePath(path)).Length;
        }
    }
}
