﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DirStat
{

    public enum PatternOption
    {
        None        = 0,
        File        = 0b000001,
        Directory   = 0b000010,
        MatchOnName = 0b000100,
        MatchOnPath = 0b001000,
        Simple      = 0b010000,
        Regex       = 0b100000,
    }

    public static class PatternOptionExtension
    {
        private static StringBuilder stringBuilder;

        public static string ToLongString(this PatternOption opt)
        {
            if (opt == PatternOption.None)
            {
                return "";
            }
            if (stringBuilder == null)
            {
                stringBuilder = new StringBuilder();
            }
            else
            {
                stringBuilder.Length = 0;
            }
            foreach (PatternOption item in Enum.GetValues(typeof(PatternOption)))
            {
                if (opt.HasFlag(item) & item != PatternOption.None)
                {
                    stringBuilder.Append(item.ToString());
                    stringBuilder.Append(", ");
                }
            }
            stringBuilder.Length -= 2;
            return stringBuilder.ToString();
        }
    }

    public abstract class Pattern : IPattern
    {
        public string PatternString { get; }
        public PatternOption Options { get; }
        public Pattern(string pattern, PatternOption options)
        {
            PatternString = pattern;
            Options = options;
        }
        public abstract bool IsMatch(string input);
    }


    public interface IPattern
    {
        bool IsMatch(string input);
    }

    public class SimplePattern : Pattern
    {
        private readonly string subString;
        private readonly bool contains;
        private readonly bool startsWith;
        private readonly bool endsWith;

        public SimplePattern(string pattern, PatternOption options) : base(pattern, options)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentException("Pattern string is null or empty", nameof(pattern));
            }
            if (pattern[0] == '*' && pattern[pattern.Length - 1] == '*')
            {
                if (pattern.Length < 3)
                {
                    throw new ArgumentException("Invalid pattern string", nameof(pattern));
                }
                subString = pattern.Substring(1, pattern.Length - 2);
                contains = true;
            }
            else if (pattern[0] == '*')
            {
                if (pattern.Length < 2)
                {
                    throw new ArgumentException("Invalid pattern string", nameof(pattern));
                }
                subString = pattern.Substring(1);
                endsWith = true;
            }
            else if (pattern[pattern.Length - 1] == '*')
            {
                if (pattern.Length < 2)
                {
                    throw new ArgumentException("Invalid pattern string", nameof(pattern));
                }
                subString = pattern.Substring(0, pattern.Length - 2);
                startsWith = true;
            }
            else
            {
                subString = pattern;
            }
        }

        public override bool IsMatch(string input)
        {
            if (contains && input.IndexOf(subString, StringComparison.OrdinalIgnoreCase) > -1)
            {
                return true;
            }
            else if (endsWith && input.EndsWith(subString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (startsWith && input.StartsWith(subString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (input.Equals(subString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
    }

    public class RegexPattern : Pattern
    {
        private Regex regex;

        public RegexPattern(string pattern, PatternOption options) : base(pattern, options)
        {
            regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public override bool IsMatch(string input)
        {
            return regex.IsMatch(input);
        }
    }

    public static class PatternFileParser
    {
        public static IEnumerable<Pattern> Parse(string patternFilePath)
        {
            var result = new List<Pattern>();
            PatternOption options = PatternOption.None;
            PatternOption defaultOptions = PatternOption.File | PatternOption.MatchOnName | PatternOption.Simple;
            var contents = File.ReadAllLines(patternFilePath);
            foreach (string line in contents)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("::", StringComparison.Ordinal))
                {
                    options = PatternOption.None;
                    string optionString = line.Trim(new char[] { ' ', ':' });
                    string[] parts = optionString.Split(' ');
                    foreach (string item in parts)
                    {
                        switch (item.ToUpper())
                        {
                            case "FILE":
                                options |= PatternOption.File;
                                break;
                            case "DIRECTORY":
                                options |= PatternOption.Directory;
                                break;
                            case "SIMPLE":
                                if (options.HasFlag(PatternOption.Regex))
                                {
                                    throw new ArgumentException("REGEX and SIMPLE can not be combined.");
                                }
                                options |= PatternOption.Simple;
                                break;
                            case "REGEX":
                                if (options.HasFlag(PatternOption.Simple))
                                {
                                    throw new ArgumentException("REGEX and SIMPLE can not be combined.");
                                }
                                options |= PatternOption.Regex;
                                break;
                            case "NAME":
                                options |= PatternOption.MatchOnName;
                                break;
                            case "PATH":
                                options |= PatternOption.MatchOnPath;
                                break;
                            default:
                                throw new ArgumentException($"Unknown pattern match option \"{item}\".");
                        }
                    }
                    if (!options.HasFlag(PatternOption.File) && !options.HasFlag(PatternOption.Directory))
                    {
                        throw new ArgumentException("FILE or DIRECTORY (or both) must be present i options.");
                    }
                    if (!options.HasFlag(PatternOption.Regex) && !options.HasFlag(PatternOption.Simple))
                    {
                        throw new ArgumentException("SIMPLE or REGEX must be present i options.");
                    }
                    if (!options.HasFlag(PatternOption.MatchOnName) && !options.HasFlag(PatternOption.MatchOnPath))
                    {
                        throw new ArgumentException("NAME or PATH (or both) must be present i options.");
                    }
                }
                else
                {
                    if (options == PatternOption.None)
                    {
                        options = defaultOptions;
                    }
                    string patternString = line.Trim();
                    if (options.HasFlag(PatternOption.Simple))
                    {
                        result.Add(new SimplePattern(patternString, options));
                    }
                    else
                    {
                        result.Add(new RegexPattern(patternString, options));
                    }
                }
            }
            return result;
        }
    }
}
