using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace bench
{
    static class Program
    {


        [DllImport("kernel32.dll")]
        static extern ErrorModes SetErrorMode(ErrorModes uMode);

        [Flags]
        public enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread] // [STAThread()] ?
        static void Main()
        {
            // experimental
            SetErrorMode(ErrorModes.SEM_NOGPFAULTERRORBOX | ErrorModes.SEM_NOOPENFILEERRORBOX); // this funtion prevents error dialog box to show up after application crash
            // 
            DateTime exe_date = File.GetLastWriteTime(System.Reflection.Assembly.GetEntryAssembly().Location);
            DateTime source_date = File.GetLastWriteTime(@"..\..\Form1.cs");
            if (source_date > exe_date) MessageBox.Show("Warning: date of executable is before date of source Form1.cs. Recompile !","Warning",MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new filter());
        }
    }
}
