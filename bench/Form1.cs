using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web;

namespace bench
{
    // todo: timeout doesn't kill the actual process, only the cmd process. 
    public partial class Form1 : Form
    {

        Hashtable processes = new Hashtable();  // from process to <args, benchmark, list of results>
        List<System.Threading.Timer> timers = new List<System.Threading.Timer>();
        List<string> labels = new List<string>();
        static int param_list_size = 8;
        TextBox[] param_list = new TextBox[param_list_size];
        int timeout = Timeout.Infinite;
        int MinMem = 1000;  // in MB        
        int cores = 8;
        int[] active = new int[8]; // {3, 5, 7 }; //note that we push all other processes to 1,2  [core # begin at 1]. with hyperthreading=off use {2,3,4}
        int failed = 0;
        bool hyperthreading = true;
        StreamWriter logfile = new System.IO.StreamWriter(@"C:\temp\log.txt");
        StreamWriter csvfile;
        string csvtext;
        Hashtable accum_results = new Hashtable();
        Hashtable results = new Hashtable();
        AbortableBackgroundWorker bg;
        HashSet<string> entries = new HashSet<string>();

        public Form1()
        {
            InitializeComponent();
            for (int i = 0; i < param_list_size; ++i)
            {
                param_list[i] = new TextBox();
                //param_list[i].Location = new System.Drawing.Point(60, 345 + i * 30);
                param_list[i].Location = new System.Drawing.Point(10,  i * 25);
                param_list[i].Size = new System.Drawing.Size(479, 20);
                param_list[i].Text = "<>";
                panel1.Controls.Add(param_list[i]);
                //Controls.Add(param_list[i]);
            }

            
            
            checkedListBox1.SetItemCheckState(0, CheckState.Checked);
            checkedListBox1.SetItemCheckState(2, CheckState.Checked);
            checkedListBox1.SetItemCheckState(4, CheckState.Checked);
            
            
            

            param_list[0].Text = " -no-muc-print-sol -no-pre -no-muc-progress";
            //text_filter.Text = "2dlx_ca_mc_ex_bp_f_new.gcnf";
            text_filter.Text = "f6n.cnf";
            //text_dir.Text = @"C:\temp\gcnf_test\belov\";
            //text_dir.Text = @"C:\temp\muc_test\SAT02\industrial\biere\cmpadd";
            //text_dir.Text = @"C:\temp\muc_test\marques-silva\hardware-verification";
            text_dir.Text = @"C:\temp\muc_test\SAT11\mus\marques-silva\hardware-verification";
            //text_exe.Text = "\"C:\\Users\\ofers\\Documents\\Visual Studio 2012\\Projects\\hhlmuc\\Release\\hhlmuc.exe\" ";
            text_exe.Text = "\"C:\\Users\\ofers\\Documents\\Visual Studio 2012\\Projects\\hmuc\\Release\\hmuc.exe\"";
            text_minmem.Text = MinMem.ToString();
            text_timeout = "600";
            text_csv.Text = @"c:\temp\res_2.csv";
        }

        #region utils

        void kill_process(Object stateinfo)
        {
            Process p = (Process)stateinfo;
            if (!p.HasExited)
            {
                bg.ReportProgress(0, "timeout: process killed: " + p.StartInfo.Arguments);
                p.Kill();
            }            
        }

        string outfile(string filename)
        {
            return filename + ".out";
        }

        void Log(string msg, bool tofile = true)
        {
            listBox1.Items.Add(msg);
            if (tofile)
            {
                logfile.WriteLine(msg);
            }
        }

        #endregion

        #region work

        void read_stdout(object sender, DataReceivedEventArgs e)
        {
            Process p = (Process)sender;

            if (e.Data != null)
            {                
                string consoleLine = e.Data;
                if (consoleLine.Length >= 4 && consoleLine.Substring(0, 3) == "###")
                {
                    var parts = consoleLine.Split(new char[] { ' ' });
                    string tag = parts[1];
                    if (tag == "Abort")
                    {
                        bg.ReportProgress(0, "* * * * * * * * * * * * *  Abort!");
                        return;
                    }                        
                        
                    if (!labels.Exists(x => x == tag)) labels.Add(tag);  // todo: wasteful. Perhaps get # of items from user. 
                    float res = Convert.ToSingle(parts[2]);
                    Tuple<string, string, List<float>> data = ((Tuple<string, string, List<float>>)processes[p]);
                    data.Item3.Add(res);
                }
            }
        }

