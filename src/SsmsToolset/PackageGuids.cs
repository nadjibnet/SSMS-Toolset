using System;

namespace SsmsToolset
{
    /// <summary>
    /// GUIDs and command IDs shared between the package code and the .vsct command table.
    /// Keep these in sync with <c>SsmsToolset.vsct</c>.
    /// </summary>
    internal static class PackageGuids
    {
        /// <summary>Identity of the VS package (matches guidSsmsToolsetPackage in the .vsct).</summary>
        public const string PackageString = "b7e3f5a1-9c24-4d8e-8f10-2a3b4c5d6e70";

        /// <summary>Command set that owns this extension's menus/commands (matches guidSsmsToolsetCmdSet).</summary>
        public const string CmdSetString = "c8f406b2-0d35-4e9f-9021-3b4c5d6e7f81";

        public static readonly Guid CmdSet = new Guid(CmdSetString);
    }

    /// <summary>Command IDs, matching the &lt;IDSymbol&gt; values in the .vsct.</summary>
    internal static class PackageIds
    {
        public const int ShowHelloCommand = 0x0100;
    }
}
