using Ba2Repacker.FileSystem;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.FormKeys.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Ba2Repacker
{
    internal class Ba2Processor
    {
        internal struct FileNameAndSize
        {
            public string fileName = "";
            public long fileSize = 0;
            public int loadOrder = 0;

            public FileNameAndSize()
            {
            }
        }

        internal struct GenericFileInfo
        {
            public readonly ModKey modKey;
            public bool isMaster = false;
            public bool isLight = false;
            public bool isVanilla = false;
            public bool isCC = false;
            public readonly HashSet<string> textureArchives = new();
            public readonly HashSet<string> mainArchives = new();
            public readonly HashSet<string> otherFiles = new();

            public GenericFileInfo(ModKey modKey)
            {
                this.modKey = modKey;
            }

            public readonly string GetMainBa2Name()
            {
                if (mainArchives.Count == 0)
                {
                    return "";
                }

                return mainArchives.First();
            }

            public readonly string GetTextureBa2Name()
            {
                if (textureArchives.Count == 0)
                {
                    return "";
                }

                return textureArchives.First();
            }

            public readonly bool CountsAsMaster()
            {
                var lowerExt = modKey.FileName.Extension.ToLower();
                return isMaster || lowerExt == ".esl" || lowerExt == ".esm";
            }

            public readonly int GetNumFiles(bool includeTextures = false)
            {
                int result = 0;
                if (CountsAsMaster())
                {
                    // count self
                    result += 1;
                }

                if (includeTextures)
                {
                    result += textureArchives.Count;
                }

                return result + mainArchives.Count + otherFiles.Count;
            }
        }

        private readonly Ba2RepackProcessor repacker;

        private const string MO2_DLL_x64 = "usvfs_x64.dll";
        private const string MO2_DLL_x86 = "usvfs_x86.dll";

        private static readonly HashSet<ModKey> vanillaMods = new()
        {
            Fallout4.ModKey,
            Robot.ModKey,
            Coast.ModKey,
            NukaWorld.ModKey,
            Workshop01.ModKey,
            Workshop02.ModKey,
            Workshop03.ModKey
        };

        private readonly IPatcherState<IFallout4Mod, IFallout4ModGetter> state;
        private readonly Settings cfg;
        private readonly List<string> ccModNames = new();
        private readonly List<string> allBa2Names = new();

        // private string disabledPath;
        private readonly string combinedMainArchive;
        private readonly string combinedTextureArchive;

        private readonly IFileSystem fsWrapper;
        private static readonly Regex MATCH_TEXTURE_NAME = new(@" - Textures[0-9]*\.ba2$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Ba2Processor(IPatcherState<IFallout4Mod, IFallout4ModGetter> state, Settings cfg)
        {
            this.state = state;
            this.cfg = cfg;

            var myBase = Path.GetFileNameWithoutExtension(state.OutputPath);

            combinedMainArchive = myBase + " - Main.ba2";
            combinedTextureArchive = myBase + " - Textures.ba2";

            // disabledPath = Path.Combine(state.DataFolderPath, cfg.disabledDir);

            var tempPath = Path.Combine(Path.GetTempPath(), "BA2_Repacker_temp");
            fsWrapper = CreateFileSystem();

            repacker = new(state.DataFolderPath, cfg.disabledSuffix, fsWrapper, tempPath);
        }

        private IFileSystem CreateFileSystem()
        {
            if(!cfg.mo2Settings.useAutoMO2mode)
            {
                Console.WriteLine(string.Format("MO2 mode disabled per settings"));
                return new RealFileSystem();
            }

            var mo2Path = GetMO2Path();
            if(mo2Path == "")
            {
                Console.WriteLine(string.Format("No MO2 detected"));
                return new RealFileSystem();
            }

            Console.WriteLine(string.Format("Detected MO2 run from "+mo2Path));
            
            return new MO2FileSystem(mo2Path, state.DataFolderPath, cfg.mo2Settings);
        }

        private static string GetMO2Path()
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            foreach (ProcessModule module in proc.Modules)
            {
                
                var procFileName = module.FileName;
                if(procFileName == null)
                {
                    continue;
                }
                    var curFile = Path.GetFileName(procFileName);
                if (curFile == MO2_DLL_x64 || curFile == MO2_DLL_x86)
                {
                    return Path.GetDirectoryName(procFileName) ?? "";
                }
            }

            return "";
        }

        public void Process()
        {
            RestoreInitialState();
            LoadExistingBa2s();
            LoadCCCfile();
            // var patchFileName = state.PatchMod.ModKey.FileName.String;
            int numMainFiles = 0;
            int numTextureFiles = 0;
            List<GenericFileInfo> eligibleMods = new();

            foreach (var entry in state.LoadOrder)
            {
                var key = entry.Key;
                var curMod = entry.Value.Mod;
                if (curMod != null)
                {
                    var curFileInfo = GetFileInfo(curMod);
                    numMainFiles += curFileInfo.GetNumFiles(false);
                    numTextureFiles += curFileInfo.textureArchives.Count;
                    //Console.WriteLine("File "+key+" has "+ numMainFiles+" main files");

                    if (curFileInfo.isVanilla)
                    {
                        continue;
                    }

                    if (cfg.skipCCmods && curFileInfo.isCC)
                    {
                        continue;
                    }

                    if (cfg.whitelistMode)
                    {
                        if (cfg.modWhitelist.Contains(curMod.ModKey))
                        {
                            eligibleMods.Add(curFileInfo);
                        }
                    }
                    else
                    {
                        if (!cfg.modBlacklist.Contains(curMod.ModKey))
                        {
                            eligibleMods.Add(curFileInfo);
                        }
                    }
                }
            }

            Console.WriteLine("Main files: " + numMainFiles + ", Texture Files: " + numTextureFiles);
            Console.WriteLine("We have " + (eligibleMods.Count) + " eligible mods");


            var mainTooMany = numMainFiles - cfg.Ba2Limit;
            var texTooMany = numTextureFiles - cfg.TextureLimit;

            Console.WriteLine("Main files over limit: " + mainTooMany + ", Texture Files over limit: " + texTooMany);

            List<Task> tasks = new();

            var cancelToken = new CancellationToken();

            if (mainTooMany > 0)
            {
                var list = sortAndLimit(getMainArchiveList(eligibleMods), mainTooMany);
                // Console.WriteLine("OK, list = "+list.Count);
                if (list.Count > 1)
                {
                    Console.WriteLine("Repacking a new MAIN BA2");
                    var task = repacker.RepackByList(extractFileNames(list), combinedMainArchive, cancelToken);
                    tasks.Add(task);
                }
            }

            if (texTooMany > 0)
            {
                var list = sortAndLimit(getTextureArchiveList(eligibleMods), mainTooMany);
                if (list.Count > 1)
                {
                    Console.WriteLine("Repacking a new TEXTURES BA2");
                    var task = repacker.RepackByList(extractFileNames(list), combinedTextureArchive, cancelToken);
                    tasks.Add(task);
                }
            }

            if (tasks.Count > 0)
            {
                Console.WriteLine("Waiting for " + tasks.Count + " tasks");
                Task.WhenAll(tasks).Wait();
                Console.WriteLine("Repacking finished");
            }
            else
            {
                Console.WriteLine("Nothing to repack");
            }
        }

        private int getLoadOrderIndex(ModKey mod)
        {
            return state.LoadOrder.IndexOf(mod);
            // what's even the point of passing ModKey to FindIndex? the R isn't even used...
            //return state.RawLoadOrder.FindIndex<ILoadOrderListingGetter, ModKey>(foo => (foo.ModKey == mod));
        }

        private List<string> extractFileNames(List<FileNameAndSize> inList)
        {
            return inList.Select(nas => nas.fileName).ToList();
        }

        private List<FileNameAndSize> sortBySizeAndLimit(List<FileNameAndSize> inList, int count)
        {
            var part1 = inList.OrderBy(fs => fs.fileSize).ToList();

            if (part1.Count <= count)
            {
                return part1;
            }
            return part1.GetRange(0, count);
        }

        /// <summary>
        /// This should return a list which is:
        ///     1. sorted by filesize ASC
        ///     2. trimmed to at most `count` entries
        ///     3. sorted again, by load order DESC
        /// </summary>
        /// <param name="inList"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private List<FileNameAndSize> sortAndLimit(List<FileNameAndSize> inList, int count)
        {
            var preSorted = sortBySizeAndLimit(inList, count);

            return preSorted.OrderBy(fs => fs.loadOrder).Reverse().ToList();
        }

        private List<FileNameAndSize> getMainArchiveList(List<GenericFileInfo> list)
        {
            List<FileNameAndSize> result = new();
            foreach (var entry in list)
            {
                var mainBa2 = entry.GetMainBa2Name();
                if (mainBa2 == "")
                {
                    Console.WriteLine("Skipping " + entry.modKey + " because no Main BA2");
                    continue;
                }
                var fullPath = Path.Combine(state.DataFolderPath, mainBa2);

                var data = new FileNameAndSize
                {
                    fileName = mainBa2,
                    fileSize = fsWrapper.GetFileSize(fullPath),
                    loadOrder = getLoadOrderIndex(entry.modKey)
                };
                if (data.fileSize <= cfg.MaxFileSize * 1000000)
                {
                    result.Add(data);
                } else
                {
                    Console.WriteLine("Skipping " + entry.modKey + " because too large");
                }
            }

            Console.WriteLine("getMainArchiveList returns " + result.Count+ " entries");
            return result;
        }

        private List<FileNameAndSize> getTextureArchiveList(List<GenericFileInfo> list)
        {
            List<FileNameAndSize> result = new();
            foreach (var entry in list)
            {
                var textureBa2 = entry.GetTextureBa2Name();
                if (textureBa2 == "")
                {
                    continue;
                }
                var fullPath = Path.Combine(state.DataFolderPath, textureBa2);

                var data = new FileNameAndSize
                {
                    fileName = textureBa2,
                    fileSize = fsWrapper.GetFileSize(fullPath),//new System.IO.FileInfo(fullPath).Length,
                    loadOrder = getLoadOrderIndex(entry.modKey)
                };
                if (data.fileSize <= cfg.MaxFileSize)
                {
                    result.Add(data);
                }
            }

            return result;
        }

        private GenericFileInfo GetFileInfo(IFallout4ModGetter modGetter)
        {
            ModKey key = modGetter.ModKey;
            GenericFileInfo result = new(key)
            {
                isVanilla = IsVanillaFile(key),
                isCC = IsCCMod(key),
                isMaster = modGetter.ModHeader.Flags.HasFlag(Fallout4ModHeader.HeaderFlag.Master),
                isLight = modGetter.ModHeader.Flags.HasFlag(Fallout4ModHeader.HeaderFlag.LightMaster)
            };

            string baseName = Path.GetFileNameWithoutExtension(key.FileName);

            //addFileIfExists(result.mainArchives, baseName + " - Main.ba2");
            //addFileIfExists(result.textureArchives, baseName + " - Textures.ba2");

            addFileIfExists(result.otherFiles, baseName + " - Geometry.csg");
            addFileIfExists(result.otherFiles, baseName + ".cdx");

            // var ba2Base = baseName + " - ";

            // fill the BA2s using allBa2Names
            var extra = getArchivesForFile(baseName, !result.isVanilla);//allBa2Names.Where(entry => entry.StartsWith(ba2Base));
            // separate extra into textures and non-textures
            foreach (var entry in extra)
            {
                if (MATCH_TEXTURE_NAME.IsMatch(entry))
                {
                    result.textureArchives.Add(entry);
                }
                else
                {
                    result.mainArchives.Add(entry);
                }
            }

            return result;
        }

        private IEnumerable<string> getArchivesForFile(string baseName, bool exactMatchOnly = true)
        {
            var ba2Base = baseName + " - ";
            if (!exactMatchOnly)
            {
                return allBa2Names.Where(entry => entry.StartsWith(ba2Base, StringComparison.OrdinalIgnoreCase));
            }

            // otherwise, only Main and Textures count
            var result = new List<string>();
            var mainName = ba2Base + "Main.ba2";
            var texName = ba2Base + "Textures.ba2";

            if (allBa2Names.Contains(mainName, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(mainName);
            }

            if (allBa2Names.Contains(texName, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(texName);
            }

            return result;
        }

        private void addFileIfExists(HashSet<string> targetList, string fileName)
        {
            var fullPath = fsWrapper.CombinePath(state.DataFolderPath, fileName);
            if (!fsWrapper.FileExists(fullPath))
            {
                return;
            }
            targetList.Add(fileName);
        }

        private void RestoreInitialState()
        {
            DeleteIfExists(combinedMainArchive);
            DeleteIfExists(combinedTextureArchive);

            // disabledPath
            var allFiles = fsWrapper.GetDirectoryFiles(state.DataFolderPath, "*.ba2" + cfg.disabledSuffix, SearchOption.TopDirectoryOnly);
            //string[] allFiles = Directory.GetFiles(state.DataFolderPath, "*.ba2"+cfg.disabledSuffix, SearchOption.TopDirectoryOnly);
            foreach (var srcFullPath in allFiles)
            {
                var fileName = Path.GetFileName(srcFullPath).SubtractSuffix(cfg.disabledSuffix);

                var dstFullPath = Path.Combine(state.DataFolderPath, fileName);

                if (fsWrapper.IsReadable(dstFullPath)) // I hope this breaks, if MO2 is acting up
                {
                    // do not overwrite. if there is a file over there, assume it comes from a mod update, and discard the .disabled one
                    fsWrapper.DeleteFile(srcFullPath);
                }
                else
                {
                    // move it back
                    fsWrapper.RenameFile(srcFullPath, dstFullPath);
                }
            }
        }

        private void DeleteIfExists(string fileName)
        {
            var fullPath = Path.Combine(state.DataFolderPath, fileName);
            if (fsWrapper.IsReadable(fullPath))
            {
                fsWrapper.DeleteFile(fullPath);
            }
        }

        private void LoadExistingBa2s()
        {
            //string[] allFiles = Directory.GetFiles(state.DataFolderPath, "*.ba2", SearchOption.TopDirectoryOnly);
            var allFiles = fsWrapper.GetDirectoryFiles(state.DataFolderPath, "*.ba2", SearchOption.TopDirectoryOnly);
            foreach (var fullPath in allFiles)
            {
                allBa2Names.Add(Path.GetFileName(fullPath));
            }
            //allBa2Names.AddRange(allFiles);
        }

        private void LoadCCCfile()
        {
            var fullPath = Path.Combine(state.DataFolderPath, "..\\Fallout4.ccc");
            if (!fsWrapper.FileExists(fullPath))
            {
                return;
            }

            var lines = File.ReadAllLines(fullPath);

            ccModNames.AddRange(lines);
        }
        
        private static bool IsVanillaFile(ModKey mod)
        {
            return (vanillaMods.Contains(mod));
        }

        private bool IsCCMod(ModKey mod)
        {
            if (ccModNames.Count == 0)
            {
                return false;
            }

            // well fuck you, C#/Linq/VC... for some reason, it MUST have the <string> here
            // return System.Linq.Enumerable.Contains<string>(ccModNames, mod.FileName, StringComparer.OrdinalIgnoreCase);
            // return ccModNames.Contains(mod.FileName, StringComparer.OrdinalIgnoreCase);
            return ccModNames.Contains<string>(mod.FileName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
