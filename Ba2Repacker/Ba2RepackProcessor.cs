using Ba2Repacker.FileSystem;
using Wabbajack.Compression.BSA;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Ba2Repacker
{
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

        public async Task RepackByList(List<String> list, string archiveName, CancellationToken token)
        {
            if (list.Count <= 1)
            {
                return;
            }

            var addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var firstPrevBa2 = list.First();
            // Wabbajack doesn't seem to provide a way to create the "state" anew, so, stealing it from the first BA2 we got
            var state = await GetBa2State(firstPrevBa2);

            var path = fsWrapper.CombinePath(dataFolder, archiveName);

            if (fsWrapper.FileExists(path))
            {
                // delete it
                fsWrapper.DeleteFile(path);
            }

            await using var writer = BSADispatch.CreateBuilder(state, _manager);
            // how, how now?
            foreach (var fileName in list)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Console.WriteLine("Repacking file " + fileName);

                var wabbaPath = AbsolutePath.ConvertNoFailure(fsWrapper.CombinePath(dataFolder, fileName));
                var reader = await BSADispatch.Open(wabbaPath);
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
                    // fac.GetStream()

                    await writer.AddFile(readFile.State, await fac.GetStream(), token);

                    // var memFile = new ExtractedMemoryFile(fac);
                    // memFile.
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
            foreach (var fileName in list)
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

        private async Task<IArchive> GetBa2State(string someBa2name)
        {
            var wabbaPath = AbsolutePath.ConvertNoFailure(fsWrapper.CombinePath(dataFolder, someBa2name));
            var reader = BSADispatch.Open(wabbaPath);
            var wat = await reader;

            // do I need to close this?

            return wat.State;
        }
    }
}
