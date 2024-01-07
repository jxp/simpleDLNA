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


    public static Guid GUID_SLEEP_SUBGROUP = new Guid("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    public static Guid GUID_STANDBYIDLE = new Guid("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");

    [DllImport("powrprof.dll")]
    #pragma warning disable CS3002 // Return type is not CLS-compliant
    public static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, ref IntPtr ActivePolicyGuid);
    #pragma warning restore CS3002 // Return type is not CLS-compliant

    [DllImport("powrprof.dll")]
    #pragma warning disable CS3002 // Return type is not CLS-compliant
    #pragma warning disable CS3001 // Argument type is not CLS-compliant
    public static extern uint PowerReadACValue(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingGuid,
        ref Guid PowerSettingGuid, ref int Type, ref int Buffer, ref uint BufferSize);
    #pragma warning restore CS3001 // Argument type is not CLS-compliant
    #pragma warning restore CS3002 // Return type is not CLS-compliant

  }
}
