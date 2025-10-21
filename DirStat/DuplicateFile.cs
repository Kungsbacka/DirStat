using System.Collections.Generic;

namespace DirStat
{
    internal static class DuplicateFile
    {
        private static Dictionary<string, List<string>> _dict = new Dictionary<string, List<string>>();

        public static void Add(string fileName, string filePath, long fileSize)
        {
            string id = fileName + fileSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!_dict.TryGetValue(id, out List<string> list))
            {
                list = new List<string>();
                _dict.Add(id, list);
            }
            list.Add(filePath);
        }


    }
}
