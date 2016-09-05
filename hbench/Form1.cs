/*
 * HBench - a GUI-based platform for performance benchmarking
 * Author: Ofer Strichman ofers@ie.technion.ac.il
 * Distributed freely under the GPL license
 */


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Management;

namespace bench
{    
    public partial class filter : Form
    {
        // reading from config  file: 
        string history_file = Path.Combine(Application.StartupPath, ConfigurationManager.AppSettings["history_filename"]);//"history.txt"
        string graphDir = ConfigurationManager.AppSettings["cpbm"]; //@"c:\temp\cpbm-0.5\";        
        StreamWriter logfile = new StreamWriter(ConfigurationManager.AppSettings["log"]); // @"C:\temp\log.txt");        
        string stat_tag = ConfigurationManager.AppSettings["stat_tag"]; // ###
        string abort_tag = ConfigurationManager.AppSettings["abort_tag"];
        bool hyperthreading = ConfigurationManager.AppSettings["hyperthreading"] == "true";
        static int param_list_size = int.Parse(ConfigurationManager.AppSettings["param_list_size"]);

        // more configurations:   
        int timeout_val = Timeout.Infinite; // will be read from history file
        int MinMem_val = 0;  // in MB. Will be read from history file        
        bool preserveCores12 = ConfigurationManager.AppSettings["PreserveFirstCores"] == "true"; 
        int firstcore;  
        int cores = Environment.ProcessorCount;
        int[] active = new int[8]; // {3, 5, 7 }; //note that we push all other processes to 1,2  [core # begin at 1]. with hyperthreading=off use {2,3,4}
        int failed = 0;
        const string labelTag = "#";
        const string noOpTag = "<>";
        const char setSeparator = '|';

        enum fields {
            exe, dir, filter_str, csv, param, param_groups, stat_field, core_list, timeout, min_mem,  // combos
            checkBox_skip_long_runs, checkBox_remote, checkBox_rec, checkBox_rerun_empty_out, checkBox_filter_out, checkBox_filter_csv, checkBox_copy, // checkboxes
            misc }; // elements maintained in the history file

        // declarations:
        Hashtable processes = new Hashtable();  // from process to <args, benchmark, list of results>
        List<string> failed_benchmarks;
        List<System.Threading.Timer> timers = new List<System.Threading.Timer>();
        List<string> labels = new List<string>();        
        TextBox[] param_list = new TextBox[param_list_size];
        List<string> ext_param_list = new List<string>(); 
        RadioButton[] scatter1 = new RadioButton[param_list_size];
        RadioButton[] scatter2 = new RadioButton[param_list_size];
        StreamWriter csvfile;        
        Hashtable csv4plot = new Hashtable();                
        Hashtable accum_results = new Hashtable();
        Hashtable results = new Hashtable();
        AbortableBackgroundWorker bg;
        HashSet<string> entries = new HashSet<string>();        
        Dictionary<fields, List<string>> history;        
        bool write_history_file = false;
        string benchmarksDir, searchPattern, csvtext;
        List<int> kill_list = new List<int>();
        
        public filter()
        {
            InitializeComponent();            
            GroupBox radioset1 = new GroupBox();
            GroupBox radioset2 = new GroupBox();
            radioset1.Location = new Point(2, 0);
            radioset2.Location = new Point(30, 0);
            radioset1.Size = new Size(20, param_list_size * 25);
            radioset2.Size = new Size(20, param_list_size * 25);
            failed_benchmarks = new List<string>();

            firstcore = preserveCores12 ? 3 : 1;
            for (int i = firstcore; i <= cores; ++i)  // cores 1,2 are preserved for other processes. 
                checkedListBox_cores.Items.Add("c" + i.ToString());

            ToolTip scatter_tt = new ToolTip();

            for (int i = 0; i < param_list_size; ++i)
            {
                param_list[i] = new TextBox();                
                param_list[i].Location = new Point(60,  i * 25);
                param_list[i].Size = new Size(429, 20);     
                panel1.Controls.Add(param_list[i]);

                scatter1[i] = new RadioButton();                
                scatter1[i].Location = new Point(0, i * 25);
                scatter_tt.SetToolTip(scatter1[i], "First param for scatter plot");
                radioset1.Controls.Add(scatter1[i]);

                scatter2[i] = new RadioButton();            
                scatter2[i].Location = new Point(0, i * 25);
                scatter_tt.SetToolTip(scatter2[i], "Second param for scatter plot");
                radioset2.Controls.Add(scatter2[i]);
            }
            scatter1[0].Checked = scatter2[1].Checked = true; 
            panel1.Controls.Add(radioset1);
            panel1.Controls.Add(radioset2);

            read_history(history_file);
            checkBox_rerun_empty_out.Enabled = checkBox_filter_out.Checked;
            checkBox_copy.Enabled = checkBox_remote.Checked;
            
        }

