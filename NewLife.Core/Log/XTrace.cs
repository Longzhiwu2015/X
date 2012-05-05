using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NewLife.Configuration;

namespace NewLife.Log
{
    /// <summary>日志类，包含跟踪调试功能</summary>
    /// <remarks>
    /// 该静态类包括写日志、写调用栈和Dump进程内存等调试功能。
    /// 
    /// 默认写日志到文本文件，可通过挂接<see cref="OnWriteLog"/>事件来改变日志输出方式。
    /// 改变日志输出方式后，将不再向文本文件输出日志，但可通过<see cref="Log"/>继续写日志到文本文件中。
    /// 对于控制台工程，可以直接通过<see cref="UseConsole"/>方法，把日志输出重定向为控制台输出，并且可以为不同线程使用不同颜色。
    /// </remarks>
    public static class XTrace
    {
        #region 写日志
        /// <summary>文本文件日志</summary>
        public static TextFileLog Log = TextFileLog.Create(Config.GetConfig<String>("NewLife.LogPath"));

        /// <summary>日志路径</summary>
        public static String LogPath { get { return Log.LogPath; } }

        /// <summary>输出日志</summary>
        /// <param name="msg">信息</param>
        public static void WriteLine(String msg)
        {
            if (OnWriteLog != null)
            {
                var e = new WriteLogEventArgs(msg);
                OnWriteLog(null, e);
                return;
            }

            Log.WriteLine(msg);
        }

        /// <summary>输出异常日志</summary>
        /// <param name="ex">异常信息</param>
        public static void WriteException(Exception ex)
        {
            if (OnWriteLog != null)
            {
                var e = new WriteLogEventArgs(null, ex);
                OnWriteLog(null, e);
                return;
            }
            Log.WriteException(ex);
        }

        /// <summary>输出异常日志</summary>
        /// <param name="ex">异常信息</param>
        public static void WriteExceptionWhenDebug(Exception ex)
        {
            if (Debug) Log.WriteLine(ex.ToString());
        }

        /// <summary>
        /// 堆栈调试。
        /// 输出堆栈信息，用于调试时处理调用上下文。
        /// 本方法会造成大量日志，请慎用。
        /// </summary>
        public static void DebugStack()
        {
            Log.DebugStack(2, Int32.MaxValue);
        }

        /// <summary>堆栈调试。</summary>
        /// <param name="maxNum">最大捕获堆栈方法数</param>
        public static void DebugStack(int maxNum)
        {
            Log.DebugStack(maxNum);
        }

        /// <summary>堆栈调试</summary>
        /// <param name="start">开始方法数，0是DebugStack的直接调用者</param>
        /// <param name="maxNum">最大捕获堆栈方法数</param>
        public static void DebugStack(int start, int maxNum)
        {
            Log.DebugStack(start, maxNum);
        }

        /// <summary>写日志事件。绑定该事件后，XTrace将不再把日志写到日志文件中去。</summary>
        //public static event EventHandler<WriteLogEventArgs> OnWriteLog
        //{
        //    add { Log.OnWriteLog += value; }
        //    remove { Log.OnWriteLog -= value; }
        //}
        public static event EventHandler<WriteLogEventArgs> OnWriteLog;

        /// <summary>写日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void WriteLine(String format, params Object[] args)
        {
            Log.WriteLine(format, args);
        }
        #endregion

        #region 使用控制台输出
        private static Int32 init = 0;
        /// <summary>使用控制台输出日志，只能调用一次</summary>
        /// <param name="useColor"></param>
        public static void UseConsole(Boolean useColor = true)
        {
            if (init > 0 || Interlocked.CompareExchange(ref init, 1, 0) != 0) return;
            if (!Runtime.IsConsole) return;

            if (useColor)
                OnWriteLog += XTrace_OnWriteLog2;
            else
                OnWriteLog += XTrace_OnWriteLog;
        }

        private static void XTrace_OnWriteLog(object sender, WriteLogEventArgs e)
        {
            Console.WriteLine(e.ToString());
        }

        static Dictionary<Int32, ConsoleColor> dic = new Dictionary<Int32, ConsoleColor>();
        static ConsoleColor[] colors = new ConsoleColor[] { ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Magenta, ConsoleColor.Red, ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Blue };
        private static void XTrace_OnWriteLog2(object sender, WriteLogEventArgs e)
        {
            lock (dic)
            {
                ConsoleColor cc;
                var key = e.ThreadID;
                if (!dic.TryGetValue(key, out cc))
                {
                    //lock (dic)
                    {
                        //if (!dic.TryGetValue(key, out cc))
                        {
                            cc = colors[dic.Count % 7];
                            dic[key] = cc;
                        }
                    }
                }
                var old = Console.ForegroundColor;
                Console.ForegroundColor = cc;
                Console.WriteLine(e.ToString());
                Console.ForegroundColor = old;
            }
        }
        #endregion

