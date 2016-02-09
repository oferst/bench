using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;



namespace bench
{
    // todo: timeout doesn't kill the actual process, only the cmd process. 
    public partial class Form1 : Form
    {

        Hashtable processes = new Hashtable();  // from process to <args, benchmark, list of results>
        List<System.Threading.Timer> timers = new List<System.Threading.Timer>();
        List<string> labels = new List<string>();
        static int param_list_size = 16;
        string graphDir = @"c:\temp\cpbm-0.5\";        
        TextBox[] param_list = new TextBox[param_list_size];
        RadioButton[] scatter1 = new RadioButton[param_list_size];
        RadioButton[] scatter2 = new RadioButton[param_list_size];
        int timeout = Timeout.Infinite;
        int MinMem = 1000;  // in MB        
        int cores = 8;
        int[] active = new int[8]; // {3, 5, 7 }; //note that we push all other processes to 1,2  [core # begin at 1]. with hyperthreading=off use {2,3,4}
        int failed = 0;        
        bool hyperthreading = true;
        StreamWriter logfile = new System.IO.StreamWriter(@"C:\temp\log.txt");
        string timestring = "totalTimeNoInitialCheck"; // "time"   -- for the scatter/cactus plots, we compare this field. 
        StreamWriter csvfile;        
        Hashtable csv4plot = new Hashtable();        
        string csvtext;
        Hashtable accum_results = new Hashtable();
        Hashtable results = new Hashtable();
        AbortableBackgroundWorker bg;
        HashSet<string> entries = new HashSet<string>();
        int bench_bound = 500;

