using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ba2Repacker.FileSystem
{
    internal interface IFileSystem
    {
        public bool FileExists(string path);

        public bool DirectoryExists(string path);

        public bool MkDir(string path);

        public bool RenameFile(string src, string dst, bool overwrite = false);

        public bool CopyFile(string src, string dst, bool overwrite = false);

        public bool DeleteFile(string path);

        public string ResolvePath(string path);

        public bool IsReadable(string path);

        public string CombinePath(params string[] parts);
    }
}
