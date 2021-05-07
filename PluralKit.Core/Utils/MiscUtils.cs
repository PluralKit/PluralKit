using System;

namespace PluralKit.Core
{
    public static class MiscUtils
    {
        public static bool TryMatchUri(string input, out Uri uri)
        {
            try
            {
                uri = new Uri(input);
                if (!uri.IsAbsoluteUri || (uri.Scheme != "http" && uri.Scheme != "https")) 
                    return false;
            }
            catch (UriFormatException)
            {
                uri = null;
                return false;
            }

            return true;
        }
    }
}