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

namespace WindowsFormsApplication1
{

    public partial class Form1 : Form
    {

        List<Process> processes = new List<Process>();
        static int param_list_size = 4;
        TextBox[] param_list = new TextBox[param_list_size];
        int timeout = Timeout.Infinite;
        int MinMem = 1000;  // in MB
        int cores = 8;
        int failed = 0;
        StreamWriter logfile = new System.IO.StreamWriter(@"C:\temp\log.txt");
        StreamWriter csvfile;
        string csvtext;
        Hashtable results = new Hashtable();

        public Form1()
        {
            InitializeComponent();                        
            for (int i = 0; i < param_list_size; ++i)
            {
                param_list[i] = new TextBox();
                param_list[i].Location = new System.Drawing.Point(60, 345 + i * 30);
                param_list[i].Size = new System.Drawing.Size(407, 20);
                param_list[i].Text = "<>";
                Controls.Add(param_list[i]);
            }
            param_list[0].Text = " -no-muc-print-sol -no-pre";
            //text_filter.Text = "2dlx_ca_mc_ex_bp_f_new.gcnf";
            text_filter.Text = "*.cnf";
            //text_dir.Text = @"C:\temp\gcnf_test\belov\";
            //text_dir.Text = @"C:\temp\muc_test\SAT02\industrial\biere\cmpadd";
            text_dir.Text = @"C:\temp\muc_test\marques-silva\hardware-verification";
            //text_exe.Text = "\"C:\\Users\\ofers\\Documents\\Visual Studio 2012\\Projects\\hhlmuc\\Release\\hhlmuc.exe\" ";
            text_exe.Text = "\"C:\\Users\\ofers\\Documents\\Visual Studio 2012\\Projects\\hmuc\\Release\\hmuc.exe\" ";
            text_minmem.Text = MinMem.ToString();
            text_csv.Text = @"c:\temp\res.csv";
        }

        #region utils

        void kill_process(Object stateinfo)
        {
            Process process = (Process)stateinfo;
            if (!process.HasExited)
            {
                Log("timeout: process killed: " + process.StartInfo.Arguments);
                process.Kill();
            }
        }
         
        string outfile(string filename) {
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


        int run_native(string cmd, bool wait = false)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            process.StartInfo.RedirectStandardOutput = true;
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = " /c \"" + cmd + "\"";
            process.StartInfo = startInfo;
            //process.MaxWorkingSet = new IntPtr(2000000000); //2Gb            
            processes.Add(process);

            process.Start();

            var timer = new System.Threading.Timer(kill_process, process, timeout, Timeout.Infinite);  // 10 min. time-out

            if (wait)
            {
                process.WaitForExit();
                return process.ExitCode;
            }
            return -1;
        }

        int run(string cmd, bool wait = false)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = " /c \"" + cmd + "\"";
            process.StartInfo = startInfo;
            //process.MaxWorkingSet = new IntPtr(2000000000); //2Gb            
            processes.Add(process);

            process.Start();

            var timer = new System.Threading.Timer(kill_process, process, timeout, Timeout.Infinite);  // 10 min. time-out

            if (wait)
            {
                process.WaitForExit();
                return process.ExitCode;
            }
            return -1;
        }