        #region history
        void read_history(string history_file)
        {
            history = new Dictionary<fields, List<string>>();
            string[] lines = new string[]{""};
            try {
                lines = File.ReadAllLines(history_file);
            }
            catch
            {
                MessageBox.Show(history_file + " not found (in the hbench directory). Copy history_default.txt to history.txt and restart.");
                Environment.Exit(1);
            }
            fields fieldValue = fields.misc;

            // reading history file

            foreach (string line in lines)
            {
                if (line.Length == 0) continue;
                if (line.Length >=2 && line.Substring(0,2) == "--")
                {
                    string key = line.Substring(3);
                    try { fieldValue = (fields)Enum.Parse(typeof(fields), key); }
                    catch { MessageBox.Show(key + " is not a valid field name in file " + history_file + ". Aborting."); return; }
                    history[fieldValue] = new List<string>();
                    continue;
                }
                history[fieldValue].Add(line);
            }

            // associating history with the combo-s
            foreach (Control C in Controls)
            {
                if (C.GetType() == typeof(ComboBox))
                {
                    try
                    {
                        fields field = (fields)Enum.Parse(typeof(fields), C.Name); // name of combo must be identical to the item in the enum list. 
                        BindingSource bs = new BindingSource();
                        bs.DataSource = history[field];
                        ((ComboBox)C).DataSource = bs;
                    }
                    catch
                    { }   // could be missing entry in the history file, so we let it go through. 
                }
                else if (C.GetType() == typeof(CheckBox))
                {
                    try
                    {
                        fields field = (fields)Enum.Parse(typeof(fields), C.Name);
                        ((CheckBox)C).Checked = history[field][0] == "yes";
                    }
                    catch
                    { }
                }
            }

            // updating core list
            try {
                string[] corelist = (history[fields.core_list][0]).Split(',');
                foreach (string st in corelist)
                {
                    int c;
                    if (int.TryParse(st, out c) == false || (c < firstcore) || c > cores) MessageBox.Show("field core_list in history file contains bad core indices (should be in the range  3.." + cores + " on this machine). Cores 1,2 are saved for other processes.");
                    else checkedListBox_cores.SetItemCheckState(c - firstcore, CheckState.Checked);
                }
            }
            catch { MessageBox.Show("Core list seems to be empty"); }                        
        }

        void write_history()
        {  
            // rewriting history
            StreamWriter file = new StreamWriter(history_file);            
            foreach (fields field in Enum.GetValues(typeof(fields))) {
                
                if (history.Keys.Contains(field))
                {
                    file.WriteLine("-- " + field.ToString());
                    foreach (string line in history[field])
                    {
                        file.WriteLine(line);
                    }
                    file.WriteLine();
                }
            }            

            file.Close();
        }

        #endregion

        #region utils

        
        string normalize_string(string s)
        {
            return s.Replace("=", "").Replace(" ", "").Replace("-", "").Replace("_", "").Replace(labelTag, "").Replace("%f", ""); // to make param a legal file name
        }

        string expand_string(string s, string filename, string param="", string outfilename="")  // the last two are used for remote execution
        {            
            string res = s.Replace("%f", "\"" + filename + "\"").Replace("%p", param).Replace("%o", outfilename);
            if (res == s) return res;
            else return expand_string(res, filename, param, outfilename);  // recursive because the replacing strings may contain %directives themselves.
        }


        string getid(string param, string filename)
        {
            return "P: " + param + "," +
                Path.GetDirectoryName(filename) + "," +  // benchmark
                Path.GetFileName(filename);
        }

        void init_csv_file()
        {
            try {
                csvfile = new StreamWriter(csv.Text, checkBox_filter_csv.Checked);
            }
            catch 
            {
                MessageBox.Show("Cannot open " + csv.Text);
                throw ;
            }
        }

        void readEntries()
        {       
            string line, res;
            StreamReader csvfile;
            try {
                csvfile = new StreamReader(csv.Text);      //(@"C:\temp\res.csv");
                csvfile.ReadLine(); // header
            }
            catch
            {
                MessageBox.Show("seems that " + csv.Text + "is in use");
                return;
            }
            
            int i;
            while ((line = csvfile.ReadLine()) != null)
            {
                i = 0;
                for (int j = 0; j < 3; ++j) {
                    i = line.IndexOf(',', i+1);
                }
                Debug.Assert(i >= 0);
                res = line.Substring(0,i);
                entries.Add(res);
                //if (line.IndexOf(',', i + 1) > i + 1) entries.Add(res);  // in case we have after file name ",," it means that time was not recorded; 
                //else listBox1.Items.Add("remove line: " + line);
            }
            csvfile.Close();
        }


        private void build_process_tree(int pid)
        {
            kill_list.Add(pid);
            Process proc = Process.GetProcessById(pid);
            bg.ReportProgress(0, "added process id = " + proc.Id + " (" + proc.ProcessName + ")");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
               ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                build_process_tree(Convert.ToInt32(mo["ProcessID"]));
            }
        }

        private void KillProcessAndChildren(int pid)
        {
            build_process_tree(pid);
            foreach (int p in kill_list)  // killing them top-down (first parent, then child). The order matters in situations where killing first the child makes the parent think that it terminated and wrote something accordingly. 
            {
                try
                {
                    Process proc = Process.GetProcessById(p);
                    bg.ReportProgress(0, "killing process " + proc.ProcessName);
                    proc.Kill();
                }
                catch { } // in case the process is already dead.
            }
    }