        Process run(string cmd, string args, string filename, int affinity = 0x007F)
        {
            Process p = new Process();

            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.FileName = cmd;
            p.StartInfo.Arguments = args + " " + filename;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;

            //process.MaxWorkingSet = new IntPtr(2000000000); //2Gb 

            p.OutputDataReceived += read_stdout;

            p.Start();
            p.BeginOutputReadLine();
            p.ProcessorAffinity = (IntPtr)affinity;
            p.PriorityClass = ProcessPriorityClass.RealTime;

            var timer = new System.Threading.Timer(kill_process, p, timeout, Timeout.Infinite);
            timers.Add(timer); // needed ?
            List<float> l = new List<float>();
            processes[p] = new Tuple<string, string, List<float>>(args, filename, l);
            return p;
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string csvheader = "";
            int cnt = 0;
            Process[] p = new Process[cores + 1];
            
            string benchmarksDir = text_dir.Text,
                searchPattern = text_filter.Text;
            
            var fileEntries = new DirectoryInfo(benchmarksDir).GetFiles(searchPattern, checkBox_rec.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (fileEntries.Length == 0) bg.ReportProgress(0, "empty file list\n");

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool ok = false;

            for (int par = 0; par < param_list_size; ++par)  // for each parameter
            {
                if (param_list[par].Text == "<>") continue;
                bg.ReportProgress(0, "- - - - - " + param_list[par].Text + "- - - - - ");
                failed = 0;
                results.Clear();
                accum_results.Clear();
                cnt = 0;

                int ind1 = text_exe.Text.LastIndexOf('\\'),
                ind2 = text_exe.Text.LastIndexOf('.');
                string exe = text_exe.Text.Substring(ind1 + 1, ind2 - ind1 - 1);

                foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                {
                    string fileName = fileinfo.FullName;
                    ok = false;
                    do
                    {
                        Int64 AvailableMem = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
                        if (AvailableMem > MinMem)
                            foreach (int i in active)
                            {
                                if (i == 0) break;
                                if (p[i] == null || p[i].HasExited)
                                {
                                    bg.ReportProgress(0, "running " + fileName + " on core " + i.ToString());
                                    p[i] = run(text_exe.Text, param_list[par].Text, fileName, 1 << (i-1));
                                    ok = true;
                                    break;
                                }
                            }
                        else bg.ReportProgress(0, "not enough memory...");

                        if (!ok)
                        {
                            //bg.ReportProgress(0, ".");
                            System.Threading.Thread.Sleep(5000);// 5 seconds wait                        
                        }
                    } while (!ok);
                }
            }

            // post processing
            foreach (DictionaryEntry entry in processes)
            {
                Tuple<string, string, List<float>> trio = entry.Value as Tuple<string, string, List<float>>;
                Process p1 = (Process)entry.Key;
                p1.WaitForExit();
                List<float> l = (List<float>)trio.Item3;
                csvtext += "P:" + trio.Item1 + " ,";
                csvtext += Path.GetDirectoryName(trio.Item2) + ","; // benchmark
                csvtext += Path.GetFileName(trio.Item2) + ",";
                for (int i = 0; i < l.Count; ++i)
                    csvtext += l[i].ToString() + ",";
                if (l.Count == 0)   // in case of timeout / mem-out / whatever
                {
                    try { csvtext += Convert.ToInt32(text_timeout.Text); }
                    catch { }
                }
                csvtext += "\n";
            }

            bg.ReportProgress(0, "* all processes finished *");
            stopwatch.Stop();

            foreach (string lbl in labels) csvheader += lbl + ",";
            string time = (Convert.ToSingle(stopwatch.ElapsedMilliseconds) / 1000.0).ToString();
            bg.ReportProgress(0, "# of benchmarks:" + cnt);

            bg.ReportProgress(0, "parallel time = " + time);
            bg.ReportProgress(0, "============================");

            bg.ReportProgress(1, time); //label_paralel_time.Text
            try { bg.ReportProgress(2, accum_results["time"].ToString()); }
            catch { };  //label_total_time.Text}
            bg.ReportProgress(3, cnt.ToString()); // label_cnt.Text 
            bg.ReportProgress(5, failed.ToString());
            bg.ReportProgress(4, ""); // button1.Enabled = true;

            if (!checkBox_append.Checked)
            {
                csvfile.Write("param, dir, bench, " + csvheader);
                csvfile.WriteLine();
            }
            csvfile.Write(csvtext);
            csvfile.Close();
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            String log = e.UserState as string;
            //if (log == null) return;
            switch (e.ProgressPercentage)
            {
                case -1: Log(log, false); break;
                case 0: Log(log); break;
                case 1: label_paralel_time.Text = log; break;
                case 2: label_total_time.Text = log; break;
                case 3: label_cnt.Text = log; break;
                case 4: button1.Enabled = true; break;
                case 5: label_fails.Text = log; break;
            }
        }

        #endregion

        #region GUI

        private void button_start_Click(object sender, EventArgs e)
        {
            label_paralel_time.Text = "";
            label_total_time.Text = "";
            label_cnt.Text = "";
            label_fails.Text = "";
            csvtext = "";
            bg = new AbortableBackgroundWorker();
            processes.Clear();
            accum_results.Clear();
            results.Clear();
            int j = 0;
            foreach (int indexChecked in checkedListBox1.CheckedIndices)
            {
                active[j++] = indexChecked + 3;
            }
            try  // in case the field contains non-numeral.
            {
                int x = Convert.ToInt32(text_timeout.Text);
                timeout = x * 1000; // need milliseconds.
            }
            catch { timeout = Timeout.Infinite; }

            try  // in case the field contains non-numeral.
            {
                int x = Convert.ToInt32(text_minmem.Text);
                MinMem = x;
            }
            catch { MinMem = 0; }

            try
            {
                csvfile = new System.IO.StreamWriter(text_csv.Text, checkBox_append.Checked);      //(@"C:\temp\res.csv");
                //csvfile.WriteLine("param, bench, time");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot open the csv file!\n" + ex.ToString());
                return;
            }

            button1.Enabled = false;
            bg.WorkerReportsProgress = true;
            bg.DoWork += new System.ComponentModel.DoWorkEventHandler(backgroundWorker1_DoWork);
            bg.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(backgroundWorker1_ProgressChanged);

            Process[] localAll = Process.GetProcesses();
            int success = 0, failure = 0;
            foreach (Process p in localAll)
            {
                try
                {
                    if (hyperthreading) p.ProcessorAffinity = (IntPtr)((int)p.ProcessorAffinity & 3);  // cores 1,2
                    else p.ProcessorAffinity = (IntPtr)((int)p.ProcessorAffinity & 1);  // core 1
                    ++success;
                }
                catch
                {
                    ++failure;
                }
            }
            if (hyperthreading) listBox1.Items.Add("Moved " + success + " processes to cores 1,2");
            else listBox1.Items.Add("Moved " + success + " processes to core 1");

            bg.RunWorkerAsync();
        }

        private void button_kill_Click(object sender, EventArgs e)
        {
            int ind1 = text_exe.Text.LastIndexOf('\\'),  // we cannot use Path.GetFileNameWithoutExtension because the string contains "
                ind2 = text_exe.Text.LastIndexOf('.');
            string exe = text_exe.Text.Substring(ind1 + 1, ind2 - ind1 - 1);
            
            Process[] Pr = Process.GetProcessesByName(exe);
            foreach (Process p in Pr)
            {
                if (!p.HasExited) p.Kill();
            }
            if (bg != null)
            {
                bg.Abort();
                bg.Dispose();
            }
            if (csvfile != null) csvfile.Close();
            
            Process[] localAll = Process.GetProcesses();
            foreach (Process p in localAll)
            {
                try
                {
                    if (hyperthreading) p.ProcessorAffinity = (IntPtr)(0xFF);
                    else p.ProcessorAffinity = (IntPtr)(0xF);            
                }
                catch
                {                    
                }
            }
            listBox1.Items.Add("Retrieved Affinity");
            button1.Enabled = true;
        }

        private void button_csv_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.FileName = text_csv.Text;
            p.StartInfo = startInfo;
            p.Start();
        }
        #endregion



    }
}

//int j = 0;
//while (true)
//{
//    //run(@"powershell -ExecutionPolicy ByPass -File  c:\temp\cores.ps1", true);
//    //run("unix2dos report.txt", true);
//    //if (run("grep -c \" 0 %\" report.txt", true) == 0) break;
//    int load;
//    float fload;
//    free_core = -1;

//    for (; RR < cores; ++RR)
//    {
//        foreach (int i in active)
//        {
//            if (i < RR) continue;
//            j = i;                                
//            break;
//        }
//        fload = C[j].NextValue();
//        load = Convert.ToInt32(fload);
//        bg.ReportProgress(-1, "load = " + fload.ToString());
//        if (load < 3)
//        {
//            free_core = j;
//            RR = j + 1;
//            break;
//        }
//    }
//    if (RR == cores)
//    {
//        RR = 0;
//        continue;
//    }