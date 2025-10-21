// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:DirStat.Program.PrintUsage")]
[assembly: SuppressMessage("Performance", "CA1851:Possible multiple enumerations of 'IEnumerable' collection", Justification = "<Pending>", Scope = "member", Target = "~M:DirStat.Program.Main(System.String[])~System.Int32")]
[assembly: SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "<Pending>", Scope = "member", Target = "~M:DirStat.PatternOptionExtension.ToLongString(DirStat.PatternOption)~System.String")]
[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Used for JSON serialization/deserialization", Scope = "type", Target = "~T:DirStat.Tag")]