        void kill_process(Object stateinfo)
        {
            Process p = (Process)stateinfo;         
            if (!p.HasExited)
            {
                bg.ReportProgress(0, "timeout: process killed: " + p.StartInfo.Arguments);

                // updating time field                
                benchmark data = (benchmark)processes[p];                
                failed_benchmarks.Add(data.name);
                failed++;
                label_fails.Text = failed.ToString();
                List<float> l = data.res;
                l.Add(Convert.ToInt32(timeout.Text) + 1); // +1 for debugging

                KillProcessAndChildren(p.Id);
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

        bool read_out_file(Process p, string filename, bool first)
        {
            bool success = false;
            
            StreamReader file =   new StreamReader(filename);
            string line;
            while ((line = file.ReadLine()) != null)            
            {                
                if (line.Length >= 4 && line.Substring(0, 3) == stat_tag)
                {
                    var parts = line.Split(new char[] { ' ' });
                    string tag = parts[1];
                    //if (tag == "SAT") // uncomment if we want to erase benchmarks that are SAT.
                    //{
                    //    listBox1.Items.Add("* * * * * * * * * * * * *  SAT!");
                    //    file.Close();
                    //    return false;  
                    //}
                    if (tag == abort_tag || tag == "SAT")  
                    {
                        listBox1.Items.Add("* * * * * * * * * * * * *  Abort!");
                        file.Close();
                        return true;
                    }

                    
                    float res;
                    if (float.TryParse(parts[2], out res))
                    {
                        if (first) {
                            Debug.Assert(!labels.Exists(x => x == tag));
                            labels.Add(tag);
                            success = true;
                        }
                        else {
                            if (!labels.Exists(x => x == tag))
                            {
                                listBox1.Items.Add("label " + tag + " in file " + filename + " did not appear in the first file. Aborting");
                                throw(new Exception("incompatible labels"));
                              //  return true;
                            }
                        }                        
                        benchmark data = (benchmark)processes[p];
                        data.res.Add(res);
                    }
                    else listBox1.Items.Add("skipping non-numerical data: " + parts[2]);
                }
            }
            file.Close();
            if (first)
            {
                if (success) listBox1.Items.Add("reading labels from " + filename);
                else listBox1.Items.Add("failed reading labels from " + filename);
            }
            return success;
        }
        

        void wait_for_remote_Termination()
        {
            string remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
            int res;  
            
            while (true)
            {
                res = run_remote("ssh", remote_user + " \"qstat -u" + ConfigurationManager.AppSettings["remote_user"] + "| grep \"" + ConfigurationManager.AppSettings["remote_user"] + "\"").Item1;
                if (res != 0) break;                
                Thread.Sleep(5000); // 5 seconds wait                        
            }
            listBox1.Items.Add("* All remote processes terminated *");
        }


        void wait_for_Termination()
        {
            foreach (DictionaryEntry entry in processes)
            {
                Process p1 = (Process)entry.Key;
                if (!p1.HasExited)  p1.WaitForExit();
            }
        }

        void buildcsv(bool Addheader)
        {
            string csvheader = "";
            int cnt_wrong_label = 0;
            create_plot_files();
            foreach (DictionaryEntry entry in processes)
            {                
                benchmark bm = entry.Value as benchmark;
                Process p1 = (Process)entry.Key;
                
                List<float> l = bm.res;
                csvtext += getid(bm.param, bm.name) + ","; // benchmark
                if (l.Count > 0)
                {
                    csvtext += ","; // for the 'fail' column
                    for (int i = 0; i < l.Count; ++i)
                        csvtext += l[i].ToString() + ",";
                }
                else   // in case of timeout / mem-out / whatever
                {
                    csvtext += "1,";                 
                 }
                csvtext += "\n";
                
                try
                {
                    if (l.Count > 0)  // if it is 0, it implies that it was a fail (typically T.O.).
                    {
                        int time_col = labels.IndexOf(stat_field.Text);
                        
                        if (time_col < 0) { cnt_wrong_label++; }
                        else
                            ((StreamWriter)csv4plot[normalize_string(bm.param)]).WriteLine(
                            bm.name + "," + // full benchmark path
                            normalize_string(bm.param) + "," + // param
                            l[time_col].ToString() + "," +
                            timeout.Text + "s");
                    }
                }
                catch (Exception ex) { listBox1.Items.Add("exception: " + ex.Message); }
            }
            if (cnt_wrong_label > 0) listBox1.Items.Add("Warning: \"" + stat_field.Text + "\" (the name of the statistics field specified below) is not a column in " + cnt_wrong_label + " out files. Will not add data to statistics.");
            foreach (string lbl in labels) csvheader += lbl + ",";
            
            if (Addheader)
            {
                csvfile.Write("param, dir, bench, fail," + csvheader);
                csvfile.WriteLine();
            }
            try
            {
                csvfile.Write(csvtext);
            }
            catch
            {
                MessageBox.Show("seems that " + csv.Text + "is in use"); return;
            }
            csvfile.Close();
            foreach (var key in csv4plot.Keys) ((StreamWriter)csv4plot[key]).Close();
        }

        string remove_label(string args)
        {
            string str = args; 
            int s = str.IndexOf(labelTag);
            if (s >= 0)
            {
                int l = str.Substring(s).IndexOf(' ');
                if (l == -1) str = args.Remove(s); // when the label is at the end, it is not ending with a space
                else str = args.Remove(s, l);
            }
            return str;
        }
              


        Tuple<int, string, string> run_remote(string cmd, string args) // for unix commands. Synchronous. 
        {
            Process p = new Process();         

            p.StartInfo.FileName = cmd;
            p.StartInfo.Arguments = remove_label(args);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;// false;
            p.StartInfo.CreateNoWindow = true;

            try
            {
                p.Start();
            }
            catch { MessageBox.Show("cannot start process" + p.StartInfo.FileName); throw; }
            string output = p.StandardOutput.ReadToEnd();            
            p.WaitForExit();
            // returns <exist-status, command, output of command>
            return new Tuple<int,string,string>(p.ExitCode, "> " + p.StartInfo.FileName + " " + args, output);
        }

        Process run(string cmd, string args, string outfilename, int affinity = 0x007F)
        {
            Process p = new Process();

            
            p.StartInfo.FileName = cmd;
            p.StartInfo.Arguments = remove_label(args);

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

            var timer = new System.Threading.Timer(kill_process, p, timeout_val, Timeout.Infinite);
            timers.Add(timer); // needed ?
            return p;
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {            
            int cnt = 0;
            Process[] p = new Process[cores + 1];
            
            var fileEntries = new DirectoryInfo(benchmarksDir).GetFiles(searchPattern, checkBox_rec.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (fileEntries.Length == 0) bg.ReportProgress(0, "empty file list\n");
            string remote_user = "", remote_bench_path ="";
            if (checkBox_remote.Checked)
            {
                remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
                remote_bench_path = remote_user + ":" + ConfigurationManager.AppSettings["remote_bench_dir"];
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool ok = false;
            bool copy_to_remote = checkBox_copy.Checked;
            expand_param_list();             
            for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
            {
                if (ext_param_list[par].IndexOf("%f") == -1)
                {
                    listBox1.Items.Add("Warning: param " + ext_param_list[par] + "does not include a %f directive. Skipping");
                    continue;
                }
                bg.ReportProgress(0, "- - - - - " + ext_param_list[par] + "- - - - - ");
                failed = 0;
                results.Clear();
                accum_results.Clear();                                
                foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                {
                    string fileName = fileinfo.FullName;
                    if (checkBox_skip_long_runs.Checked && failed_benchmarks.Contains(fileName))
                    {
                        bg.ReportProgress(0,"Skipping " + fileName + "; it timed-out with a previos configuration.");
                        continue;
                    }
                    string id = getid(ext_param_list[par], fileName);
                    if (entries.Contains(id)) continue;                    
                    ok = false;                    
                    do
                    {
                        string outText = "";
                        long AvailableMem = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
                        if (AvailableMem > MinMem_val)
                            foreach (int i in active)
                            {
                                if (i == 0) break;
                                if (p[i] == null || p[i].HasExited)
                                {
                                    if (checkBox_remote.Checked)
                                    {
                                        string bench = Path.GetFileName(fileName);
                                        if (copy_to_remote)
                                        {                                            
                                            File.Copy(fileName, bench, true); // copying benchmark to work dir.                                            
                                            outText = run_remote("scp", bench + " " + remote_bench_path).Item2;//" ofers@tamnun.technion.ac.il:~/hmuc/test");
                                            bg.ReportProgress(0, outText);
                                            File.Delete(bench);
                                        }
                                        string bench_remote_path = ConfigurationManager.AppSettings["remote_bench_dir"] +  bench;
                                        if (ConfigurationManager.AppSettings["remote_bench_dir"].LastIndexOf("/")!= ConfigurationManager.AppSettings["remote_bench_dir"].Length - 1)
                                        {
                                            MessageBox.Show("remote_bench_dir as defined in .config file has to terminate with a '/'. Aborting.");
                                            return;
                                        }
                                        bg.ReportProgress(0, "running " + fileName + " remotely. ");
                                        cnt++;
                                        bg.ReportProgress(3, cnt.ToString()); // label_cnt.Text                                         
                                        Tuple<int, string, string> outTuple = run_remote(
                                            "ssh ",
                                            remote_user + " \"" + expand_string(ConfigurationManager.AppSettings["remote_ssh_cmd"], bench_remote_path, ext_param_list[par], outfile(bench_remote_path, ext_param_list[par]))
                                            );
                                        bg.ReportProgress(0, outTuple.Item2); // command
                                        bg.ReportProgress(0, outTuple.Item3); // output
                                    }
                                    else                               
                                    {
                                        string outfilename = outfile(fileName, ext_param_list[par]);
                                        if (!checkBox_filter_out.Checked || !File.Exists(outfilename) || (checkBox_rerun_empty_out.Checked && (new FileInfo(outfilename)).Length <= 10))
                                        {
                                            bg.ReportProgress(0, "running " + fileName + " on core " + i.ToString());
                                            cnt++;
                                            bg.ReportProgress(3, cnt.ToString()); // label_cnt.Text 
                                            string local_exe_Text="";
                                            exe.Invoke(new Action(() => {local_exe_Text = exe.Text; })); // since we are not on the form's thread, this is a safe way to get information from there. Without it we may get an exception.
                                            // string local_param_list_text = "";
                                            //param_list[par].Invoke(new Action(() => { local_param_list_text = ext_param_list[par]; })); // since we are not on the form's thread, this is a safe way to get information from there. Without it we may get an exception.
                                            p[i] = run(local_exe_Text, expand_string(ext_param_list[par], fileName), outfilename, 1 << (i - 1));
                                            List<float> l = new List<float>();
                                            processes[p[i]] = new benchmark(ext_param_list[par], fileName, l);
                                        }
                                        else bg.ReportProgress(0, "skipping " + fileName + " due to existing out file.");
                                    }
                                   
                                    ok = true;
                                    break;
                                }
                            }
                        else bg.ReportProgress(0, "not enough memory...");

                        if (!ok)
                        {                 
                            Thread.Sleep(5000);// 5 seconds wait                        
                        }
                    } while (!ok);
                }
                copy_to_remote = false;  // no point in re-copying for the next parameter. 
            }

            // post processing

            bg.ReportProgress(4, ""); // button1.Enabled = true;

            if (checkBox_remote.Checked)
            {
                bg.ReportProgress(0, "Waiting for remote termination... "); //When all processes end, press `import to CSV'"); // because we do not check remotely if all processes ended.
                wait_for_remote_Termination();
                bg.ReportProgress(6, ""); // import_out_to_csv                      
                return;
            }
            
            wait_for_Termination();
            
            bg.ReportProgress(0, "* all processes finished *");
            stopwatch.Stop();

            string time = (Convert.ToSingle(stopwatch.ElapsedMilliseconds) / 1000.0).ToString();
            bg.ReportProgress(0, "# of benchmarks:" + cnt);

            bg.ReportProgress(0, "parallel time = " + time);
            bg.ReportProgress(0, "============================");

            bg.ReportProgress(1, time); //label_paralel_time.Text            
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
                case 3: label_cnt.Text = log; break;
                case 4: button1.Enabled = true; break;
                case 5: label_fails.Text = log; break;
                case 6: /*Debug.Assert(!checkBox_remote.Checked);  */import_out_to_csv(); break;
            }
        }

        #endregion

        #region GUI

        void create_plot_files()
        {
            try
            {
                expand_param_list();            
                for (int par = 0; par < ext_param_list.Count; ++par)
                {
                    string param = normalize_string(ext_param_list[par]);
                    if (param == noOpTag) continue;
                    csv4plot[param] = new StreamWriter(graphDir + param + ".csv");
                    ((StreamWriter)csv4plot[param]).WriteLine("Benchmark,command,usertime,timeout");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot create csv files for cpbm in" + graphDir + "!\n Here is the exception text:\n" + ex.ToString());
                return;
            }
        }

        private void button_start_Click(object sender, EventArgs e)
        {
            try {
            if (File.Exists(csv.Text))
            {
                StreamReader csvfile = new StreamReader(csv.Text);
                csvfile.Close();
            }
            }
            catch
            {
                MessageBox.Show("seems that " + csv.Text + " is in use. Close it and try again.");
                return;
            }


            label_paralel_time.Text = "";            
            label_cnt.Text = "";
            label_fails.Text = "";
            labels.Clear();
            csvtext = "";
            bg = new AbortableBackgroundWorker();
            processes.Clear();
            accum_results.Clear();
            results.Clear();
            
            int j = 0;
            foreach (int indexChecked in checkedListBox_cores.CheckedIndices)
            {
                active[j++] = indexChecked + firstcore;
            }
            try  // in case the field contains non-numeral.
            {
                timeout_val = 1000 * Convert.ToInt32(timeout.Text); // need milliseconds.                 
            }
            catch { timeout_val = Timeout.Infinite; }

            try  // in case the field contains non-numeral.
            {
                MinMem_val = Convert.ToInt32(timeout.Text);                 
            }
            catch { MinMem_val = 0; }

            try
            {
                if (checkBox_filter_csv.Checked && File.Exists(csv.Text)) readEntries();
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
            bg.DoWork += new DoWorkEventHandler(backgroundWorker1_DoWork);
            bg.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker1_ProgressChanged);

            if (!checkBox_remote.Checked)
            {
                if (preserveCores12)
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
                else listBox1.Items.Add("Note: other processes may run on the same cores");
            }
            benchmarksDir = dir.Text; searchPattern = filter_str.Text;
            bg.RunWorkerAsync();           
        }

        private void button_kill_Click(object sender, EventArgs e)
        {
            if (checkBox_remote.Checked)
            {

                // the following command produeces, e.g., qstat -uofers | grep "ofers" | cut -d"." -f1 | xargs qdel, which kills all prcesses by user ofers.
                string remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
                if (MessageBox.Show("Delete all processes of user " + remote_user + "?", "Confirm kill processes", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    string outText = run_remote("ssh", remote_user + " \"qstat -u" + ConfigurationManager.AppSettings["remote_user"] + "| grep \"" +
                      ConfigurationManager.AppSettings["remote_user"] + "\" | cut -d\".\" -f1 | xargs qdel\"").Item2;
                    listBox1.Items.Add(outText);
                }
            }
            else { // local
                int ind1 = exe.Text.LastIndexOf('\\'),  // we cannot use Path.GetFileNameWithoutExtension because the string may contain "
                ind2 = exe.Text.LastIndexOf('.');
                string exe_text = exe.Text.Substring(ind1 + 1, ind2 - ind1 - 1);
                if (MessageBox.Show("Delete all processes called " + exe_text + "?", "Confirm kill processes", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

                Process[] Pr = Process.GetProcessesByName(exe_text);
                foreach (Process p in Pr)
                {
                    if (!p.HasExited) KillProcessAndChildren(p.Id);
                }

                if (preserveCores12) // we changed affinity of other processes, now we retrieve it. 
                {
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
                }
            }
            if (bg != null)
            {
                bg.Abort();
                bg.Dispose();
            }
            if (csvfile != null) csvfile.Close();
            button1.Enabled = true;
        }

        private void button_csv_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.FileName = csv.Text;
            p.StartInfo = startInfo;
            try {
                p.Start();
            }
            catch { MessageBox.Show("The csv file cannot be opened."); }
        }
        #endregion

        /// <summary>
        /// Goes over all parameters (param_list), and creates a new list ext_param_list after expanding the cross-product of expressions such as '{ 1 | 2 | 3 }'.
        /// For example, %f -par1 = {1 | 2} -par2 = {0.1 | 0.2} -par3  will turn into 4 strings in ext_param_list
        /// %f -par1 = 1 -par2 = 0.1 -par3
        /// ...
        /// </summary>
        private void expand_param_list()
        {
            ext_param_list.Clear();
            for (int par = 0; par < param_list_size; ++par)  // for each parameter
            {
                if (param_list[par].Text == noOpTag) continue;
                List<Tuple<int, int>> indices = new List<Tuple<int, int>>(); // pairs of start + end indices of '{' '}' in the string.
                List<string[]> sets = new List<string[]>(); // sets of parameters
                string str = param_list[par].Text; // e.g., -par1 = {1 | 2} -par2 = {0.3 | 0.5} -par3
                int end = 0;
                while (true)
                {
                    int start = str.IndexOf('{', end);
                    if (start == -1) break;
                    end = str.IndexOf('}', start + 1);
                    if (end == -1)
                    {
                        MessageBox.Show("unbalanced {} in parameter " + par);
                        return;
                    }
                    indices.Add(new Tuple<int, int>(start, end));
                    string s = str.Substring(start + 1, end - start - 1); //the contents of the set
                    sets.Add(s.Split(setSeparator)); 
                }
                string res = "";                
                if (sets.Count > 0)
                {
                    var routes = product.CartesianProduct(sets);
                    foreach (var route in routes)  // e.g., route = {1, 0.3} // array of strings
                    {                        
                        res = str.Substring(0, indices[0].Item1); // e.g., res = "-par1 = "
                        int i = 0;
                        foreach (string st in route)
                        {
                            res += st; // e.g., res = "-par1 = 1"
                            if (i < indices.Count - 1) res += str.Substring(indices[i].Item2 + 1, indices[i + 1].Item1 - 1 - indices[i].Item2 - 1); // e.g., res = "-par1 = 1 -par2 = "
                            else res += str.Substring(indices[i].Item2 + 1); // the suffix
                            i++;
                        }
                        ext_param_list.Add(res); // e.g. "-par1 = 1 -par2 = 0.3 -par3"
                    }
                }
                else ext_param_list.Add(str);
            }
        }


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
            if (param_list[param1].Text.IndexOf("{") != -1 || param_list[param2].Text.IndexOf("{") != -1) { MessageBox.Show("Please specify scatter graphs without { } (product) symbols."); return; }
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "run-scatter.bat";
            string f1 = normalize_string(param_list[param1].Text), f2 = normalize_string(param_list[param2].Text);            
            if (f1 == noOpTag || f2 == noOpTag) { MessageBox.Show("Param cannot be " + noOpTag); return; }
            startInfo.Arguments = string.Compare(f1, f2) < 0 ? f1 + " " + f2 : f2 + " " + f1; // apparently make_graph treats them alphabetically, so we need to give them alphabetically to know what pdf is eventually generated. 
            startInfo.WorkingDirectory = graphDir;
            string fullName1 = Path.Combine(graphDir, f1 + ".csv"), fullName2 = Path.Combine(graphDir, f2 + ".csv");
            if (!File.Exists(fullName1) || !File.Exists(fullName2))
            {
                MessageBox.Show("files " + fullName1 + " or " + fullName2 + " cannot be found. Try re-importing the out files to generate them.");
                return;
            }
                p.StartInfo = startInfo;
            p.Start();
        }

        private void button_cactus_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "run-cactus.bat";
            startInfo.Arguments = "";
            expand_param_list();
            for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
            {           
                startInfo.Arguments += " " + normalize_string(ext_param_list[par]) + ".csv";
            }
            startInfo.WorkingDirectory = graphDir;
            startInfo.CreateNoWindow = false;
            p.StartInfo = startInfo;
            p.Start();
        }

        private void checkBox_remote_CheckedChanged(object sender, EventArgs e)
        {
            checkBox_filter_out.Enabled = timeout.Enabled = min_mem.Enabled = exe.Enabled = checkedListBox_cores.Enabled = !(((CheckBox)sender).Checked);
            checkBox_copy.Enabled = (((CheckBox)sender).Checked);
            checkBox_CheckedChanged(sender, e);
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
            if (MessageBox.Show("This operation erases files. continue ? ", "confirm deletion", MessageBoxButtons.YesNo) == DialogResult.No) return;
            Hashtable benchmarks = new Hashtable();
            string fileName = csv.Text;
            HashSet<string> failed_all = new HashSet<string>();
            int cnt = 0;
            // finding failed benchmarks 
            try
            {
                foreach (string line in File.ReadLines(fileName))
                {
                    benchmarks[getfield(line, 3)] = getfield(line, 2);
                }
            }
            catch { MessageBox.Show("seems that " + csv.Text + "is in use"); return; }

            foreach (string line in File.ReadLines(fileName))
            {
                string time = getfield(line, 4);
                double d;
                bool isdouble = double.TryParse(time, out d);
                if (isdouble)
                {
                    if (d <= Convert.ToInt32(timeout.Text) + 1)
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
                try { File.Delete(path);}
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
            string fileName = csv.Text;
            HashSet<string> failed_short_once = new HashSet<string>();
            bool header = true;
            int timeFieldLocation = labels.IndexOf(stat_field.Text);            
            
            try {
                foreach (string line in File.ReadLines(fileName))
                {                    
                    if (header)
                    {
                        List<string> labels1  = new List<string>(line.Split(new char[] {',' }));
                        timeFieldLocation = labels1.IndexOf(stat_field.Text);
                        if (timeFieldLocation == -1)
                        {
                            MessageBox.Show("cannot find field " + stat_field.Text + " in header of " + fileName);
                            return;
                        }
                        timeFieldLocation++; // because indexOf is 0-based
                        header = false;
                        continue;
                    }
                    string longesttime = getfield(line, timeFieldLocation);
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
            }
            catch
            {
                MessageBox.Show("seems that " + csv.Text + "is in use");
                return;
            }

            // keeping only benchmarks that take time. 
            var linesToKeep = File.ReadLines(fileName).Where(l => (!failed_short_once.Contains(getfield(l, 3)) || getfield(l, 1) == "param"));   // second item so it includes the header.

            var tempFile = Path.GetTempFileName();

            File.WriteAllLines(tempFile, linesToKeep);

            File.Delete(fileName);
            File.Move(tempFile, fileName);
        }
        
        private void button_del_fails_Click(object sender, EventArgs e)
        {            
            string fileName = csv.Text;
            HashSet<string> failed_atleast_once = new HashSet<string>();
            bool header = true;
            int cnt = 0;
            // finding failed benchmarks 
            try {
                foreach (string line in File.ReadLines(fileName))
                {
                    cnt++;
                    if (header) { header = false; continue; }
                    string failed = getfield(line, 4);
                    if (failed.Length == 0) continue;
                    Debug.Assert(failed == "1");
                    listBox1.Items.Add("failed line: " + line);
                    failed_atleast_once.Add(Path.Combine(getfield(line, 2), getfield(line, 3)));
                }
            }
            catch { MessageBox.Show("seems that " + csv.Text + "is in use"); return; }
            
            // keeping only benchmarks that are not failed by any parameter combination. 
            var linesToKeep = File.ReadLines(fileName).Where(l => (!failed_atleast_once.Contains(Path.Combine(getfield(l, 2), getfield(l, 3))) || getfield(l, 1) == "param"));   // second item so it includes the header.         
            
            var tempFile = Path.GetTempFileName();

            File.WriteAllLines(tempFile, linesToKeep);

            File.Delete(fileName);
            File.Move(tempFile, fileName);
            string msg = "Kept " + (linesToKeep.Count()) + " lines out of " +  cnt;
            listBox1.Items.Add(msg);

        }

        void import_out_to_csv()
        {           
            int in_csv = 0, file_exists = 0, file_not_exist = 0;
            string benchmarksDir = dir.Text,
            searchPattern = filter_str.Text;            
            listBox1.Items.Add("--- Importing ---");
            var fileEntries = new DirectoryInfo(benchmarksDir).GetFiles(searchPattern, checkBox_rec.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (fileEntries.Length == 0) listBox1.Items.Add("empty file list\n");
            
            processes.Clear();
            if (checkBox_filter_csv.Checked && File.Exists(csv.Text)) readEntries();
            else entries.Clear();
            if (checkBox_remote.Checked) listBox1.Items.Add("Files will be imported to " + Directory.GetCurrentDirectory());
            bool first = true;
            string remote_user = "", remote_bench_path = "";
            if (checkBox_remote.Checked)
            {
                remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
                remote_bench_path = remote_user + ":" + ConfigurationManager.AppSettings["remote_bench_dir"];
            }

            expand_param_list();
            for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
            {                
                foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                {
                    string fileName = fileinfo.FullName;                    
                    string id = getid(ext_param_list[par], fileName);
                    if (entries.Contains(id)) { in_csv++; continue; }                    
                    string outfileName = "";
                    if (checkBox_remote.Checked)
                    {
                        outfileName = outfile(Path.GetFileName(fileName), ext_param_list[par]); // we import from the working directory (bench/bin/release/ or debug/)
                        if (!File.Exists(outfileName))
                        {
                            string outText = run_remote("scp", remote_bench_path + outfileName + " " + outfileName).Item2; // download the file
                            listBox1.Items.Add(outText);
                        }
                    }
                    else
                    {
                        outfileName = outfile(fileName, ext_param_list[par]); // we import from the same directory as the source cnf file
                    }                    
                    if (File.Exists(outfileName))
                    {
                        Process p = new Process(); // we are only using this process as a carrier of the information from the file, so we can use the buildcsv function. 
                        List<float> l = new List<float>();
                        processes[p] = new benchmark(ext_param_list[par], fileName, l);
                        bool res = read_out_file(p, outfileName, first);
                        //try   // uncomment to delete benchmark files that are SAT
                        //{
                        //    if (!read_out_file(p, outfileName, first))
                        //    {
                        //        listBox1.Items.Add(fileName + " is SAT. Deleting.");
                        //        File.Delete(fileName);
                        //        processes.Remove(p);
                        //    }
                        //}
                        //catch { return; } // we get here if there is inconsistencies in the labels
                        file_exists++;
                        if (first && res) first = false;  // we want to keep it 'first' as long as we did not read labels. 
                    }
                    else
                    {
                        listBox1.Items.Add(outfileName + " is missing");
                        file_not_exist++;
                    }
                }
            }

            bool Addheader = !checkBox_filter_csv.Checked || !File.Exists(csv.Text);
            try
            {
                init_csv_file();
            }
            catch (Exception ex)
            {                
                return;
            }
            listBox1.Items.Add(in_csv.ToString() + " benchmarks already in the csv file.");
            listBox1.Items.Add(file_exists.ToString() + " benchmark results imported from out files.");
            listBox1.Items.Add(file_not_exist.ToString() + " outfile missing.");
            
            buildcsv(Addheader);
        }

        private void button_import_Click(object sender, EventArgs e)  // import out files from remote server, and process them to generate the csv + plot files. 
        {
            import_out_to_csv();
        }

        private void button_del_allfail_Click(object sender, EventArgs e) // delete benchmarks that no combination of parameters solved.
        {
            del_Allfail_benchmark();
        }

        private void button_del_shorts_click(object sender, EventArgs e)
        {
            del_short_calls();
        }

        private void checkBox_out_CheckedChanged(object sender, EventArgs e)
        {
            checkBox_rerun_empty_out.Enabled = checkBox_filter_out.Checked;
            checkBox_CheckedChanged(sender, e);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();            
            dialog.SelectedPath = @dir.Text;            
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                dir.Text = dialog.SelectedPath;
                ActiveControl = dir;
            }
        }

        private void comboBox_Leave(object sender, EventArgs e)
        {
            string text = ((ComboBox)sender).Text;
            fields fieldValue = (fields)Enum.Parse(typeof(fields), ((ComboBox)sender).Name);
            if (!history.ContainsKey(fieldValue)) history[fieldValue] = new List<string>();
            if (!history[fieldValue].Contains(text))
            {
                history[fieldValue].Insert(0,text);                
                ((ComboBox)sender).DataSource = history[fieldValue];
                write_history_file = true;                
            }
        }

        private void combo_SelectedIndexChanged(object sender, EventArgs e)
        {
            string element = ((ComboBox)sender).SelectedItem.ToString();
            fields fieldValue = (fields)Enum.Parse(typeof(fields), ((ComboBox)sender).Name);
            history[fieldValue].Remove(element);
            history[fieldValue].Insert(0, element);
            write_history_file = true;
        }

        private void param_groups_SelectedIndexChanged(object sender, EventArgs e)
        {
            string[] param = param_groups.Items[((ComboBox)sender).SelectedIndex].ToString().Split(',');
            int i = 0;
            foreach (string st in param)
            {
                param_list[i].Text = st;
                ++i;
                if (i >= param_list_size) break;
            }
            for (;i < param_list_size; ++i)
            {
                param_list[i].Text = noOpTag;
            }
        }

        private void editHistoryFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = "notepad";
            p.StartInfo.Arguments = history_file;
            p.Start();
        }

        private void refreshMenusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            read_history(history_file);
        }

        private void checkBox_CheckedChanged(object sender, EventArgs e)
        {            
            fields fieldValue = (fields)Enum.Parse(typeof(fields), ((CheckBox)sender).Name);
            string checked_yesno = ((CheckBox)sender).Checked ? "yes" : "no";
            if (!history.ContainsKey(fieldValue))
            {
                history[fieldValue] = new List<string>();
                history[fieldValue].Insert(0, checked_yesno);
            }
            else history[fieldValue][0] = checked_yesno;
            write_history_file = true;
        }

        private void configToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = "notepad";
            p.StartInfo.Arguments = "bench.exe.config";
            p.Start();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // here we update the history file if needed. 
            
            // params. Computing current param_group according to the text in param_list
            string param_set = "";
            bool first = true;
            for (int i = 0; i < param_list_size; ++i)
            {
                if (param_list[i].Text != noOpTag)
                {
                    if (!first) param_set += ",";                    
                    param_set += param_list[i].Text;
                    first = false;
                }
            }
            if (!history[fields.param_groups].Contains(param_set))
            {
                history[fields.param_groups].Insert(0, param_set);
                write_history_file = true;
            }

            // cores
            string active_cores_str = "";
            first = true;
            foreach (int indexChecked in checkedListBox_cores.CheckedIndices)
            {
                if (!first) active_cores_str += ",";
                active_cores_str += (indexChecked + firstcore).ToString();
                first = false;
            }
            if (!history.Keys.Contains(fields.core_list) || history[fields.core_list].Count == 0)
            {
                history[fields.core_list] = new List<string>();
                history[fields.core_list].Add(active_cores_str);
                write_history_file = true;
            }
            else
                if (active_cores_str != history[fields.core_list][0])
            {
                history[fields.core_list][0] = active_cores_str;
                write_history_file = true;
            }
            
            if (write_history_file) write_history();
        }
    }

    public class benchmark
    {
        public string param;
        public string name;
        public List<float> res;

        public benchmark(string param, string name, List<float> res)
        {
            this.param = param;
            this.name = name;
            this.res = res;
        }
    }

    public static class product
    {

        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            var accum = new List<T[]>();
            var list = sequences.ToList();
            if (list.Count > 0)
                CartesianRecurse(accum, new Stack<T>(), list, list.Count - 1);
            return accum;
        }

        static void CartesianRecurse<T>(List<T[]> accum, Stack<T> stack,
                                        List<IEnumerable<T>> list, int index)
        {
            foreach (T item in list[index])
            {
                stack.Push(item);
                if (index == 0)
                    accum.Add(stack.ToArray());
                else
                    CartesianRecurse(accum, stack, list, index - 1);
                stack.Pop();
            }
        }

        public static void printAllTest()
        {
            List<string[]> L = new List<string[]> { new[] { "a", "b" }, new[] { "c", "d" }, new[] { "e", "f" } };

            var routes = CartesianProduct<string>(L);
            foreach (var route in routes)
            {
                Console.WriteLine(string.Join(", ", route));
                Console.WriteLine(route.ElementAt(1));
            }
        }
    }


}

