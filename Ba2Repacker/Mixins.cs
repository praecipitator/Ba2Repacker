namespace Ba2Repacker
{
    internal static class Mixins
    {
        public static string SubtractSuffix(this string str, string suffix)
        {
            var suffixLength = suffix.Length;
            var maybeSuffix = str[^suffixLength..];

            if (maybeSuffix == suffix)
            {
                return str[0..^suffixLength];
            }

            return str;
        }
    }
}
