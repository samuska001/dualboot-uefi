using System;
using System.IO;
namespace DualBoot.Installer {
 public static class SecureInstallerTests {
  public static int Run(){try{if(InstallerTests.Run()!=0)return 1;SecureBootTests.Run();if(SteamCollectionTests.Main(new string[0])!=0)return 1;return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
  public static int Main(){return Run();}
 }
}
