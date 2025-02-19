using System.Text;
using System.Timers;
using Timer = System.Timers.Timer;

namespace BuildingBlocksPlatform.Logger
{
    public class RollFileLogger
    {
        /// <summary>
        /// [Only useful before the KouLog initialized] Default value is 1000 millisecond; 
        /// </summary>
        public static double? CustomWriteInterval { get; set; } = null;
        private Timer _writeLogTimer = null!;
        private DateTime lastRollFileTime = DateTime.Now;
        private StreamWriter? writer = null!;

        public RollFileLogger(string name, string path, TimeSpan rollFilePeriod, bool flushWhenWrite = false, int bufferSize = 65536)
        {
            Name = name;
            Path = path;
            RollFilePeriod = rollFilePeriod;
            BufferSize = bufferSize;
            FlushWhenWrite = flushWhenWrite;

            Directory.CreateDirectory(path);
            Initialize();
        }

        //protected readonly object LogWriteLock = new();
        protected readonly object LogBufferLock = new();
        protected StringBuilder Buffer = null!;

        public string? Name { get; init; }
        public string? Path { get; init; }
        public int BufferSize {get; init; }
        public bool FlushWhenWrite { get; init; }

        /// <summary>
        /// Roll file period (default one file per day)
        /// </summary>
        public TimeSpan RollFilePeriod { get; set; }

        private void Initialize()
        {
            var interval = CustomWriteInterval ?? 1000;
            _writeLogTimer = new Timer(interval);
            _writeLogTimer.Elapsed += WriteLogTimerElapsed;
            _writeLogTimer.Start();
            Buffer = new StringBuilder();
            writer = CreateWriter();
        }

        public void Term()
        {
            if (writer != null)
            {
                writer.Close();
            }
        }

        public string CurrLogFilePath => System.IO.Path.Join(Path, $"{Name} {DateTime.Now:yyyy-MM-dd HH_mm}.log");

        //public string PrevLogFilePath => System.IO.Path.Join(Path, $"{Name}1.log");

        private void WriteLogTimerElapsed(object sender, ElapsedEventArgs args)
        {
            RetrieveAndWrite();

            // roll file
            if (DateTime.Now.Subtract(lastRollFileTime) > RollFilePeriod)
            {
                if (writer != null)
                {
                    writer.Flush();
                    writer.Close();
                }

                //File.Delete(PrevLogFilePath);
                //File.Move(CurrLogFilePath, PrevLogFilePath);
                lastRollFileTime = DateTime.Now;
                writer = CreateWriter();

                // 清理过期文件
                var dir = new DirectoryInfo(Path!);
                foreach (var file in dir.GetFiles())
                {
                    if (file.CreationTime < DateTime.Now - RollFilePeriod * 24)
                    {
                        file.Delete();
                    }
                }
            }
        }

        private StreamWriter CreateWriter()
        {
            return new StreamWriter(CurrLogFilePath, false, Encoding.UTF8, BufferSize);
        }

        private void RetrieveAndWrite()
        {
            if (Buffer.Length > 0)
            {
                string logMsg;
                lock (LogBufferLock)
                {
                    logMsg = Buffer.ToString();
                    Buffer.Clear();
                }

                WriteLog(logMsg);
            }
        }
        private void WriteLog(string? content)
        {
            if (content.IsNullOrEmpty()) return;

            //lock (LogWriteLock)
            //{
            //    writer.Write(content);
            //}
            if (writer == null)
            {
                writer = CreateWriter();
            }
            writer.Write(content);
            if (FlushWhenWrite) writer.Flush();
        }

        /// <summary>
        /// Add log into log file.
        /// </summary>
        /// <param name="content"></param>
        public void Write(string? content)
        {
            if (content == null) return;
            var logContent = $"[{DateTime.Now}] {content}";
            lock (LogBufferLock)
            {
                Buffer.AppendLine(logContent);
            }
        }
    }
}