        void process_out(string fileName, int par, ref int cnt, ref string csvheader)
        {
            ++cnt;
            //run("grep \"### time\" " + outfile(fileName) + " | sed -e s/\"### time\"// > tmp ", true);
            run("grep \"### \" " + outfile(fileName) + "  > tmp ", true);
            StreamReader sr = new StreamReader("tmp");
            try
            {
                csvtext += "\"\t" + param_list[par].Text + "\"," + fileName + ",";  // the \t is necessary for excel, so it does not interpret e.g. -auto as a formula. 
                //float fres = Convert.ToSingle(System.IO.File.ReadAllText("tmp"));


                csvheader = "";
                while (!sr.EndOfStream)
                {
                    var parts = sr.ReadLine().Split(new char[] { ' ' });
                    string tag = parts[1];
                    csvheader += tag + ",";
                    float fres = Convert.ToSingle(parts[2]);
                    csvtext += fres + ",";
                    float current = results.ContainsKey(tag) ? (float)results[tag] : 0.0f;
                    results[tag] = current + fres;
                }
                csvtext += "\n";
            }
            catch
            {
                bg.ReportProgress(0, "failed to read result from " + outfile(fileName));
                failed++;
                csvtext += "\n";
            }
            sr.Close();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string csvheader = "";
            int cnt = 0;            
            PerformanceCounter[] C;
            C = new PerformanceCounter[cores];
            for (int i = 0; i < cores; ++i)
            {
                C[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true);
            }
            string benchmarksDir = text_dir.Text,
                searchPattern = text_filter.Text;

            
            string[] fileEntries = Directory.GetFiles(benchmarksDir, searchPattern);            

            
            Stopwatch stopwatch = Stopwatch.StartNew(); 

            for (int par = 0; par < param_list_size; ++par)
            {
                if (param_list[par].Text == "<>") continue;
                bg.ReportProgress(0, "- - - - - " + param_list[par].Text + "- - - - - ");
                string exe = text_exe.Text + " " + param_list[par].Text + " ";
                failed = 0;

                foreach (string fileName in fileEntries)
                {
                    int i = 0;
                    while (true)
                    {
                        //run(@"powershell -ExecutionPolicy ByPass -File  c:\temp\cores.ps1", true);
                        //run("unix2dos report.txt", true);
                        //if (run("grep -c \" 0 %\" report.txt", true) == 0) break;
                        int load;
                        float fload;
                        bool first = false;
                        for (; i < cores; ++i)
                        {
                            fload = C[i].NextValue();
                            load = Convert.ToInt32(fload);
                            bg.ReportProgress(-1, "load = " + fload.ToString());
                            if (load < 3)
                            {
                                if (!first) first = true;   // this way we make sure two cores are free, to leave one for OS and such. 
                                else break;
                            }
                        }
                        Int64 AvailableMem = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
                        if (i == cores) {
                            bg.ReportProgress(-1, "all cores busy...");                            
                            i = 0; 
                        }
                        else
                        {
                            if (AvailableMem > MinMem) break;
                            else bg.ReportProgress(0, "not enough memory...");                            
                            
                        }
                        bg.ReportProgress(0, "echo waiting 5 sec....");                            
                        System.Threading.Thread.Sleep(5000);// 5 seconds wait
                    }
                    bg.ReportProgress(0, "running " + fileName);
                    run(exe + fileName + " > " + outfile(fileName));
                    // run_native(fileName);

                }

                // waiting for all processes to end before collecting statistics.

                foreach (Process p in processes)
                {                    
                    if (!p.HasExited)
                    {                        
                        bg.ReportProgress(0, "waiting for " + p.StartInfo.Arguments);
                        p.WaitForExit();
                    }
                }
                
                // processing output
                
                cnt = 0;
                results.Clear();
                foreach (string fileName in fileEntries) process_out(fileName, par, ref cnt, ref csvheader);
                
                bg.ReportProgress(0, "fails = " + failed.ToString());
                foreach (DictionaryEntry de in results) bg.ReportProgress(0, "Total `" + de.Key.ToString() + "' reported by exe = " + de.Value.ToString());                
            }
            
            stopwatch.Stop();
            string time = (Convert.ToSingle(stopwatch.ElapsedMilliseconds)/1000.0).ToString();
            bg.ReportProgress(0, "# of benchmarks:" + cnt);
            
            bg.ReportProgress(0, "parallel time = " + time);           
            bg.ReportProgress(0, "============================");

            bg.ReportProgress(1, time); //label_paralel_time.Text
            try { bg.ReportProgress(2, results["time"].ToString()); }
            catch { };  //label_total_time.Text}
            bg.ReportProgress(3, cnt.ToString()); // label_cnt.Text 
            bg.ReportProgress(5, failed.ToString());
            bg.ReportProgress(4, ""); // button1.Enabled = true;

            csvfile.Write("param, bench, " + csvheader);           
            csvfile.WriteLine();
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
                csvfile = new System.IO.StreamWriter(text_csv.Text);      //(@"C:\temp\res.csv");
                //csvfile.WriteLine("param, bench, time");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot open the csv file!\n" + ex.ToString());
                return;
            }

            button1.Enabled = false;
            bg.WorkerReportsProgress = true;
            bg.RunWorkerAsync();
        }

        private void button_kill_Click(object sender, EventArgs e)
        {
            foreach (Process p in processes)
            {
                if (!p.HasExited) p.Kill();
            }
        }
        
        #endregion
    }


    


}

