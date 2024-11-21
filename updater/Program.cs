using ICSharpCode.SharpZipLib.Zip;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Resources;

namespace updater;

public class Program
{
    static readonly string runPath = AppDomain.CurrentDomain.BaseDirectory;
    static void Main(string[] args)
    {
        Start();
        Console.Read();
    }

    static void Write(string msg)
    {
        Console.WriteLine(msg);
    }
    static void WriteAndExit(string msg)
    {
        Console.WriteLine(msg);
        Console.Read();
        Environment.Exit(0);
    }

    static void Start() { StartForWindow(); }
    static void StartForWindow()
    {
        Write("1、初始化配置文件中...");
        InitIni(out string zipName, out string iisAppPoolName);
        if (!string.IsNullOrEmpty(zipName) && !string.IsNullOrEmpty(iisAppPoolName))
        {
            string zipFile = runPath + zipName;
            if (!File.Exists(zipFile))
            {
                WriteAndExit("Can't find the file : " + zipFile);
            }
            Write("2、读取并解压Zip文件中...");
            List<string> fileNames = GetZipFileNames(zipFile);

            Write("...");
            if (fileNames.Count > 0)
            {
                try
                {
                    int i = 0;
                    foreach (string fileName in fileNames)
                    {
                        i++;
                        string oldFile = runPath + fileName;
                        if (File.Exists(oldFile))
                        {
                            string temp = "__" + DateTime.Now.Ticks + i + ".temp";
                            File.Move(oldFile, runPath + temp);
                        }
                    }
                }
                catch (Exception err)
                {
                    WriteAndExit(err.Message);
                }

            }
            Write("...");
            ExtractToDirectory(zipFile, runPath);

            Write("3、重启 IIS 应用程序池中...");
            string[] items = iisAppPoolName.Split(',');
            foreach (var item in items)
            {
                ReStartApp(item);
            }
            Console.WriteLine("-------------升级完成-------------");
            Write("4、等待应用程序结束并清理缓存文件中，预计1分钟左右...");
            while (true)
            {
                Thread.Sleep(3000);
                try
                {
                    string[] files = Directory.GetFiles(runPath, "*.temp");
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                    break;
                }
                catch (Exception err)
                {
                    Write("...");
                    Thread.Sleep(5000);
                }

            }
            Thread thread = new(new ThreadStart(Exit));
            thread.Start();

            Write("清理完成，5秒后自动退出。");
        }
    }
    static void Exit()
    {
        Thread.Sleep(5000);
        Environment.Exit(0);
    }
    static void InitIni(out string zipName, out string iisAppPoolName)
    {
        zipName = null;
        iisAppPoolName = null;
        try
        {
            string ini = runPath + "updater.ini";
            if (!File.Exists(ini))
            {
                WriteAndExit("Can't find the updater.ini");
            }
            string[] items = File.ReadAllLines(ini);
            foreach (string item in items)
            {
                if (!string.IsNullOrEmpty(item) && !item.StartsWith('#'))
                {
                    string[] keyValue = item.Split('=');
                    if (keyValue[0] == "zipName" && keyValue.Length > 0)
                    {
                        zipName = keyValue[1].Trim();
                    }
                    else if (keyValue[0] == "iisAppPoolName" && keyValue.Length > 0)
                    {
                        iisAppPoolName = keyValue[1].Trim();
                    }
                }
            }
            if (string.IsNullOrEmpty(zipName) || string.IsNullOrEmpty(iisAppPoolName))
            {
                WriteAndExit("updater.ini zipName or iisAppPoolName can't be empty.");
            }
        }
        catch (Exception err)
        {
            WriteAndExit(err.Message);
        }
    }
    static List<string> GetZipFileNames(string zipFile)
    {
        List<string> fileNames = [];
        try
        {
            using ZipInputStream zipStream = new(File.Open(zipFile, FileMode.Open));
            while (true)
            {
                var zipEntry = zipStream.GetNextEntry();
                if (zipEntry == null) { break; }
                string name = zipEntry.ToString();
                //if (name.EndsWith(".dll"))
                //{
                    fileNames.Add(name);
                //}
            }
        }
        catch (Exception err)
        {
            WriteAndExit(err.Message);
        }
        return fileNames;
    }
    static void ExtractToDirectory(string zipFile, string toFolder)
    {
        try
        {
            FastZip fastZip = new();
            fastZip.ExtractZip(zipFile, toFolder, string.Empty);
            Thread.Sleep(1000);//等待解压完成。
        }
        catch (Exception err)
        {
            WriteAndExit(err.Message);
        }
    }
    static void ReStartApp(string appPool)
    {
        try
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = @"c:\Windows\System32\inetsrv\appcmd.exe";
            info.Arguments = @"recycle apppool " + appPool;
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            Process.Start(info);
        }
        catch (Exception err)
        {
            Console.WriteLine(err.Message);
        }
    }
}
