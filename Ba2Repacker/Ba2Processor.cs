using Ba2Repacker.FileSystem;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.FormKeys.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis;
using Noggog;
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

            public string getMainBa2Name()
            {
                if (mainArchives.Count == 0)
                {
                    return "";
                }

                return mainArchives.First();
            }

            public string getTextureBa2Name()
            {
                if (textureArchives.Count == 0)
                {
                    return "";
                }

                return textureArchives.First();
            }

            public bool countsAsMaster()
            {
                var lowerExt = modKey.FileName.Extension.ToLower();
                return isMaster || lowerExt == ".esl" || lowerExt == ".esm";
            }

            public int getNumFiles(bool includeTextures = false)
            {
                int result = 0;
                if (countsAsMaster())
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

        private Ba2RepackProcessor repacker;

        private static HashSet<ModKey> vanillaMods = new()
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
        private List<string> ccModNames = new();
        private List<string> allBa2Names = new();

        // private string disabledPath;
        private string combinedMainArchive;
        private string combinedTextureArchive;

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

            if (cfg.mo2Settings.EnableMO2Mode)
            {
                fsWrapper = new MO2FileSystem(state.DataFolderPath, cfg.mo2Settings);
                //fsWrapper.CopyFile("F:\\SteamSSD\\steamapps\\common\\Fallout 4\\data\\meh\\testFile.txt", "F:\\SteamSSD\\steamapps\\common\\Fallout 4\\data\\meh\\testFile.foo.txt");
            }
            else
            {
                fsWrapper = new RealFileSystem();
            }

            repacker = new(state.DataFolderPath, cfg.disabledSuffix, fsWrapper, tempPath);
        }

        public void Process()
        {
            RestoreInitialState();
            loadExistingBa2s();
            loadCCCfile();
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

                    numMainFiles += curFileInfo.getNumFiles(false);
                    numTextureFiles += curFileInfo.textureArchives.Count;

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
                if (list.Count > 1)
                {
                    Console.WriteLine("Repacking a new MAIN BA2");
                    var task = repacker.repackByList(extractFileNames(list), combinedMainArchive, cancelToken);
                    tasks.Add(task);
                }
            }

            if (texTooMany > 0)
            {
                var list = sortAndLimit(getTextureArchiveList(eligibleMods), mainTooMany);
                if (list.Count > 1)
                {
                    Console.WriteLine("Repacking a new TEXTURES BA2");
                    var task = repacker.repackByList(extractFileNames(list), combinedTextureArchive, cancelToken);
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
                var mainBa2 = entry.getMainBa2Name();
                if (mainBa2 == "")
                {
                    continue;
                }
                var fullPath = fsWrapper.ResolvePath(Path.Combine(state.DataFolderPath, mainBa2));

                var data = new FileNameAndSize
                {
                    fileName = mainBa2,
                    fileSize = new System.IO.FileInfo(fullPath).Length,
                    loadOrder = getLoadOrderIndex(entry.modKey)
                };
                if (data.fileSize <= cfg.MaxFileSize * 1000000)
                {
                    result.Add(data);
                }
            }

            return result;
        }

        private List<FileNameAndSize> getTextureArchiveList(List<GenericFileInfo> list)
        {
            List<FileNameAndSize> result = new();
            foreach (var entry in list)
            {
                var textureBa2 = entry.getTextureBa2Name();
                if (textureBa2 == "")
                {
                    continue;
                }
                var fullPath = fsWrapper.ResolvePath(Path.Combine(state.DataFolderPath, textureBa2));

                var data = new FileNameAndSize
                {
                    fileName = textureBa2,
                    fileSize = new System.IO.FileInfo(fullPath).Length,
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
                isVanilla = isVanillaFile(key),
                isCC = isCCMod(key),
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
            var fullPath = Path.Combine(state.DataFolderPath, fileName);
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

        private void loadExistingBa2s()
        {
            //string[] allFiles = Directory.GetFiles(state.DataFolderPath, "*.ba2", SearchOption.TopDirectoryOnly);
            var allFiles = fsWrapper.GetDirectoryFiles(state.DataFolderPath, "*.ba2", SearchOption.TopDirectoryOnly);
            foreach (var fullPath in allFiles)
            {
                allBa2Names.Add(Path.GetFileName(fullPath));
            }
            //allBa2Names.AddRange(allFiles);
        }

        private void loadCCCfile()
        {
            var fullPath = Path.Combine(state.DataFolderPath, "..\\Fallout4.ccc");
            if (!fsWrapper.FileExists(fullPath))
            {
                return;
            }

            var lines = File.ReadAllLines(fullPath);
            /*
            foreach(var line in lines)
            {
                ccModNames.Add(line);
            }
            */
            ccModNames.AddRange(lines);
        }

        private bool hasMainBa2(ModKey mod)
        {
            string baseName = Path.GetFileNameWithoutExtension(mod.FileName);

            var fullPath = Path.Combine(state.DataFolderPath, baseName + " - Main.ba2");
            return fsWrapper.FileExists(fullPath);
        }

        private int getNumStreamingFiles(ModKey mod)
        {
            int result = 0;
            // hack
            if (mod == Fallout4.ModKey)
            {
                result = 10; // the extra files which Fallout.esm pulls
            }
            // also pull  - Voices_XX.ba2
            string fileName = mod.FileName;
            string baseName = Path.GetFileNameWithoutExtension(fileName);

            // foo - Main.ba2
            if (hasFile(baseName + " - Main.ba2"))
            {
                result++;
            }
            // foo - Geometry.csg
            if (hasFile(baseName + " - Geometry.csg"))
            {
                result++;
            }
            // foo.cdx
            if (hasFile(baseName + ".cdx"))
            {
                result++;
            }

            return result;
        }

        private bool hasFile(string fileName)
        {
            var fullPath = Path.Combine(state.DataFolderPath, fileName);
            return fsWrapper.FileExists(fullPath);
        }

        private bool isVanillaFile(ModKey mod)
        {
            return (vanillaMods.Contains(mod));
        }

        private bool isCCMod(ModKey mod)
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
