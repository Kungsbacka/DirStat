# DirStat

DiStat collects statistics about files and folders. It can be run on a single folder or multiple folders read from a
JSON file (see format below).

## Arguments

    -l, --list <file>
        read paths to scan from a file, one path per line. If both a list and"
        a directory is specified, the directory is added to the list"

    -m, --match <file>
        read patterns from a file, one pattern per line. Pattern matching"
        can be controlled by inserting pattern matching options on a separate
        line beginning with two colons (::) and separated by space.
        The following options are availabe: FILE for matching files, DIRECTORY
        for matching directories, PATH for matching on full path, NAME for
        matching on name only (not whole path), SIMPLE for simple wildcard matching
        with asterisk (*) at the start, the end or both, and finally REGEX for
        matching using regular expressions. SIMPLE and REGEX cannot be combined.
        All patterns that come after a line with options, are affected by the
        preceeding options. Multiple lines with options can be used. If no
        line with options is present, or if patterns come before an initial
        options line, the following default options apply: simple match that
        match on name and matches only files (SIMPLE NAME FILE). All matching
        (including regex) is case insensitive. If a name or path matches
        multiple patterns, only the first match is reported.

    -pl, --path-limit <limit>
        specify limit for what is considered a long path. Default is 260 characters.

    -na, --no-age
        leave out file age statisticts in the result.

    -ne, --no-ext
        leave out file extension statistics in the result.

    -pa, --pattern-age
        if pattern matching is enabled, only age for files that
        match a pattern gets added to the file age statistics.
        This parameter and the --no-ext parameter cannot be used together.

    -pe, --pattern-ext
        if pattern matching is enabled, only extensions for files that
        match a pattern gets added to the file extension statistics.
        This parameter and the --no-ext parameter cannot be used together.

    -gs, --group-on-subdir
        Subdirectories immediately below the specified directory are reported
        seperately.

    -o, --output-file <file>
        results are written to this file. If this parameter is omitted, the results
        are written to data.csv.

    -f, --format-json
        adds line breaks and indentation to JSON output.

    -h, --help
        show help and exit

## Output file

The output JSON has the structure you see below, where every folder in the folder list gets
its own entry.

* __Directory__ is the base directory and an optional tag list that is read from the file list. It can be used to attach metadata to scanned directories that can be used when analyzingthe data.
* All sizes are in bytes.
* __LongPathList__ is a list of paths whose length is longer than 260 characters, or the number of characters specified by the -pl argument.
* __FailedDirectoryList__ contains a list of all paths that could not be scanned.
* If pattern matching is specified, __PatternMatchList__  contains a list if all matched paths.
* __FileExtensionList__ has detailed information about all file extensions found.
* __FileChangeList__ groups files into different age groups based on when they were modified. The groups are: 1, 2, 3, 4, 5, 10, 20, 30, 182, 365, 730, 1095, 1460, 1825, 2492 and 3650 days ago.

```json
[
  {
    "Directory": {
      "Path": "C:\\Scanned folder",
      "TagList": [{"Name" : "Department", "Value": "HR"}]
    },
    "LongestFilePath": "c:\\Scanned folder\\subdir\\document.docx",
    "LargestFileSize": 26246440,
    "TotalSize": 62509555,
    "FileCount": 31,
    "DirectoryCount": 3,
    "ScanTime": "00:00:00.0018973",
    "ScanTimeInMS": 2,
    "LongPathList": [],
    "FailedDirectoryList": [],
    "PatternMatchList": [],
    "FileExtensionList": [
      {
        "Extension": ".docx",
        "Size": 8364528,
        "Count": 31
      }
    ],
    "FileChangedList": [
      {
        "DaysAgo": 1,
        "Created": {
          "Size": 62509555,
          "Count": 31
        },
        "Modified": {
          "Size": 62509555,
          "Count": 31
        }
      }
    ]
  }
]
```





## File list

The folder list contains all folders that should be scanned and is specified using -l (or --list).

* __Path__ is the path that should be scanned.
* __GroupOnSubdirectories__ can be used to report each subdirectory directly below the specified path individualy. One scenario is a share with home directories where you want a report on each individual directory.
* __TagList__ contains a list of key value pairs that is transferred unchanged to the report file. Useful for adding metadata to scanned folders.

```json
[
    {
        "Path": "G:\\Home",
        "GroupOnSubdirectories": true,
        "TagList": [
            {
                "Value": "HR",
                "Name": "Department"
            }
        ]
    }
]
```

## Pattern matching

Pattern matching is controlled by rules that are read from a file specified by -m (--match).
The file can contain hints to how the different patterns should be used. Hints are placed
on a separate line beginning with :: and separated by space.

* __SIMPLE__ Patterns below users simple wildcard matching with an asterisk (*) in the beginning, end or both. All other asterisks will be matched as a litteral character.
* __REGEX__ Patterns below uses regular expression matching and should contain vaild regex.
* __NAME__ Match only on the name (file or folder) and not the whole path.
* __FILE__ Mathc only files and not folders.
* __DIRECTORY__ Match only folders and not files.


## Pattern matching - example

    ::SIMPLE FILE NAME
    *.pdf
    Report*
    *secure*

    ::SIMPLE DIRECTORY NAME
    System

    ::REGEX FILE
    Report (Sally|John)\.xlsx$
