using Wabbajack.Paths;

namespace Ba2Repacker.FileSystem
{
    internal class RealFileSystem : IFileSystem
    {
        public virtual bool CopyFile(string src, string dst, bool overwrite = false)
        {
            try
            {
                File.Copy(src, dst, overwrite);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error copying " + src + " to " + dst + ": " + e.Message);
                return false;
            }
            return true;
        }

        public virtual bool DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error deleting " + path + ": " + e.Message);
                return false;
            }
            return true;
        }

        public virtual bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public virtual bool MkDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error creating " + path + ": " + e.Message);
            }

            return true;
        }

        public virtual bool RenameFile(string src, string dst, bool overwrite = false)
        {
            try
            {
                File.Move(src, dst, overwrite);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error renaming " + src + " to " + dst + ": " + e.Message);
                return false;
            }
            return true;
        }

        public virtual string ResolvePath(string path)
        {
            // passthrough
            return path;
        }

        public virtual string CombinePath(params string[] parts)
        {
            if (parts.Length == 0)
            {
                return "";
            }

            if (parts.Length == 1)
            {
                // passthrough
                return ResolvePath(parts[0]);
            }

            return ResolvePath(Path.Combine(parts));
        }

        public bool IsReadable(string path)
        {
            if (!FileExists(path))
            {
                return false;
            }
            // add some extra check here, because MO2
            using var fs = new FileStream(path, FileMode.Open);
            return fs.CanRead;
        }

        public virtual List<string> GetDirectoryFiles(string inPath, string filter, SearchOption options = SearchOption.TopDirectoryOnly)
        {
            var strings = Directory.GetFiles(inPath, filter, options);

            return strings.ToList();
        }

        public virtual long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }
    }
}
