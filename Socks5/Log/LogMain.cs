using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;

namespace Socks5
{
    internal class LogMain
    {

        #region 变量
        /// <summary>
        /// 日志的缓冲集合
        /// </summary>
        private readonly List<LogItem> _LogItems = null;

        /// <summary>
        /// 日志文件路径
        /// </summary>
        private readonly String _LogFilePath = String.Empty;

        /// <summary>
        /// 日志存放路径
        /// </summary>
        private readonly String _LogDirectory = String.Empty;

        /// <summary>
        /// 默认日志类型
        /// </summary>
        internal LogType _LogType { get; private set; }

        private Timer _Timer = null;
        #endregion

        #region 方法
        /// <summary>
        /// 初始化LogMain实例
        /// </summary>
        /// <param name="LogDirectory">日志存放文件夹[如果文件夹不存在则自动创建]</param>
        internal LogMain(String LogDirectory)
        {
            try
            {
                _LogDirectory = LogDirectory; // 日志文件夹位置
                var dateTime = DateTime.Now;
                _LogFilePath = (_LogDirectory + "\\" + dateTime.Year + "-" + dateTime.Month + "-" + dateTime.Month + "$" + dateTime.Hour + "." + dateTime.Minute + "." + dateTime.Second + ".log");

                if (_LogItems == null) _LogItems = new List<LogItem>(); // 检查/创建日志集合对象
                if (!Directory.Exists(_LogDirectory)) Directory.CreateDirectory(_LogDirectory); // 检查/创建日志目录

                using (StreamWriter sw = File.CreateText(_LogFilePath)) { sw.WriteLineAsync($"[{dateTime}] @ [None]>>LogEngine Initialization Complete!"); } // 创建文件并记录日志系统初始化完成
                _LogType = LogType.Error;
                if (_Timer == null) _Timer = new Timer(60000);
                _Timer.Enabled = true;
                _Timer.Elapsed += Timer_Elapsed;
                _Timer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 设置当前日志默认类型
        /// </summary>
        /// <param name="logType"></param>
        internal void SetLogType(LogType logType)
        {
            this._LogType = logType;
        }

        /// <summary>
        /// 添加一条日志到日志缓冲区
        /// </summary>
        /// <param name="logContent">日志内容</param>
        internal void AddLog(String logContent)
        {
            _LogItems.Add(new LogItem(_LogType, logContent));
        }

        /// <summary>
        /// 添加一条日志到日志缓冲区
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <param name="logContent">日志内容</param>
        internal void AddLog(LogType logType, String logContent)
        {
            _LogItems.Add(new LogItem(logType, logContent));
        }

        /// <summary>
        /// 保存日志
        /// </summary>
        internal void SaveLog()
        {
            try
            {
                lock (this)
                {
                    if (_LogItems.Count > 0)
                    {
                        using (StreamWriter sw = new StreamWriter(@_LogFilePath, true, Encoding.UTF8))
                        {
                            foreach (var i in _LogItems)
                            {
                                sw.WriteLineAsync(i.ToString());
                            }
                            _LogItems.Clear();
                        }
                        GC.Collect(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        #region 委托/事件
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SaveLog();
        }

        #endregion

    }
}
