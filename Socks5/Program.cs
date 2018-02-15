using System;
using System.Threading;

namespace Socks5
{
    class Program
    {
        static void Main()
        {
            ProgramMain main = new ProgramMain();
        }
    }
    class ProgramMain
    {
        private static LogMain Log = null;
        private static Mutex mutex = null;
        private Socks5Listener Listener = null;
        internal ProgramMain()
        {
            mutex = new Mutex(true, System.Diagnostics.Process.GetCurrentProcess().MachineName, out bool isRun);
            Log = new LogMain(AppDomain.CurrentDomain.BaseDirectory + "//Log");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Green;

            var cmd = String.Empty;

            if (isRun)
            {
                Console.WriteLine(DateTime.Now + "\r\n请注意,监听IP地址务必为内网IP,不可为127.0.0.1/0.0.0.0,否则会导致程序不工作!");
                Console.Write(">");
                cmd = Console.ReadLine();
                while (!cmd.Equals("2333"))
                {
                    switch (cmd)
                    {
                        case "0":
                            Console.Clear();
                            break;
                        case "1":
                            Console.Write("请输入监听地址:");
                            var a = Console.ReadLine();
                            Console.Write("请输入监听端口:");
                            var b = Console.ReadLine();
                            Console.Write("请输入最大用户数:");
                            var c = Console.ReadLine();
                            Listener = new Socks5Listener(a, Convert.ToInt32(b), Convert.ToInt32(c), ref Log);
                            Listener.StartListener();
                            Console.Clear();
                            Console.WriteLine("时间:" + DateTime.Now + "\r\n已启动Socks5代理服务器!");
                            break;
                        case "2":
                            Console.WriteLine("正在重启代理服务器,请稍后...");
                            if (Listener != null) Listener.Restart();
                            else Console.WriteLine("Socks5代理服务器未开启,无法完成重启操作!");
                            Console.WriteLine("代理服务器重启完成!");
                            break;
                        case "3":
                            Console.WriteLine("正在关闭代理服务器,请稍后...");
                            if (Listener != null) Listener.Stop();
                            else Console.WriteLine("Socks5代理服务器未开启,无法完成关闭操作!");
                            Console.WriteLine("代理服务器已关闭!");
                            break;
                        case "4":
                            Console.Write("请输入监听地址:");
                            var d = Console.ReadLine();
                            Console.Write("请输入监听端口:");
                            var e = Console.ReadLine();
                            Console.Write("请输入最大用户数:");
                            var f = Console.ReadLine();
                            if (Listener == null) Listener = new Socks5Listener(d, Convert.ToInt32(e), Convert.ToInt32(f), ref Log);
                            else {
                                Listener.Stop();
                                Listener.Dispose();
                                Listener = new Socks5Listener(d, Convert.ToInt32(e), Convert.ToInt32(f), ref Log);
                                Listener.StartListener();
                                Console.WriteLine("设置更改完成.");
                            }
                            break;
                        case "?":
                            Console.WriteLine("输入?查看命令帮助\r\n输入0清空控制台\r\n输入1开启Socks5代理服务器\r\n输入2重启代理服务器\r\n输入3关闭代理服务器\r\n输入4关闭代理服务器\r\n输入4更改代理服务器设置\r\n输入2333关闭程序");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("请注意,更改代理服务器设置会导致服务器重启!");
                            Console.ForegroundColor = ConsoleColor.Green;
                            break;
                        default:
                            Console.WriteLine("命令错误!\r\n请输入?查看帮助.");
                            break;
                    }
                    Console.Write(">");
                    cmd = Console.ReadLine();
                }
            }

            Log.SaveLog();
            Console.WriteLine("日志保存完成!\r\n请按任意键退出...");
            Console.Read();
        }
    }
}
