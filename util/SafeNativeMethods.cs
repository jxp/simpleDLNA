using System;
using System.Runtime.InteropServices;

namespace NMaier.SimpleDlna.Utilities
{
  internal static class SafeNativeMethods
  {
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    internal static extern int StrCmpLogicalW(string psz1, string psz2);

    [DllImport("iphlpapi.dll")]
    public static extern uint SendARP(
      uint destIP, uint srcIP, [Out] byte[] pMacAddr,
      ref uint phyAddrLen);

    [DllImport("libc", CharSet = CharSet.Ansi)]
    public static extern int uname(IntPtr buf);
  }

  public static class NativeMethods
  {

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    [FlagsAttribute]
    public enum EXECUTION_STATE : Int64
    {
      ES_AWAYMODE_REQUIRED = 0x00000040,
      ES_CONTINUOUS = 0x80000000,
      ES_DISPLAY_REQUIRED = 0x00000002,
      ES_SYSTEM_REQUIRED = 0x00000001
      // Legacy flag, should not be used.
      // ES_USER_PRESENT = 0x00000004
    }

  }
}
