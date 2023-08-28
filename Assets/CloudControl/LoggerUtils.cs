using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using System.Diagnostics;
using UnityEngine.SceneManagement;

namespace Baidu.VR.Zion.Utils
{
    public static class LoggerUtils
    {
        private static readonly ILogger Logger = UnityEngine.Debug.unityLogger;

        /// <summary>
        /// log 中 tag使用的颜色值
        /// </summary>
        public enum Colour
        {
            white = ColourProtocol.white,
            black = ColourProtocol.black,
            teal = ColourProtocol.teal,
            cyan = ColourProtocol.cyan,
            lightblue = ColourProtocol.lightblue,
            purple = ColourProtocol.purple,
            orange = ColourProtocol.orange,
            olive = ColourProtocol.olive,
            brown = ColourProtocol.brown,
            maroon = ColourProtocol.maroon,
            red = ColourProtocol.red,
            yellow = ColourProtocol.yellow
        }

        /// <summary>
        /// 是否开启log
        /// </summary>
        /// <param name="isEnable"></param>
        public static void SetLogEnabled(bool isEnable)
        {
            Logger.logEnabled = isEnable;
        }

        /// <summary>
        /// 设置log的输出级别
        /// </summary>
        /// <param name="type">输出log类型的最小值 大于当前type的log 会输出</param>
        public static void SetFilterLogType(LogType type)
        {
            Logger.filterLogType = type;
        }

        public static void Log(string msg, string tag = "", Colour color = Colour.white)
        {
            FormatLog(LogType.Log, msg, tag, color);
        }
        
        public static void LogWarning(string msg, string tag = "", Colour color = Colour.white)
        {
            FormatLog(LogType.Warning, msg, tag, color);
        }

        public static void LogError(string msg, string tag = "", Colour color = Colour.white)
        {
            FormatLog(LogType.Error, msg, tag, color);
        }

        /// <summary>
        /// 格式化log msg
        /// </summary>
        /// <param name="type"></param>
        /// <param name="msg">log message</param>
        /// <param name="tag">log tag 加入了时间，当前场景</param>
        /// <param name="color">tag的颜色</param>
        private static void FormatLog(LogType type, string msg, string tag, Colour color)
        {
            var preTag = "";
#if !UNITY_EDITOR
            preTag = $"[{DateTime.Now:HH:mm:ss}]";
#endif
            tag = !string.IsNullOrEmpty(tag) ? $"{preTag} <b><color={color}>[{tag}]</color></b>" : $"{preTag}";
            switch (type)
            {
                case LogType.Log:
                    Logger.Log(tag, msg);
                    break;
                case LogType.Warning:
                    Logger.LogWarning(tag, msg);
                    break;
                case LogType.Error:
                    Logger.LogError(tag, msg);
                    break;
            }
        }
        
        #region 暂时用不到 后续加入单独的模块 存入文件 或 做日志上报
        //when the logs reaches the max,then save to the desk
        // const int MAX_LOG = 5000;
        // private static Queue<string> queue_log = new Queue<string>();
        // private static void AddToQueue(string str)
        // {
        //     queue_log.Enqueue(str);
        //     if (queue_log.Count >= MAX_LOG)
        //     {
        //         SaveDebugFile();
        //         queue_log.Clear();
        //     }
        // }
        //
        // public static void CleanDebugLogCache()
        // {
        //     queue_log.Clear();
        // }
        //
        // public static void SaveDebugFile()
        // {
        //     string path = Path.Combine(Application.persistentDataPath, "Logs");
        //     DirectoryInfo dir = new DirectoryInfo(path);
        //     if (!dir.Exists)
        //     {
        //         dir.Create();
        //     }
        //
        //     string fileName = String.Format("Log_{0}.txt", DateTime.Now.ToString("HH_mm_ss_ff"));
        //     string fullPath = Path.Combine(path, fileName);
        //     FileInfo file = new FileInfo(fullPath);
        //     if (file.Exists)
        //     {
        //         file.Delete();
        //     }
        //
        //     using (StreamWriter write = new StreamWriter(fullPath, true))
        //     {
        //         while (queue_log.Count > 0)
        //         {
        //             write.WriteLine(queue_log.Dequeue());
        //         }
        //     }
        //     UnityEngine.Debug.Log("saved log in: " + path);
        // }
        #endregion
    }
}