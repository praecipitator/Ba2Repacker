using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ba2Repacker
{
    internal static class Mixins
    {
        public static string SubtractSuffix(this string str, string suffix)
        {
            var suffixLength = suffix.Length;
            var maybeSuffix = str[^suffixLength..];

            if(maybeSuffix == suffix)
            {
                return str[0..^suffixLength];
            }

            return str;

        }
    }
}
