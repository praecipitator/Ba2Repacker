using Ba2Repacker.FileSystem;
using Wabbajack.Compression.BSA;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Ba2Repacker
{
    internal class PreprocessResult
    {
        public List<string> list;

        public BA2State archiveState;

        public PreprocessResult(BA2State archiveState, List<string> list)
        {
            this.archiveState = archiveState;
            this.list = list;
        }
    }

    internal class Ba2RepackProcessor
    {
        protected readonly TemporaryFileManager _manager;

        private readonly string dataFolder;

        private readonly string disabledSuffix;

        private readonly IFileSystem fsWrapper;

        public Ba2RepackProcessor(string dataFolder, string disabledSuffix, IFileSystem fsWrapper, string tempPath)
        {
            this.dataFolder = dataFolder;
            //this.baseName = baseName;
            this.disabledSuffix = disabledSuffix;
            // var tempPath = Path.Combine(dataFolder, "__repack_temp__");
            _manager = new TemporaryFileManager(AbsolutePath.ConvertNoFailure(tempPath));
            this.fsWrapper = fsWrapper;
        }

        public async Task RepackByList(List<String> list, string archiveName, bool isTextureMode, CancellationToken token)
        {
            if (list.Count <= 1)
            {
                return;
            }

            var typeName = isTextureMode ? "Textures" : "Main";

            var preprocessResult = await PreprocessList(list, isTextureMode, token);
            if (preprocessResult == null)
            {
                Console.WriteLine("WARNING: Could not preprocess input list for " + typeName + " archive, NOT repacking");
                return;
            }

            if (preprocessResult.list.Count <= 1)
            {
                Console.WriteLine("WARNING: Not enough valid files for repacking a " + typeName + " archive, NOT repacking");
                return;
            }

            var addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            //var firstPrevBa2 = list.First();
            // Wabbajack doesn't seem to provide a way to create the "state" anew, so, stealing it from the first BA2 we got
            //var state = await GetBa2State(firstPrevBa2);

            var path = fsWrapper.CombinePath(dataFolder, archiveName);

            if (fsWrapper.FileExists(path))
            {
                // delete it
                fsWrapper.DeleteFile(path);
            }

            var successList = new List<String>();

            await using var writer = BSADispatch.CreateBuilder(preprocessResult.archiveState, _manager);
            // how, how now?
            foreach (var fileName in preprocessResult.list)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Console.WriteLine("Repacking " + typeName + " archive " + fileName);

                var wabbaPath = AbsolutePath.ConvertNoFailure(fsWrapper.CombinePath(dataFolder, fileName));
                var reader = await BSADispatch.Open(wabbaPath);

                var readerState = reader.State;//Type=DX10

                bool addedFilesFromCurrent = false;
                foreach (Wabbajack.Compression.BSA.Interfaces.IFile readFile in reader.Files)
                {
                    // maybe normalize the path? does it even matter?
                    var pathInBa2 = readFile.Path.ToString();

                    if (addedFiles.Contains(pathInBa2))
                    {
                        // skip
                        continue;
                    }

                    addedFiles.Add(pathInBa2);

                    var fac = await readFile.GetStreamFactory(token);

                    var fileState = readFile.State;
                    if (isTextureMode)
                    {
                        if (fileState is not BA2DX10File)
                        {
                            Console.WriteLine("ERROR: Cannot add file " + pathInBa2 + " from " + fileName + ": wrong compression format");

                            continue;
                        }
                    }
                    else
                    {
                        if (fileState is not BA2File)
                        {
                            Console.WriteLine("ERROR: Cannot add file " + pathInBa2 + " from " + fileName + ": wrong compression format");

                            continue;
                        }
                    }
                    // fac.GetStream()

                    // fileState.

                    await writer.AddFile(readFile.State, await fac.GetStream(), token);

                    addedFilesFromCurrent = true;
                }
                if (addedFilesFromCurrent)
                {
                    successList.Add(fileName);
                }
                // do I need to close the reader? I hope I don't, because I don't know how
                // hopefully it happens automagically, as soon as reader goes out of scope
            }
            // now, write the thing somehow?
            var wabbaOutPath = AbsolutePath.ConvertNoFailure(path);
            await using (var outStream = wabbaOutPath.Open(FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await writer.Build(outStream, token);
            }

            // disabledFullPath
            foreach (var fileName in successList)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                var fullSrcPath = fsWrapper.CombinePath(dataFolder, fileName);
                var fullDstPath = fullSrcPath + disabledSuffix;

                fsWrapper.RenameFile(fullSrcPath, fullDstPath, true); // there shouldn't be anything to overwrite, but, just in case
            }
        }

        /**
         * Check that all files in the list are valid BA2s of the proper format, and return only those which are,
         * along with the Wabbajack archive state
         *
         * */

        private async Task<PreprocessResult?> PreprocessList(List<string> inputList, bool isTextureMode, CancellationToken token)
        {
            BA2State? resultState = null;
            List<string> resultList = new();

            foreach (var fileName in inputList)
            {
                if (token.IsCancellationRequested)
                {
                    return null;
                }

                //Console.WriteLine("Repacking file " + fileName);

                var wabbaPath = AbsolutePath.ConvertNoFailure(fsWrapper.CombinePath(dataFolder, fileName));
                var reader = await BSADispatch.Open(wabbaPath);

                if (reader.State is not BA2State stateAsBa2)
                {
                    Console.WriteLine("WARNING: File " + fileName + " is not a BA2 archive, skipping");
                    continue;
                }

                if (isTextureMode)
                {
                    if (stateAsBa2.Type != BA2EntryType.DX10)
                    {
                        Console.WriteLine("WARNING: File " + fileName + " is not a Texture archive, skipping");
                        continue;
                    }
                }
                else
                {
                    if (stateAsBa2.Type != BA2EntryType.GNRL)
                    {
                        Console.WriteLine("WARNING: File " + fileName + " is not a general file archive, skipping");
                        continue;
                    }
                }

                // finally, add as eligible
                resultState ??= stateAsBa2;
                resultList.Add(fileName);
            }

            if (null == resultState)
            {
                return null;
            }

            return new PreprocessResult(resultState, resultList);
        }
    }
}
