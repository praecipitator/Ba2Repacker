using Ba2Repacker.IniParser;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ba2Repacker.FileSystem
{
    internal class MO2FileSystem: RealFileSystem
    {
        private Environment.SpecialFolder[] SpecialFolders = {
            Environment.SpecialFolder.LocalApplicationData, // local
            Environment.SpecialFolder.ApplicationData,      // roaming
            Environment.SpecialFolder.CommonApplicationData // common
        };

        private Section_MO2Settings cfg;

        private string mo2BaseDirectory = "";
        private string mo2ProfileDirectory = "";
        private string mo2ModsDirectory = "";
        private string gameDataPath;

        // this is in the reverse order already
        private readonly List<string> EnabledModNames = new();

        public MO2FileSystem(string gameDataPath, Section_MO2Settings cfg)
        {
            this.gameDataPath = gameDataPath;
            this.cfg = cfg;
            Bootstrap();
        }

        /// <summary>
        /// Here, we recieve a full path valid in MO2's virtual FS, and resolve it to a real path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        override public string ResolvePath(string path)
        {
            if(!path.StartsWith(gameDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            var relPath = Path.GetRelativePath(gameDataPath, path);
            if(relPath == null)
            {
                return path;
            }

            var targetMod = FindModContaining(relPath);
            if(targetMod != "")
            {
                return Path.Combine(mo2ModsDirectory, targetMod, relPath);
            }

            return path;
        }

        public string FindModContaining(string relPath)
        {
            foreach (var modName in EnabledModNames)
            {
                var modPath = Path.Combine(mo2ModsDirectory, modName, relPath);
                if (File.Exists(modPath))
                {
                    return modName;
                }
            }

            return "";
        }

        override public bool RenameFile(string src, string dst, bool overwrite = false)
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
                    var targetMod = FindModContaining(relPathSrc);
                    if (targetMod != "")
                    {
                        var modBaseDir = Path.Combine(mo2ModsDirectory, targetMod);

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

        override public bool CopyFile(string src, string dst, bool overwrite = false)
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
                    var targetMod = FindModContaining(relPathSrc);
                    if (targetMod != "")
                    {
                        var modBaseDir = Path.Combine(mo2ModsDirectory, targetMod);

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
            var mo2dir = findMO2AppData();

            var f4dir = Path.Combine(mo2dir, "Fallout 4");
            if(!Directory.Exists(f4dir))
            {
                throw new FileNotFoundException("Failed to find MO2's configuration folder for Fallout 4", f4dir);
            }

            var mainIni = Path.Combine(f4dir, "ModOrganizer.ini");
            if(!File.Exists(mainIni))
            {
                throw new FileNotFoundException("Failed to find MO2's configuration file for Fallout 4", mainIni);
            }

            var mainIniReader = new IniReader(mainIni);

            mo2BaseDirectory = NormalizePath(mainIniReader.GetValueMO2("Settings", "base_directory"));

            if(mo2BaseDirectory == "" || !Directory.Exists(mo2BaseDirectory))
            {
                throw new FileNotFoundException("MO2's Fallout 4 dir is empty or doesn't exist", mo2BaseDirectory);
            }

            mo2ModsDirectory = NormalizePath(mainIniReader.GetValueMO2("Settings", "mod_directory", "%BASE_DIR%/mods"));
            mo2ModsDirectory = mo2ModsDirectory.Replace("%BASE_DIR%", mo2BaseDirectory);
            if (mo2ModsDirectory == "" || !Directory.Exists(mo2ModsDirectory))
            {
                throw new FileNotFoundException("MO2's Fallout 4 mod dir is empty or doesn't exist", mo2ModsDirectory);
            }

            var profilesDir = NormalizePath(mainIniReader.GetValueMO2("Settings", "profiles_directory", "%BASE_DIR%/profiles"));//profiles_directory
            profilesDir = profilesDir.Replace("%BASE_DIR%", mo2BaseDirectory);
            if (profilesDir == "" || !Directory.Exists(profilesDir))
            {
                throw new FileNotFoundException("MO2's Fallout 4 profiles dir is empty or doesn't exist", profilesDir);
            }

            var profileName = cfg.profileOverride;
            if(cfg.profileOverride == "")
            {
                profileName = mainIniReader.GetValueMO2("General", "selected_profile");
            }
            if(profileName == "")
            {
                throw new InvalidDataException("Failed to read the currently selected MO2 profile");
            }

            // finally the actual profile
            mo2ProfileDirectory = Path.Combine(profilesDir, profileName);
            if (!Directory.Exists(mo2ProfileDirectory))
            {
                throw new FileNotFoundException("Profile folder for "+profileName+" doesn't exist", mo2ProfileDirectory);
            }

            var modListPath = Path.Combine(mo2ProfileDirectory, "modlist.txt");
            if(!File.Exists(modListPath))
            {
                throw new FileNotFoundException("modlist.txt doesn't exist!", modListPath);
            }
            ReadModList(modListPath);

            // find "C:\Users\alexg\AppData\Local\ModOrganizer"
            // in there, find "Fallout 4"
            // then, in "ModOrganizer.ini"
            /*
            [Settings]
            base_directory=F:/MO2-Games/Fallout4 
            // MAYBE other paths are also  here? 
            // maybe mod_directory?
            // cache_directory=F:/MO2-Games/Fallout4/webcache_test
            // %BASE_DIR%

            [General]
            selected_profile=@ByteArray(SS2 Playing)
            */
            // in there, profiles
            // find selected_profile
            // read modlist.txt
            // ignore lines with # and -, only with +
            // these are folders of $base_directory/mods
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
                if(line.StartsWith('+'))
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

        private string findMO2AppData()
        {
            foreach(var folderEntry in SpecialFolders)
            {
                var appData = Environment.GetFolderPath(folderEntry);
                if(!Directory.Exists(appData))
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
    }
}
