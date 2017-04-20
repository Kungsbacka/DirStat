using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static System.Console;
using static DirStat.NativeMethods;
using Newtonsoft.Json;
using System.Linq;

namespace DirStat
{
    public class DirectoryItem
    {
        public string Path { get; set; }
        public List<Tag> TagList { get; set; }
        public bool GroupOnSubdirectories { get; set; }

        public DirectoryItem(string path, bool groupOnImmediateChildren)
            : this(path, null, groupOnImmediateChildren) { }

        [JsonConstructor]
        public DirectoryItem(string path, List<Tag> tagList, bool groupOnImmediateChildren)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty.");
            }
            Path = path;
            TagList = tagList;
            GroupOnSubdirectories = groupOnImmediateChildren;
        }

        public bool ShouldSerializeGroupOnSubdirectories()
        {
            return false;
        }
    }

    public class Tag
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public Tag(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    public class SizeAndCount
    {
        public long Size { get; set; }
        public long Count { get; set; }
    }

    public class FileChanged
    {
        public int DaysAgo { get; set; }
        public SizeAndCount Created { get; set; }
        public SizeAndCount Modified { get; set; }

        public FileChanged(int daysAgo)
        {
            DaysAgo = daysAgo;
            Created = new SizeAndCount();
            Modified = new SizeAndCount();
        }
    }

    public class PatternMatch
    {
        public string Pattern { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
    }

    public class FileExtension
    {
        public string Extension { get; set; }
        public long Size { get; set; }
        public long Count { get; set; }

        public FileExtension(string ext)
        {
            Extension = ext;
        }
    }

    public class AnalysisData
    {
        public DirectoryItem Directory { get; set; }
        public string LargestFilePath { get; set; }
        public long LargestFileSize { get; set; }
        public long TotalSize { get; set; }
        public long FileCount { get; set; }
        public long DirectoryCount { get; set; }
        public TimeSpan ScanTime { get; set; }
        public int ScanTimeInMS
        {
            get
            {
                return (int)Math.Round(ScanTime.TotalMilliseconds, 0);
            }
        }
        [JsonProperty(PropertyName = "LongPathList")]
        public List<string> LongPathList { get; set; }
        [JsonProperty(PropertyName = "FailedDirectoryList")]
        public List<string> FailedDirectoryList { get; set; }
        [JsonProperty(PropertyName = "PatternMatchList")]
        public List<PatternMatch> PatternMatchList { get; set; }
        [JsonIgnore]
        internal Dictionary<string, FileExtension> FileExtensionDictionary { get; set; }
        [JsonIgnore]
        internal Dictionary<int, FileChanged> FileChangedDictionary { get; set; }
        public List<FileExtension> FileExtensionList
        {
            get
            {
                return FileExtensionDictionary.Values.ToList();
            }
            set
            {
                FileExtensionDictionary = value.ToDictionary(v => v.Extension, v => v);
            }
        }
        public List<FileChanged> FileChangedList
        {
            get
            {
                return FileChangedDictionary.Values.ToList();
            }
            set
            {
                FileChangedDictionary = value.ToDictionary(v => v.DaysAgo, v => v);
            }
        }
        public AnalysisData(DirectoryItem directoryItem)
        {
            Directory = directoryItem;
            LongPathList = new List<string>();
            PatternMatchList = new List<PatternMatch>();
            FailedDirectoryList = new List<string>();
            FileExtensionDictionary = new Dictionary<string, FileExtension>();
            FileChangedDictionary = new Dictionary<int, FileChanged>();
            foreach (int days in new int[] { 1, 2, 3, 4, 5, 10, 20, 30, 182, 365, 730, 1095, 1460, 1825 })
            {
                FileChangedDictionary.Add(days, new FileChanged(days));
            }
        }
    }

    class Program
    {
        const long FileTimeIntervalsIn24Hours = 864000000000L;

        static Options _opt;

        class Pattern
        {
            public string Text;
            public bool Contains;
            public bool StartsWith;
            public bool EndsWith;
            public string OriginalText;
        }

        class Options
        {
            public List<DirectoryItem> DirectoryItemList = new List<DirectoryItem>();
            public string ErrorMessage;
            public string PatternFilePath;
            public string ListFilePath;
            public string OutputFilePath;
            public List<Pattern> PatternList;
            public bool PrintUsageAndExit;
            public bool AnalyzeFileAge = true;
            public bool AnalyzeFileExtension = true;
            public bool AnalyzeFileExtensionForPatternMatchOnly;
            public bool FormatJsonOutput;
            public bool GroupOnSubdirectories;
        }

        static Options ParseCommandLine(string[] args)
        {
            _opt = new Options();
            string directory = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i][0] == '-' || args[i][0] == '/')
                {
                    string arg = args[i].Substring(1).ToLower();
                    switch (arg)
                    {
                        case "l":
                        case "-list":
                            if (i + 1 >= args.Length)
                            {
                                _opt.ErrorMessage = "List option must be followed by a path to a file.";
                                break;
                            }
                            _opt.ListFilePath = args[++i];
                            break;
                        case "m":
                        case "-match":
                            if (i + 1 >= args.Length)
                            {
                                _opt.ErrorMessage = "Match option must be followed by a path to a file.";
                                break;
                            }
                            _opt.PatternFilePath = args[++i];
                            break;
                        case "na":
                        case "-no-age":
                            _opt.AnalyzeFileAge = false;
                            break;
                        case "ne":
                        case "-no-ext":
                            _opt.AnalyzeFileExtension = false;
                            break;
                        case "pe":
                        case "-pattern-ext":
                            _opt.AnalyzeFileExtensionForPatternMatchOnly = true;
                            break;
                        case "o":
                        case "-output-file":
                            if (i + 1 >= args.Length)
                            {
                                _opt.ErrorMessage = "Match option must be followed by a path to a file.";
                                break;
                            }
                            _opt.OutputFilePath = args[++i];
                            break;
                        case "f":
                        case "-format-json":
                            _opt.FormatJsonOutput = true;
                            break;
                        case "gs":
                        case "-group-on-subdir":
                            _opt.GroupOnSubdirectories = true;
                            break;
                        case "h":
                        case "-help":
                            _opt.PrintUsageAndExit = true;
                            break;
                        default:
                            _opt.ErrorMessage = $"Unknown option {args[i]}";
                            break;
                    }
                }
                else if (string.IsNullOrEmpty(directory))
                {
                    directory = args[i];
                }
                else
                {
                    _opt.ErrorMessage = "Directory can only be specified once";
                }
                if (string.IsNullOrEmpty(_opt.OutputFilePath))
                {
                    _opt.OutputFilePath = "data.json";
                }
                if (!string.IsNullOrEmpty(_opt.ErrorMessage))
                {
                    break;
                }
            }
            if (!string.IsNullOrEmpty(_opt.ErrorMessage))
            {
                return _opt;
            }
            if (string.IsNullOrEmpty(directory) && string.IsNullOrEmpty(_opt.ListFilePath))
            {
                _opt.ErrorMessage = "No directory specified";
            }
            else if (!_opt.AnalyzeFileExtension && _opt.AnalyzeFileExtensionForPatternMatchOnly)
            {
                _opt.ErrorMessage = "Parameters --no-ext and --pattern-ext cannot be used together";
            }
            else if (!string.IsNullOrEmpty(directory))
            {
                _opt.DirectoryItemList.Add(new DirectoryItem(directory, _opt.GroupOnSubdirectories));
            }
            return _opt;
        }

        static void PrintUsage()
        {
            var assemblyName = System.Reflection.Assembly.GetEntryAssembly().GetName();
            WriteLine();
            WriteLine($"{assemblyName.Name} v{assemblyName.Version.Major}.{assemblyName.Version.Minor} - analyze files and directories");
            WriteLine("Copyright (C) 2015-2016 Jonas Sjömark");
            WriteLine("Kungsbacka kommun - www.kungsbacka.se");
            WriteLine();
            WriteLine($"usage: {assemblyName.Name} [options] <directory>");
            WriteLine("  -l, --list <file>");
            WriteLine("      read paths to scan from a file, one path per line. If both a list and");
            WriteLine("      a directory is specified, the directory is added to the list");
            WriteLine();
            WriteLine("  -m, --match <file>");
            WriteLine("      read patterns from a file, one pattern per line. The patterns are");
            WriteLine("      case insensitive and can begin and/or end with an asterisk (*) for");
            WriteLine("      wilcard matching: abc => \"is exactly abc\", *abc => \"begins with abc\",");
            WriteLine("      abc* => \"ends with abc\", *abc* => \"contains abc\"");
            WriteLine("      If a file matches multiple patterns, only the first match is reported.");
            WriteLine();
            WriteLine("  -na, --no-age");
            WriteLine("      leave out file age statisticts in the result.");
            WriteLine();
            WriteLine("  -ne, --no-ext");
            WriteLine("      leave out file extension statistics in the result.");
            WriteLine();
            WriteLine("  -pe, --pattern-ext");
            WriteLine("      if pattern matching is enabled, only extensions for files that");
            WriteLine("      match a pattern gets added to the file extension statistics.");
            WriteLine("      This parameter and the --no-ext parameter cannot be used together.");
            WriteLine();
            WriteLine("  -gs, --group-on-subdir");
            WriteLine("      Subdirectories immediately below the specified directory are reported");
            WriteLine("      seperately.");
            WriteLine();
            WriteLine("  -o, --output-file <file>");
            WriteLine("      results are written to this file. If this parameter is omitted, the results");
            WriteLine("      are written to data.csv.");
            WriteLine();
            WriteLine("  -f, --format-json");
            WriteLine("      adds line breaks and indentation to JSON output.");
            WriteLine();
            WriteLine("  -h, --help");
            WriteLine("      show help and exit");
            WriteLine();
        }

        static void PrintError(string error)
        {
            PrintUsage();
            WriteLine($"ERROR: {error}");
            WriteLine();
#if DEBUG
            ReadLine();
#endif
        }

        static int Main(string[] args)
        {
            _opt = ParseCommandLine(args);
            if (_opt.PrintUsageAndExit)
            {
                PrintUsage();
                return 0;
            }
            if (!string.IsNullOrEmpty(_opt.ErrorMessage))
            {
                PrintError(_opt.ErrorMessage);
                return 1;
            }
            // Read directory list file
            if (!string.IsNullOrEmpty(_opt.ListFilePath))
            {
                string content;
                try
                {
                    content = File.ReadAllText(_opt.ListFilePath);
                }
                catch (FileNotFoundException)
                {
                    PrintError($"File not found: {_opt.ListFilePath}");
                    return 1;
                }
                _opt.DirectoryItemList = JsonConvert.DeserializeObject<List<DirectoryItem>>(content);
            }
            // Read pattern file
            if (!string.IsNullOrEmpty(_opt.PatternFilePath))
            {
                string[] lines = null;
                try
                {
                    lines = File.ReadAllLines(_opt.PatternFilePath);
                }
                catch (FileNotFoundException)
                {
                    PrintError($"File not found: {_opt.PatternFilePath}");
                    return 1;
                }
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    var pattern = new Pattern();
                    pattern.OriginalText = line;
                    if (line[0] == '*' && line[line.Length - 1] == '*')
                    {
                        pattern.Text = line.Trim('*').TrimEnd(' ');
                        pattern.Contains = true;
                    }
                    else if (line[0] == '*')
                    {
                        pattern.Text = line.TrimStart('*').TrimEnd(' ');
                        pattern.EndsWith = true;
                    }
                    else if (line[line.Length - 1] == '*')
                    {
                        pattern.Text = line.TrimEnd('*').TrimEnd(' ');
                        pattern.StartsWith = true;
                    }
                    else
                    {
                        pattern.Text = line.TrimEnd(' ');
                    }
                    if (!string.IsNullOrEmpty(pattern.Text))
                    {
                        if (null == _opt.PatternList)
                        {
                            _opt.PatternList = new List<Pattern>();
                        }
                        _opt.PatternList.Add(pattern);
                    }
                }
            }
            var directoryItemList = new List<DirectoryItem>(_opt.DirectoryItemList.Count);
            foreach (var directoryItem in _opt.DirectoryItemList)
            {
                if (directoryItem.GroupOnSubdirectories)
                {
                    foreach (var subdirectory in Directory.EnumerateDirectories(directoryItem.Path))
                    {
                        directoryItemList.Add(new DirectoryItem(Path.Combine(directoryItem.Path, subdirectory), directoryItem.TagList, false));
                    }
                }
                else
                {
                    directoryItemList.Add(directoryItem);
                }
            }
            long currentFileTime = (long)((DateTime.UtcNow - (new DateTime(1601, 1, 1))).TotalMilliseconds * 10000);
            WIN32_FIND_DATA findFileData = new WIN32_FIND_DATA();
            var stack = new Stack<string>();
            var stopwatch = new Stopwatch();
            var analyisDataList = new List<AnalysisData>();
            foreach (var directoryItem in directoryItemList)
            {
                var analysisData = new AnalysisData(directoryItem);
                stack.Clear();
                stack.Push(directoryItem.Path.TrimEnd('\\'));
                stopwatch.Restart();
                while (stack.Count > 0)
                {
                    string path = stack.Pop();
                    IntPtr hFind = FindFirstFileEx(
                        path + @"\*",
                        FINDEX_INFO_LEVELS.FindExInfoBasic,
                        findFileData,
                        FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                        IntPtr.Zero,
                        FIND_FIRST_EX_LARGE_FETCH
                    );
                    if (hFind.ToInt64() == -1)
                    {
                        analysisData.FailedDirectoryList.Add(path);
                    }
                    else
                    {
                        do
                        {
                            string fullPath = $"{path}\\{findFileData.cFileName}";
                            if (fullPath.Length > 260)
                            {
                                analysisData.LongPathList.Add(fullPath);
                            }
                            if ((findFileData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY)
                            {
                                if (findFileData.cFileName != "." && findFileData.cFileName != "..")
                                {
                                    analysisData.DirectoryCount++;
                                    stack.Push(fullPath);
                                }
                            }
                            else
                            {
                                long fileSize = (long)findFileData.nFileSizeHigh << 32 | findFileData.nFileSizeLow & 0xFFFFFFFFL;
                                long createdFileTime = (long)((ulong)findFileData.ftCreationTime_dwHighDateTime << 32 |
                                        findFileData.ftCreationTime_dwLowDateTime);
                                long modifiedFileTime = (long)((ulong)findFileData.ftLastWriteTime_dwHighDateTime << 32 |
                                        findFileData.ftLastWriteTime_dwLowDateTime);
                                if (fileSize > analysisData.LargestFileSize)
                                {
                                    analysisData.LargestFileSize = fileSize;
                                    analysisData.LargestFilePath = fullPath;
                                }
                                analysisData.TotalSize += fileSize;
                                analysisData.FileCount++;
                                bool patternMatch = false;
                                if (null != _opt.PatternList)
                                {
                                    foreach (var pattern in _opt.PatternList)
                                    {
                                        if (pattern.Contains && findFileData.cFileName.IndexOf(pattern.Text, StringComparison.OrdinalIgnoreCase) > -1)
                                        {
                                            patternMatch = true;
                                        }
                                        else if (pattern.EndsWith && findFileData.cFileName.EndsWith(pattern.Text, StringComparison.OrdinalIgnoreCase))
                                        {
                                            patternMatch = true;
                                        }
                                        else if (pattern.StartsWith && findFileData.cFileName.StartsWith(pattern.Text, StringComparison.OrdinalIgnoreCase))
                                        {
                                            patternMatch = true;
                                        }
                                        else if (findFileData.cFileName.Equals(pattern.Text, StringComparison.OrdinalIgnoreCase))
                                        {
                                            patternMatch = true;
                                        }
                                        if (patternMatch)
                                        {
                                            analysisData.PatternMatchList.Add(new PatternMatch
                                            {
                                                Pattern = pattern.OriginalText,
                                                Path = fullPath,
                                                Size = fileSize,
                                                Created = DateTime.FromFileTime(createdFileTime),
                                                Modified = DateTime.FromFileTime(modifiedFileTime)
                                            });
                                            break;
                                        }
                                    }
                                }
                                if (_opt.AnalyzeFileExtension)
                                {
                                    if (!_opt.AnalyzeFileExtensionForPatternMatchOnly || (_opt.AnalyzeFileExtensionForPatternMatchOnly && patternMatch))
                                    {
                                        string ext = Path.GetExtension(findFileData.cFileName).ToLower();
                                        FileExtension fileExtension;
                                        if (!analysisData.FileExtensionDictionary.TryGetValue(ext, out fileExtension))
                                        {
                                            fileExtension = new FileExtension(ext);
                                            analysisData.FileExtensionDictionary.Add(ext, fileExtension);
                                        }
                                        fileExtension.Count++;
                                        fileExtension.Size += fileSize;
                                    }
                                }
                                if (_opt.AnalyzeFileAge)
                                {
                                    int createdDaysAgo = (int)((currentFileTime - createdFileTime) / FileTimeIntervalsIn24Hours);
                                    int modifiedDaysAgo = (int)((currentFileTime - modifiedFileTime) / FileTimeIntervalsIn24Hours);
                                    foreach (var fileAccess in analysisData.FileChangedDictionary)
                                    {
                                        if (fileAccess.Key <= createdDaysAgo)
                                        {
                                            fileAccess.Value.Created.Count++;
                                            fileAccess.Value.Created.Size += fileSize;
                                        }
                                        if (createdFileTime != modifiedFileTime && fileAccess.Key <= modifiedDaysAgo)
                                        {
                                            fileAccess.Value.Modified.Count++;
                                            fileAccess.Value.Modified.Size += fileSize;
                                        }
                                    }
                                }
                            }
                        }
                        while (FindNextFile(hFind, findFileData));
                        FindClose(hFind);
                    }
                }
                analysisData.ScanTime = stopwatch.Elapsed;
                analyisDataList.Add(analysisData);
            }
            try
            {
                using (var streamWriter = new StreamWriter(_opt.OutputFilePath, false, System.Text.Encoding.UTF8))
                {
                    var jsonSerializer = new JsonSerializer();
                    if (_opt.FormatJsonOutput)
                    {
                        jsonSerializer.Formatting = Formatting.Indented;
                    }
                    jsonSerializer.Serialize(streamWriter, analyisDataList);
                }
            }
            catch (IOException)
            {
                PrintError($"Failed to write analysis data to {_opt.OutputFilePath}");
                return 1;
            }
            return 0;
        }
    }
}
