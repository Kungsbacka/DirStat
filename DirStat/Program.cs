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
        public string PatternOptions { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
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
        public string LongestFilePath { get; set; }
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


        class Options
        {
            public List<DirectoryItem> DirectoryItemList = new List<DirectoryItem>();
            public string ErrorMessage;
            public string PatternFilePath;
            public string ListFilePath;
            public string OutputFilePath;
            public bool PrintUsageAndExit;
            public bool AnalyzeFileAge = true;
            public bool AnalyzeFileExtension = true;
            public bool AnalyzeFileExtensionForPatternMatchOnly;
            public bool FormatJsonOutput;
            public bool GroupOnSubdirectories;
            public bool CheckForDuplicateFiles = true;
            public int LongPathLimit = 260;
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
                                _opt.ErrorMessage = "--list must be followed by a path to a file.";
                                break;
                            }
                            _opt.ListFilePath = args[++i];
                            break;
                        case "m":
                        case "-match":
                            if (i + 1 >= args.Length)
                            {
                                _opt.ErrorMessage = "--match must be followed by a path to a file.";
                                break;
                            }
                            _opt.PatternFilePath = args[++i];
                            break;
                        case "pl":
                        case "-path-limit":
                            if (i + 1 >= args.Length)
                            {
                                _opt.ErrorMessage = "--path-limit must be followed by an integer.";
                                break;
                            }
                            if (int.TryParse(args[++i], out int limit))
                            {
                                if (limit < 1)
                                {
                                    _opt.ErrorMessage = "--path-limit must be greater than zero (0).";
                                    break;
                                }
                                _opt.LongPathLimit = limit;
                            }
                            else
                            {
                                _opt.ErrorMessage = "Unable to parse --path-limit as an integer.";
                            }
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
                                _opt.ErrorMessage = "--output-file must be followed by a path to a file.";
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
            WriteLine("      read patterns from a file, one pattern per line. Pattern matching");
            WriteLine("      can be controlled by inserting pattern matching options a separate");
            WriteLine("      line beginning with two colons (::) and separated by space.");
            WriteLine("      The following options are availabe: FILE for matching files, DIRECTORY");
            WriteLine("      for matching directories, PATH for matching on full path, NAME for");
            WriteLine("      matching on file name only, SIMPLE for simple wildcard matching with");
            WriteLine("      asterisk (*) at the start, the end or both, and finally REGEX for");
            WriteLine("      matching using regular expressions. SIMPLE and REGEX cannot be combined.");
            WriteLine("      All patterns that come after a line with options, are affected by the");
            WriteLine("      preceeding options. Multiple lines with options can be used. If no");
            WriteLine("      line with options is present, or if patterns come before an initial");
            WriteLine("      options line, the following default options apply: simple match that");
            WriteLine("      match on name and matches only files (SIMPLE NAME FILE). All matching");
            WriteLine("      (including regex) is case insensitive. If a name or path matches");
            WriteLine("      multiple patterns, only the first match is reported.");
            WriteLine();
            WriteLine("  -pl, --path-limit <limit>");
            WriteLine("      specify limit for what is considered a long path. Default is 260 characters.");
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
            var matchDirectory = new List<Pattern>();
            var matchFile = new List<Pattern>();
            if (!string.IsNullOrEmpty(_opt.PatternFilePath))
            {
                try
                {
                    var patterns = PatternFileParser.Parse(_opt.PatternFilePath);
                    matchDirectory = patterns.Where(t => t.Options.HasFlag(PatternOption.Directory)).ToList();
                    matchFile = patterns.Where(t => t.Options.HasFlag(PatternOption.File)).ToList();
                }
                catch (FileNotFoundException)
                {
                    PrintError($"File not found: {_opt.PatternFilePath}");
                    return 1;
                }
                catch (ArgumentException e)
                {
                    PrintError($"There seemes to be a problem with the pattern file. Parser says:\r\n{e.Message}");
                    return 1;
                }
            }
            if (_opt.CheckForDuplicateFiles)
            {

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
                            long createdFileTime = (long)((ulong)findFileData.ftCreationTime_dwHighDateTime << 32 |
                                    findFileData.ftCreationTime_dwLowDateTime);
                            long modifiedFileTime = (long)((ulong)findFileData.ftLastWriteTime_dwHighDateTime << 32 |
                                    findFileData.ftLastWriteTime_dwLowDateTime);
                            if (fullPath.Length > _opt.LongPathLimit)
                            {
                                analysisData.LongPathList.Add(fullPath);
                            }
                            if ((findFileData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY)
                            {
                                if (findFileData.cFileName != "." && findFileData.cFileName != "..")
                                {
                                    analysisData.DirectoryCount++;
                                    stack.Push(fullPath);
                                    foreach (var pattern in matchDirectory)
                                    {
                                        bool isMatch = pattern.Options.HasFlag(PatternOption.MatchOnName) && pattern.IsMatch(findFileData.cFileName);
                                        isMatch |= pattern.Options.HasFlag(PatternOption.MatchOnPath) && pattern.IsMatch(fullPath);
                                        if (isMatch)
                                        {
                                            analysisData.PatternMatchList.Add(new PatternMatch
                                            {
                                                Pattern = pattern.PatternString,
                                                PatternOptions = pattern.Options.ToLongString(),
                                                Path = fullPath,
                                                Type = "Directory",
                                                Size = 0,
                                                Created = DateTime.FromFileTime(createdFileTime),
                                                Modified = DateTime.FromFileTime(modifiedFileTime)
                                            });
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                long fileSize = (long)findFileData.nFileSizeHigh << 32 | findFileData.nFileSizeLow & 0xFFFFFFFFL;
                                if (fileSize > analysisData.LargestFileSize)
                                {
                                    analysisData.LargestFileSize = fileSize;
                                    analysisData.LongestFilePath = fullPath;
                                }
                                analysisData.TotalSize += fileSize;
                                analysisData.FileCount++;
                                bool isMatch = false;
                                foreach (var pattern in matchFile)
                                {
                                    isMatch = pattern.Options.HasFlag(PatternOption.MatchOnName) && pattern.IsMatch(findFileData.cFileName);
                                    isMatch |= pattern.Options.HasFlag(PatternOption.MatchOnPath) && pattern.IsMatch(fullPath);
                                    if (isMatch)
                                    {
                                        analysisData.PatternMatchList.Add(new PatternMatch
                                        {
                                            Pattern = pattern.PatternString,
                                            PatternOptions = pattern.Options.ToLongString(),
                                            Path = fullPath,
                                            Type = "File",
                                            Size = fileSize,
                                            Created = DateTime.FromFileTime(createdFileTime),
                                            Modified = DateTime.FromFileTime(modifiedFileTime)
                                        });
                                        break;
                                    }
                                }
                                if (_opt.AnalyzeFileExtension)
                                {
                                    if (!_opt.AnalyzeFileExtensionForPatternMatchOnly || (_opt.AnalyzeFileExtensionForPatternMatchOnly && isMatch))
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