        public Form1()
        {
            InitializeComponent();
            GroupBox radioset1 = new GroupBox();
            GroupBox radioset2 = new GroupBox();
            radioset1.Location = new Point(2, 0);
            radioset2.Location = new Point(30, 0);
            radioset1.Size = new Size(20, param_list_size * 25);
            radioset2.Size = new Size(20, param_list_size * 25);

            for (int i = 0; i < param_list_size; ++i)
            {
                param_list[i] = new TextBox();
                //param_list[i].Location = new System.Drawing.Point(60, 345 + i * 30);
                param_list[i].Location = new System.Drawing.Point(60,  i * 25);
                param_list[i].Size = new System.Drawing.Size(429, 20);
                param_list[i].Text = "<>";
                panel1.Controls.Add(param_list[i]);

                scatter1[i] = new RadioButton();
                
                scatter1[i].Location = new System.Drawing.Point(0, i * 25);
                radioset1.Controls.Add(scatter1[i]);
                scatter2[i] = new RadioButton();
            
                scatter2[i].Location = new System.Drawing.Point(0, i * 25);
                radioset2.Controls.Add(scatter2[i]);
            }
            scatter1[0].Checked = scatter2[1].Checked = true; 
            panel1.Controls.Add(radioset1);
            panel1.Controls.Add(radioset2);
            
            // active cores: 
            checkedListBox1.SetItemCheckState(0, CheckState.Checked);
            checkedListBox1.SetItemCheckState(2, CheckState.Checked);
            checkedListBox1.SetItemCheckState(4, CheckState.Checked);

            checkBox_remote.Checked = false;
            checkBox_rec.Checked = true;


            param_list[0].Text = "-file";
            param_list[1].Text = "-rotate -file";
            param_list[2].Text = "-rotate -fth 1 -file";
            /*param_list[0].Text = "-pf-mode=2";            
            param_list[1].Text = "-pf-mode=3";
            param_list[2].Text = "-pf-mode=4";
            param_list[3].Text = "-pf-mode=2 -pf_force";
            param_list[4].Text = "-pf-mode=3 -pf_force";
            param_list[5].Text = "-pf-mode=4 -pf_force";
            */
            //            param_list[3].Text = "-pf-mode=3 -no-always_prove";
            /*            param_list[0].Text = "-pf-mode=2 -no-always_prove -pf_force";
                        param_list[1].Text = "-pf-mode=2 -no-always_prove";
                        param_list[2].Text = "-pf-mode=2 -pf_force";
                        param_list[3].Text = "-pf-mode=2";
                        param_list[4].Text = "-pf-mode=3 -no-always_prove -pf_force";
                        param_list[5].Text = "-pf-mode=3 -no-always_prove";
                        param_list[6].Text = "-pf-mode=3 -pf_force";
                        param_list[7].Text = "-pf-mode=3";
                        param_list[8].Text = "-pf-mode=4 -no-always_prove -pf_force";
                        param_list[9].Text = "-pf-mode=4 -no-always_prove";
                        param_list[10].Text = "-pf-mode=4 -pf_force";
                        param_list[11].Text = "-pf-mode=4";
                        param_list[12].Text = "-pf-mode=2 -no-always_prove -pf_force -pf_unsatopt";
                        param_list[13].Text = "-pf-mode=2 -no-always_prove -pf_unsatopt";
                        param_list[14].Text = "-pf-mode=2 -pf_force -pf_unsatopt";
                        param_list[15].Text = "-pf-mode=2 -pf_unsatopt";
              */          //param_list[4].Text = "-pf-mode=2 -no-always_prove -pf_unsatopt ";
                          //          param_list[5].Text = "-pf-mode=3 -no-always_prove -pf_unsatopt ";
                          /*          param_list[5].Text = "-pf-mode=2 -pf_z_budget=80";
                                      param_list[6].Text = "-pf-mode=2 -pf_z_budget=120";

                                      param_list[7].Text = "-pf-mode=3 -pf_z_budget=80";
                                      param_list[8].Text = "-pf-mode=3 -pf_z_budget=120";
                                      param_list[9].Text = "-pf-mode=3 -pf_z_budget=160";

                                      param_list[10].Text = "-pf-mode=4 -pf_z_budget=80";
                                      param_list[11].Text = "-pf-mode=4 -pf_z_budget=120";

                                      param_list[12].Text = "-pf-mode=4 -pf_z_budget=80 -pf-delay=5";
                                      param_list[13].Text = "-pf-mode=4 -pf_z_budget=80 -pf-delay=10";
                            */
                          /*
                          param_list[5].Text="-pf-mode=3 -pf_reset_z_budget -pf_z_budget=20";
                          param_list[6].Text="-pf-mode=3 -pf_reset_z_budget -pf_z_budget=40";
                          param_list[7].Text="-pf-mode=3 -pf_reset_z_budget -pf_z_budget=80";
                          param_list[8].Text="-pf-mode=3 -pf_z_budget=20";
                          param_list[9].Text="-pf-mode=3 -pf_z_budget=40";
                          param_list[0].Text="-pf-mode=2 -pf_z_budget=20"; 
                          param_list[1].Text="-pf-mode=2 -pf_z_budget=40";
                          param_list[2].Text="-pf-mode=4 -pf_z_budget=20"; 
                          param_list[3].Text="-pf-mode=4 -pf_z_budget=40";
                          param_list[4].Text = "-pf-mode=4 -pf_z_budget=80 -pf-delay=15";
                            */



            //text_filter.Text = "2dlx_ca_mc_ex_bp_f_new.gcnf";
            //text_filter.Text = "*.cnf";
            text_filter.Text = "neq004_size4.smt";
            //text_dir.Text = @"C:\temp\gcnf_test\belov\";
            //text_dir.Text = @"C:\temp\muc_test\SAT02\industrial\biere\cmpadd";
            //text_dir.Text = @"C:\temp\muc_test\marques-silva\hardware-verification";
            //text_dir.Text = @"C:\temp\muc_test\SAT11\mus\";
            //text_dir.Text = @"C:\temp\small";
            //text_dir.Text = @"C:\temp\muc_test\small";
            //text_dir.Text = @"C:\temp\muc_test\SAT11\mus_satcomp09-main-track-selected";
            //text_dir.Text = @"C:\temp\muc_test\SAT11\application";
            //text_dir.Text = @"C:\temp\muc_test\SAT02\industrial_smtlib";
            //text_dir.Text = @"C:\temp\muc_test\SAT11\mus\marques-silva\hardware-verification";
            text_dir.Text = @"C:\temp\smtmuc\benchmarks";
            //text_dir.Text = @"C:\temp\muc_test\SAT11\mus\marques-silva\bmc-default";
            //text_dir.Text = @"C:\temp\muc_test\SAT02\sat-2002-beta";
            //text_exe.Text = "\"C:\\Users\\ofers\\Documents\\Visual Studio 2012\\Projects\\hmuc\\x64\\Release\\hmuc.exe\" ";
            text_exe.Text = @"C:\temp\smtmuc\smt_muc.exe";

          //  text_exe.Text = @"C:\Users\Ofer\Documents\Visual Studio 2012\Projects\hmuc\x64\Release\hmuc.exe";
          //text_exe.Text = "\"C:\\Users\\ofers\\Documents\\Visual Studio 2012\\Projects\\hmuc\\x64\\Release\\hmuc.exe\"";
            text_minmem.Text = MinMem.ToString();
            text_timeout.Text = "900";
            //text_csv.Text = @"c:\temp\res_force.csv";
            text_csv.Text = @"c:\temp\smtmuc\res.csv";
        }

