using System.Text;
using System.Text.RegularExpressions;

namespace Ba2Repacker.IniParser
{
    internal class IniReader
    {
        private static readonly Regex EXTRACT_MO2_PARAMS = new(@"@([^()]+)\((.*)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private Dictionary<string, Dictionary<string, string>> iniData;

        public IniReader(string fileName)
        {
            iniData = new();
            ReadIni(fileName);
        }

        public Dictionary<string, string>? getSection(string name)
        {
            if (!iniData.ContainsKey(name))
            {
                return null;
            }

            return iniData[name];
        }

        public string GetValue(string section, string key, string defaultValue = "")
        {
            var dict = getSection(section);
            if (dict == null)
            {
                return defaultValue;
            }

            if (!dict.ContainsKey(key))
            {
                return defaultValue;
            }

            return dict[key];
        }

        // helpers for MO2 INIs specifically
        public string GetValueMO2(string section, string key, string defaultValue = "")
        {
            var val = GetValue(section, key, defaultValue);
            if (val == "")
            {
                return val;
            }

            // otherwise, special processing. process defaultValue, if necessary
            if (val.StartsWith('@'))
            {
                // parse this
                var match = EXTRACT_MO2_PARAMS.Match(val);
                if (match.Groups.Count == 3)
                {
                    var typeName = match.Groups[1].Value;
                    var value = match.Groups[2].Value;
                    if (typeName == "ByteArray")
                    {
                        return value;
                    }
                    // otherwise we don't know
                }
            }

            return val;
        }

        private Dictionary<string, string> GetOrCreateSection(string name)
        {
            if (!iniData.ContainsKey(name))
            {
                var newSection = new Dictionary<string, string>();
                iniData.Add(name, newSection);
                return newSection;
            }

            return iniData[name];
        }

        private void ReadIni(string fileName)
        {
            // make an empty section, just in case
            var currentSection = GetOrCreateSection("");

            const Int32 BufferSize = 128;
            using var fileStream = File.OpenRead(fileName);
            using var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize);
            String? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line == "") continue;
                if (line.StartsWith('#'))
                {
                    continue; // skip
                }
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    // section
                    var sectionName = line.Substring(1, line.Length - 2);
                    currentSection = GetOrCreateSection(sectionName);
                }
                else
                {
                    var eqIndex = line.IndexOf('=');
                    if (eqIndex < 0)
                    {
                        // bad line? take it as-is
                        currentSection.Add(line, "");
                    }
                    else
                    {
                        var key = line[..(eqIndex)];
                        var val = line[(eqIndex + 1)..];
                        currentSection.Add(key, val);
                    }
                }
            }
        }
    }
}
