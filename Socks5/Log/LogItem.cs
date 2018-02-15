using System;

namespace Socks5
{
    internal class LogItem
    {
        /// <summary>
        /// 时间
        /// </summary>
        internal DateTime Date { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        internal LogType LogType { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        internal string LogContent { get; set; }

        internal LogItem(LogType logType, string logContent)
        {
            this.Date = DateTime.Now;
            this.LogType = logType;
            this.LogContent = logContent;
        }

        public override string ToString()
        {
            return $"[{Date}] @ [{LogType}]>>{LogContent}";
        }
    }
}