        #region utils

        string normalize_string(string s)
        {
            return s.Replace("=","").Replace(" ", "").Replace("-","").Replace("_",""); // to make param a legal file name
        }

        string getid(string param, string filename)
        {
            return "P: " + param + "," +
                Path.GetDirectoryName(filename) + "," +  // benchmark
                Path.GetFileName(filename);
        }

        void init_csv_file()
        {            
            csvfile = new System.IO.StreamWriter(text_csv.Text, checkBox_append.Checked);      //(@"C:\temp\res.csv");   
        }

        void readEntries()
        {            
            StreamReader csvfile = new System.IO.StreamReader(text_csv.Text);      //(@"C:\temp\res.csv");
            string line, res;
            csvfile.ReadLine(); // header
            int i;
            while ((line = csvfile.ReadLine()) != null)
            {
                i = 0;
                for (int j = 0; j < 3; ++j) {
                    i = line.IndexOf(',', i+1);
                }
                System.Diagnostics.Debug.Assert(i >= 0);
                res = line.Substring(0,i);
                if (line.IndexOf(',', i + 1) > i + 1) entries.Add(res);  // in case we have after file name ",," it means that time was not recorded; 
                else listBox1.Items.Add("remove line: " + line);
            }
            csvfile.Close();
        }

        void kill_process(Object stateinfo)
        {
            Process p = (Process)stateinfo;
            if (!p.HasExited)
            {
                bg.ReportProgress(0, "timeout: process killed: " + p.StartInfo.Arguments);
                Tuple<string, string, List<float>> data = ((Tuple<string, string, List<float>>)processes[p]);
                List<float> l = (List<float>)data.Item3;
                l.Add(Convert.ToInt32(text_timeout.Text) + 1); // +1 for debugging                
                p.Kill();
            }            
        }

