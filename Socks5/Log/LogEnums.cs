namespace Socks5
{
    internal enum LogType
    {
        Info, // 自定义信息
        LowLevelError, // .NET Framework错误
        Error, // 自定义错误
        Warning, // 自定义警告
        Other, // 自定义其他
        Debug // 自定义Debug信息
    }
}