        #region 属性
        private static Boolean? _Debug;
        /// <summary>是否调试。如果代码指定了值，则只会使用代码指定的值，否则每次都读取配置。</summary>
        public static Boolean Debug
        {
            get
            {
                if (_Debug != null) return _Debug.Value;

                try
                {
                    //return Config.GetConfig<Boolean>("NewLife.Debug", Config.GetConfig<Boolean>("Debug", false));
                    return Config.GetMutilConfig<Boolean>(false, "NewLife.Debug", "Debug");
                }
                catch { return false; }
            }
            set { _Debug = value; }
        }

        private static String _TempPath;
        /// <summary>临时目录</summary>
        public static String TempPath
        {
            get
            {
                if (_TempPath != null) return _TempPath;

                TempPath = Config.GetConfig<String>("NewLife.TempPath", "XTemp");
                return _TempPath;
            }
            set
            {
                _TempPath = value;
                if (String.IsNullOrEmpty(_TempPath)) _TempPath = "XTemp";
                if (!Path.IsPathRooted(_TempPath)) _TempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _TempPath);
                _TempPath = Path.GetFullPath(_TempPath);
            }
        }
        #endregion

        #region Dump
        /// <summary>写当前线程的MiniDump</summary>
        /// <param name="dumpFile">如果不指定，则自动写入日志目录</param>
        public static void WriteMiniDump(String dumpFile)
        {
            if (String.IsNullOrEmpty(dumpFile))
            {
                dumpFile = String.Format("{0:yyyyMMdd_HHmmss}.dmp", DateTime.Now);
                if (!String.IsNullOrEmpty(LogPath)) dumpFile = Path.Combine(LogPath, dumpFile);
            }

            MiniDump.TryDump(dumpFile, MiniDump.MiniDumpType.WithFullMemory);
        }

        /// <summary>
        /// 该类要使用在windows 5.1 以后的版本，如果你的windows很旧，就把Windbg里面的dll拷贝过来，一般都没有问题。
        /// DbgHelp.dll 是windows自带的 dll文件 。
        /// </summary>
        static class MiniDump
        {
            [DllImport("DbgHelp.dll")]
            private static extern Boolean MiniDumpWriteDump(
            IntPtr hProcess,
            Int32 processId,
            IntPtr fileHandle,
            MiniDumpType dumpType,
           ref MinidumpExceptionInfo excepInfo,
            IntPtr userInfo,
            IntPtr extInfo);

            /// <summary>MINIDUMP_EXCEPTION_INFORMATION</summary>
            struct MinidumpExceptionInfo
            {
                public UInt32 ThreadId;
                public IntPtr ExceptionPointers;
                public UInt32 ClientPointers;
            }

            [DllImport("kernel32.dll")]
            private static extern uint GetCurrentThreadId();

            public static Boolean TryDump(String dmpPath, MiniDumpType dmpType)
            {
                //使用文件流来创健 .dmp文件
                using (FileStream stream = new FileStream(dmpPath, FileMode.Create))
                {
                    //取得进程信息
                    Process process = Process.GetCurrentProcess();

                    // MINIDUMP_EXCEPTION_INFORMATION 信息的初始化
                    MinidumpExceptionInfo mei = new MinidumpExceptionInfo();

                    mei.ThreadId = (UInt32)GetCurrentThreadId();
                    mei.ExceptionPointers = Marshal.GetExceptionPointers();
                    mei.ClientPointers = 1;

                    //这里调用的Win32 API
                    Boolean res = MiniDumpWriteDump(
                    process.Handle,
                    process.Id,
                    stream.SafeFileHandle.DangerousGetHandle(),
                    dmpType,
                   ref mei,
                    IntPtr.Zero,
                    IntPtr.Zero);

                    //清空 stream
                    stream.Flush();
                    stream.Close();

                    return res;
                }
            }

            public enum MiniDumpType
            {
                None = 0x00010000,
                Normal = 0x00000000,
                WithDataSegs = 0x00000001,
                WithFullMemory = 0x00000002,
                WithHandleData = 0x00000004,
                FilterMemory = 0x00000008,
                ScanMemory = 0x00000010,
                WithUnloadedModules = 0x00000020,
                WithIndirectlyReferencedMemory = 0x00000040,
                FilterModulePaths = 0x00000080,
                WithProcessThreadData = 0x00000100,
                WithPrivateReadWriteMemory = 0x00000200,
                WithoutOptionalData = 0x00000400,
                WithFullMemoryInfo = 0x00000800,
                WithThreadInfo = 0x00001000,
                WithCodeSegs = 0x00002000
            }
        }
        #endregion
    }
}