        string outfile(string filename, string param)
        {
            return filename + "." + normalize_string(param) + ".out";
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

        /* obsolete: for reading output online.
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
        } */

        bool read_out_file(Process p, string filename)
        {
            StreamReader file =   new StreamReader(filename);
            string line;
            while ((line = file.ReadLine()) != null)            
            {                
                if (line.Length >= 4 && line.Substring(0, 3) == "###")
                {
                    var parts = line.Split(new char[] { ' ' });
                    string tag = parts[1];
                    if (tag == "SAT") return false;
                    if (tag == "Abort")
                    {
                        listBox1.Items.Add("* * * * * * * * * * * * *  Abort!");
                        file.Close();
                        return true;
                    }

                    
                    float res;
                    if (Single.TryParse(parts[2], out res))
                    {
                        if (!labels.Exists(x => x == tag)) labels.Add(tag);
                        Tuple<string, string, List<float>> data = ((Tuple<string, string, List<float>>)processes[p]);
                        data.Item3.Add(res);
                    }
                    else listBox1.Items.Add("skipping non-numerical data: " + parts[2]);
                }
            }
            file.Close();
            return true;
        }


        void wait_for_Termination()
        {
            foreach (DictionaryEntry entry in processes)
            {
                Process p1 = (Process)entry.Key;
                if (!p1.HasExited)  p1.WaitForExit();
            }
        }

        void buildcsv()
        {
            string csvheader = "";
            create_plot_files();
            foreach (DictionaryEntry entry in processes)
            {
                Tuple<string, string, List<float>> trio = entry.Value as Tuple<string, string, List<float>>;
                Process p1 = (Process)entry.Key;
                
                List<float> l = (List<float>)trio.Item3;
                csvtext += getid(trio.Item1, trio.Item2) + ","; // benchmark

                for (int i = 0; i < l.Count; ++i)
                    csvtext += l[i].ToString() + ",";
                if (l.Count == 0)   // in case of timeout / mem-out / whatever
                {
                    csvtext += (Convert.ToInt32(text_timeout.Text) * 10).ToString() + ","; //supposed to get here only on memout/fail (not time-out). We add 10 times the time-out to make sure it is noticeable and not taken as part of the average. 
                    //fails.Add(trio.Item2);
                    //try { csvtext += "-1"; }//Convert.ToInt32(text_timeout.Text); }
                    //catch { }
                }
                csvtext += "\n";
                
                try
                {
                    if (l.Count > 0)  // if it is 0, it implies that it was a fail (typically T.O.).
                    ((System.IO.StreamWriter)csv4plot[normalize_string(trio.Item1)]).WriteLine(
                        trio.Item2 + "," + // full benchmark path
                        normalize_string(trio.Item1) + "," + // param
                        l[labels.IndexOf(timestring)].ToString() + "," +
                        text_timeout.Text + "s");
                }
                catch (Exception ex) { listBox1.Items.Add("exception: " + ex.Message); }
            }
            foreach (string lbl in labels) csvheader += lbl + ",";
            
            if (!checkBox_append.Checked)
            {
                csvfile.Write("param, dir, bench, " + csvheader);
                csvfile.WriteLine();
            }
            csvfile.Write(csvtext);
            csvfile.Close();
            foreach (var key in csv4plot.Keys) ((System.IO.StreamWriter)csv4plot[key]).Close(); 
        }

        void run_remote(string cmd, string args) // for unix commands. Synchronous. 
        {
            Process p = new Process();         

            p.StartInfo.FileName = cmd;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = false;
            p.StartInfo.CreateNoWindow = true;            


            try
            {
                p.Start();
            }
            catch { MessageBox.Show("cannot start process" + p.StartInfo.FileName); throw; }
            p.WaitForExit();
        }
         
        Process run(string cmd, string args, string outfilename, int affinity = 0x007F)
        {
            Process p = new Process();

            
            p.StartInfo.FileName = cmd;
            p.StartInfo.Arguments = args;

            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;

            //process.MaxWorkingSet = new IntPtr(2000000000); //2Gb                
            
            if (File.Exists(outfilename)) File.Delete(outfilename);
            p.OutputDataReceived += (s, e) => File.AppendAllText(outfilename, e.Data + "\n"); 
            try
            {                
                p.Start();
                p.BeginOutputReadLine();
            }
            catch { MessageBox.Show("cannot start process" + p.StartInfo.FileName); throw; }
            
            
            p.ProcessorAffinity = (IntPtr)affinity;
            p.PriorityClass = ProcessPriorityClass.RealTime;

            var timer = new System.Threading.Timer(kill_process, p, timeout, Timeout.Infinite);
            timers.Add(timer); // needed ?
            return p;
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {            
            int cnt = 0, bench_cnt = 0;
            Process[] p = new Process[cores + 1];
            
            string benchmarksDir = text_dir.Text,
                searchPattern = text_filter.Text;
            
            var fileEntries = new DirectoryInfo(benchmarksDir).GetFiles(searchPattern, checkBox_rec.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (fileEntries.Length == 0) bg.ReportProgress(0, "empty file list\n");

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool ok = false;
            bool copied = true; // !! temporary. because on remote we have all the files there anyway
            for (int par = 0; par < param_list_size; ++par)  // for each parameter
            {
                if (param_list[par].Text == "<>") continue;
                bg.ReportProgress(0, "- - - - - " + param_list[par].Text + "- - - - - ");
                failed = 0;
                results.Clear();
                accum_results.Clear();
                cnt ++;
                bench_cnt = 0;
                foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                {
                    string fileName = fileinfo.FullName;
                    string id = getid(param_list[par].Text, fileName);
                    if (entries.Contains(id)) continue;
                    if (++bench_cnt == bench_bound) break;
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
                                    

                                    if (checkBox_remote.Checked)
                                    {
                                        string bench = Path.GetFileName(fileName);
                                        if (!copied)
                                        {
                                            bg.ReportProgress(0, "running " + fileName + " remotely. ");
                                            File.Copy(fileName, bench, true); // copying benchmark to work dir. 
                                            run_remote("scp", bench + " ofers@tamnun.technion.ac.il:~/hmuc/test");
                                            File.Delete(bench);
                                        }
                                        run_remote("ssh", "ofers@tamnun.technion.ac.il \"cd hmuc;qsub -v bench=" + bench + ",arg='" + param_list[par].Text + "',argname=" + normalize_string(param_list[par].Text) + " hmuc.sh\"");
                                    }
                                    else
                               //     try
                                    {
                                        string outfilename = outfile(fileName, param_list[par].Text);
                                        if (!checkBox_out.Checked || !File.Exists(outfilename) || (new FileInfo(outfilename)).Length == 0)
                                        {
                                            bg.ReportProgress(0, "running " + fileName + " on core " + i.ToString());
                                            p[i] = run(text_exe.Text, param_list[par].Text + " " + fileName, outfilename, 1 << (i - 1));
                                            List<float> l = new List<float>();
                                            processes[p[i]] = new Tuple<string, string, List<float>>(param_list[par].Text, fileName, l);
                                        }
                                        else bg.ReportProgress(0, "skipping " + fileName + " due to existing out file.");
                                    }
                                    //catch {
                                    //        bg.ReportProgress(0, "failed");
                                    //        return;
                                    //    }
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
                copied = true;
            }

            // post processing

            bg.ReportProgress(4, ""); // button1.Enabled = true;
            
            if (checkBox_remote.Checked) return;
            wait_for_Termination();            

            bg.ReportProgress(0, "* all processes finished *");
            stopwatch.Stop();
                        
            string time = (Convert.ToSingle(stopwatch.ElapsedMilliseconds) / 1000.0).ToString();
            bg.ReportProgress(0, "# of benchmarks:" + cnt);

            bg.ReportProgress(0, "parallel time = " + time);
            bg.ReportProgress(0, "============================");

            bg.ReportProgress(1, time); //label_paralel_time.Text
            try { bg.ReportProgress(2, accum_results[timestring].ToString()); }
            catch { };  //label_total_time.Text}
            bg.ReportProgress(3, cnt.ToString()); // label_cnt.Text 
            bg.ReportProgress(5, failed.ToString());
            
            bg.ReportProgress(6, ""); // import_out_to_csv        
                       
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
                case 6: System.Diagnostics.Debug.Assert(!checkBox_remote.Checked); import_out_to_csv(); break;
            }
        }

        #endregion

        #region GUI


        void create_plot_files()
        {
            try
            {
             //   if (!checkBox_remote.Checked)   
                    for (int par = 0; par < param_list_size; ++par)
                    {
                        string param = normalize_string(param_list[par].Text);
                        if (param == "<>") continue;
                        csv4plot[param] = new System.IO.StreamWriter(graphDir + param + ".csv");
                        ((System.IO.StreamWriter)csv4plot[param]).WriteLine("Benchmark,command,usertime,timeout");
                    }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot create csv files!\n" + ex.ToString());
                return;
            }
        }


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
                if (checkBox_append.Checked && File.Exists(text_csv.Text)) readEntries();
                else entries.Clear();
                //init_csv_file();             
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

            if (!checkBox_remote.Checked)
            {
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
            }
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

        private int getCheckedRadioButton(RadioButton[] c)
        {                 
           for (int i = 0; i < c.Length; i++)                
               if (c[i].Checked) return i;                
           return -1;
        }

        private void button_scatter_Click(object sender, EventArgs e)
        {
            int param1 = getCheckedRadioButton(scatter1);
            if (param1 == -1) return;
            int param2 = getCheckedRadioButton(scatter2);
            if (param2 == -1) return;
            
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "run-scatter.bat";
            string f1 = normalize_string(param_list[param1].Text), f2 = normalize_string(param_list[param2].Text);
            startInfo.Arguments =  String.Compare(f1, f2) < 0 ? f1 + " " + f2 : f2 + " " + f1; // apparently make_graph treats them alphabetically, so we need to give them alphabetically to know what pdf is eventually generated. 
            startInfo.WorkingDirectory = graphDir;
            p.StartInfo = startInfo;
            p.Start();
        }

        private void button_cactus_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "run-cactus.bat";
            startInfo.Arguments = "";
            for (int par = 0; par < param_list_size; ++par)  // for each parameter
            {
                if (param_list[par].Text == "<>") continue;
                startInfo.Arguments += " " + normalize_string(param_list[par].Text) + ".csv";
            }
            startInfo.WorkingDirectory = graphDir;
            startInfo.CreateNoWindow = false;
            p.StartInfo = startInfo;
            p.Start();

        }

        private void checkBox_remote_CheckedChanged(object sender, EventArgs e)
        {
            button_kill.Enabled = checkedListBox1.Enabled = !(((CheckBox)sender).Checked);
        }

        // Gets field # idx in a comma-separated line
        private string getfield(string line, int idx)
        {
            int i=0;
            for (int j = 0; j < idx-1; ++j)
            {
                i = line.IndexOf(',', i + 1);
            }
            string res = line.Substring(i+1, line.IndexOf(',', i + 1)-i-1);
            return res;
        }


        private void del_Allfail_benchmark()
        {
            Hashtable benchmarks = new Hashtable();
            string fileName = text_csv.Text;
            HashSet<string> failed_all = new HashSet<string>();
            int cnt = 0;
            // finding failed benchmarks 
            foreach (string line in File.ReadLines(fileName)) {
                benchmarks[getfield(line, 3)] = getfield(line,2);
            }
            foreach (string line in File.ReadLines(fileName))
            {
                string time = getfield(line, 4);
                double d;
                bool isdouble = double.TryParse(time, out d);
                if (isdouble)
                {
                    if (d <= Convert.ToInt32(text_timeout.Text) + 1)
                    {
                        benchmarks.Remove(getfield(line, 3));
                    }
                }
            }

            foreach (string key in benchmarks.Keys)
            {
                string path = benchmarks[key] + "\\" +  key;
                listBox1.Items.Add("deleting All-failed benchmark " + path);
                failed_all.Add(key);
                cnt++;
                try { File.Delete(path); }
                catch { }
            }
            listBox1.Items.Add("Deleted benchmarks: " + cnt);

            var linesToKeep = File.ReadLines(fileName).Where(l => !failed_all.Contains(getfield(l, 3)));
            var tempFile = Path.GetTempFileName();

            File.WriteAllLines(tempFile, linesToKeep);

            File.Delete(fileName);
            File.Move(tempFile, fileName);           
        }
        

        private void del_short_calls()
        {
            string fileName = text_csv.Text;
            HashSet<string> failed_short_once = new HashSet<string>();
            // finding benchmarks with short longestcall
            foreach (string line in File.ReadLines(fileName))
            {
                string longesttime = getfield(line, 10);
                double d;
                bool isdouble = double.TryParse(longesttime, out d);
                if (isdouble)
                {
                    if (d < 1.0)
                    {
                        listBox1.Items.Add("short longesttime line: " + line);
                        failed_short_once.Add(getfield(line, 3));
                    }
                }
            }

            // keeping only benchmarks that take time. 
            var linesToKeep = File.ReadLines(fileName).Where(l => (!failed_short_once.Contains(getfield(l, 3)) || getfield(l, 1) == "param"));   // second item so it includes the header.

            var tempFile = Path.GetTempFileName();

            File.WriteAllLines(tempFile, linesToKeep);

            File.Delete(fileName);
            File.Move(tempFile, fileName);
        }


        
        private void del_fails_Click(object sender, EventArgs e)
        {            
            string fileName = text_csv.Text;
            HashSet<string> failed_atleast_once = new HashSet<string>();        
            // finding failed benchmarks 
            foreach (string line in File.ReadLines(fileName)) 
            {
                string time = getfield(line, 4);
                double d;
                bool isdouble = double.TryParse(time, out d);
                if (isdouble)
                {
                    if (d > Convert.ToInt32(text_timeout.Text) + 1)
                    {
                        listBox1.Items.Add("failed line: " + line);
                        failed_atleast_once.Add(getfield(line, 3));
                    }
                }                
            }
            
            // keeping only benchmarks that are not failed by any parameter combination. 
            var linesToKeep = File.ReadLines(fileName).Where(l => (!failed_atleast_once.Contains(getfield(l, 3)) || getfield(l, 1) == "param"));   // second item so it includes the header.         
            
            var tempFile = Path.GetTempFileName();

            File.WriteAllLines(tempFile, linesToKeep);

            File.Delete(fileName);
            File.Move(tempFile, fileName);

        }

        void import_out_to_csv()
        {
            int in_csv = 0, file_exists = 0, file_not_exist = 0;
            string benchmarksDir = text_dir.Text,
            searchPattern = text_filter.Text;
            int bench_cnt = 0;
            listBox1.Items.Add("--- Importing ---");
            var fileEntries = new DirectoryInfo(benchmarksDir).GetFiles(searchPattern, checkBox_rec.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (fileEntries.Length == 0) listBox1.Items.Add("empty file list\n");
            
            processes.Clear();
            if (checkBox_append.Checked && File.Exists(text_csv.Text)) readEntries();
            else entries.Clear();
            if (checkBox_remote.Checked) listBox1.Items.Add("Files will be imported to " + Directory.GetCurrentDirectory());
            for (int par = 0; par < param_list_size; ++par)  // for each parameter
            {
                if (param_list[par].Text == "<>") continue;
                bench_cnt = 0;
                foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                {
                    string fileName = fileinfo.FullName;                    
                    string id = getid(param_list[par].Text, fileName);
                    if (entries.Contains(id)) { in_csv++; continue; }
                    if (++bench_cnt == bench_bound) break;
                    string outfileName = "";
                    if (checkBox_remote.Checked)
                    {
                        outfileName = outfile(Path.GetFileName(fileName), param_list[par].Text); // we import from the working directory (bench/bin/release/ or debug/)
                        if (!File.Exists(outfileName)) run_remote("scp", "ofers@tamnun.technion.ac.il:~/hmuc/test/" + outfileName + " " + outfileName); // download the file
                    }
                    else
                    {
                        outfileName = outfile(fileName, param_list[par].Text); // we import from the same directory as the source cnf file
                    }                    
                    if (File.Exists(outfileName))
                    {
                        Process p = new Process(); // we are only using this process as a carrier of the information from the file, so we can use the buildcsv function. 
                        List<float> l = new List<float>();
                        processes[p] = new Tuple<string, string, List<float>>(param_list[par].Text, fileName, l);
                        if (!read_out_file(p, outfileName)) { 
                            listBox1.Items.Add(fileName + " is SAT. Deleting."); 
                            File.Delete(fileName);
                            processes.Remove(p);
                        }
                        file_exists++;
                    }
                    else
                    {
                        listBox1.Items.Add(outfileName + " is missing");
                        file_not_exist++;
                    }
                }
            }
            
            try
            {
                init_csv_file();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot open the csv file!\n" + ex.ToString());
                return;
            }
            listBox1.Items.Add(in_csv.ToString() + " benchmarks already in the csv file.");
            listBox1.Items.Add(file_exists.ToString() + " benchmark results imported from out files.");
            listBox1.Items.Add(file_not_exist.ToString() + " outfile missing.");
            
            buildcsv();
        }

        private void button_import_Click(object sender, EventArgs e)  // import out files from remote server, and process them to generate the csv + plot files. 
        {
            import_out_to_csv();
        }

        private void button_del_allfail_Click(object sender, EventArgs e) // delete benchmarks that no combination of parameters solved.
        {
            del_Allfail_benchmark();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            del_short_calls();
        } 
    }
}

