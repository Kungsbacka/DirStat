using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DirStat
{
    public static class DuplicateFile
    {
        private static Dictionary<string, List<string>> _dict = new Dictionary<string, List<string>>();

        public static void Add(string fileName, string filePath, long fileSize)
        {
            string id = fileName + fileSize.ToString();
            if (!_dict.TryGetValue(id, out List<string> list))
            {
                list = new List<string>();
                _dict.Add(id, list);
            }
            list.Add(filePath);
        }


    }
}
