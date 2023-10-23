using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ba2Repacker.FileSystem
{
    internal class RealFileSystem : IFileSystem
    {
        virtual public bool CopyFile(string src, string dst, bool overwrite = false)
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

        virtual public bool DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error deleting " + path+ ": " + e.Message);
                return false;
            }
            return true;
        }

        virtual public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        virtual public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        virtual public bool MkDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error creating " + path + ": " + e.Message);
            }

            return true;
        }

        virtual public bool RenameFile(string src, string dst, bool overwrite = false)
        {
            try
            {
                File.Move(src, dst, overwrite);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error renaming " + src+" to " +dst + ": " + e.Message);
                return false;
            }
            return true;

        }

        virtual public string ResolvePath(string path)
        {
            // passthrough
            return path;
        }

        virtual public string CombinePath(params string[] parts)
        {
            if(parts.Length == 0)
            {
                return "";
            }

            if(parts.Length == 1)
            {
                // passthrough
                return ResolvePath(parts[0]);
            }

            return ResolvePath(Path.Combine(parts));
        }

        public bool IsReadable(string path)
        {
            if(!FileExists(path))
            {
                return false;
            }
            // add some extra check here, because MO2
            using var fs = new FileStream(path, FileMode.Open);
            return fs.CanRead;
        }
    }
}
