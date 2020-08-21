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
using System.Text.RegularExpressions;
using System.Globalization;

namespace bench
{    
    public partial class filter : Form
    {
        // reading from config  file: 
        string history_file = Path.Combine(Application.StartupPath, ConfigurationManager.AppSettings["history_filename"]);//"history.txt"
        string graphDir =  ConfigurationManager.AppSettings["cpbm"]; //@"c:\temp\cpbm-0.5\";        
        StreamWriter logfile = new StreamWriter(ConfigurationManager.AppSettings["log"]); // @"C:\temp\log.txt");        
        string stat_tag = ConfigurationManager.AppSettings["stat_tag"]; // ###
        string abort_tag = ConfigurationManager.AppSettings["abort_tag"];
        const string timedout_Tag = "timedout";
        bool hyperthreading = ConfigurationManager.AppSettings["hyperthreading"] == "true";
        static int param_list_size = int.Parse(ConfigurationManager.AppSettings["param_list_size"]);

        // more configurations:   
        int timeout_val = Timeout.Infinite; // will be read from history file
        int MinMem_val = 0;  // in MB. Will be read from history file        
        bool preserveFirstCores = ConfigurationManager.AppSettings["PreserveFirstCores"] == "true"; 
        int firstcore;  
        int cores = Environment.ProcessorCount;
        int[] active = new int[8]; // {3, 5, 7 }; 
        int failed = 0;
        const string labelTag = "#";
        const string noOpTag = "<>";
        const char setSeparator = '|';

        enum fields {
            exe, dir, filter_str, maxfiles, csv, param, param_groups, stat_field, core_list, timeout, min_mem,  // combos
            checkBox_skip_long_runs, checkBox_remote, checkBox_rec, checkBox_rerun_empty_out, checkBox_filter_out, checkBox_filter_csv, checkBox_copy, // checkboxes
            misc }; // elements maintained in the history file
        enum header_fields { exedate, param, dir, bench, fail, timedout };
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
        HashSet<string> BenchmarkNamesFromCsv = new HashSet<string>();        
        Dictionary<fields, List<string>> history;        
        bool write_history_file = false;
        string benchmarksDir, searchPattern;
        private const string id_prefix = "P: ";


        private struct Forplot // used for storing information about benchmarks when preparing the plot files. 
        {
            string bench;
            string param;
            string val;
            public Forplot(string b, string p, string v)
            {
                bench = b;
                param = p;
                val = v;
            }

            public string Bench { get => bench; set => bench = value; }
            public string Param { get => param; set => param = value; }
            public string Val { get => val; set => val = value; }
        }


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

            firstcore = preserveFirstCores ? (hyperthreading ? 3 : 2) : 1;
            
            for (int i = firstcore; i <= cores; ++i)  // cores 1,2 are preserved for other processes. 
                checkedListBox_cores.Items.Add("c" + i.ToString());

            ToolTip scatter_tt = new ToolTip();

            for (int i = 0; i < param_list_size; ++i)
            {
                param_list[i] = new TextBox();                
                param_list[i].Location = new Point(60,  i * 25);
                param_list[i].Size = new Size(640, 20);     
                param_list[i].Leave += new System.EventHandler(this.textBox_Leave);
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
            searchPattern = filter_str.Text;
            benchmarksDir = dir.Text;
            readLabelsFromCsv();
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
            Stack<Control> controls = new Stack<Control>();
            foreach (Control C in Controls) controls.Push(C);

            while (controls.Count > 0)
            {
                Control C = controls.Pop();
                Type type = C.GetType();

                if (type == typeof(GroupBox))  
                {
                    foreach (Control cc in C.Controls) controls.Push(cc);
                    continue; 
                }
                if (type == typeof(ComboBox))
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
                else if (type == typeof(CheckBox))
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

        // called from background-worker thread
        string normalize_string(string s)
        {
            return s.Replace("=", "").Replace(" ", "").Replace("_", "").Replace(labelTag, "").Replace("%f", "").Replace("-",""); // to make param a legal file name. Might have a problem with '-' because some parameters use negative values. We cannot use in the replacement string a "-" because having this in the file name makes scatter/cactus refer to this as a parameter.
        }


        // called from background-worker thread
        string expand_string(string s, string filename, string param="", string outfilename="")  // the last two are used for remote execution
        {            
            string res = s.Replace("%f", "\"" + filename + "\"").Replace("%p", param).Replace("%o", outfilename);
            if (res == s) return res;
            else return expand_string(res, filename, param, outfilename);  // recursive because the replacing strings may contain %directives themselves.
        }


        string strip_id_prefix(string param)
        {
            System.Diagnostics.Debug.Assert(param.Substring(0, 3) == id_prefix);
            return param.Substring(3);
        }

        // called from background-worker thread
        string getid(string param, string filename, string prefix = id_prefix)
        {
            return getid(param, Path.GetDirectoryName(filename), Path.GetFileName(filename), prefix);                
        }

        string getid(string param, string dir, string filename, string prefix)
        {
            return prefix + param + "," +
                dir + "," +  // benchmark
                filename;
        }
        void readLabelsFromCsv()
        {
            string header, nextline;
            List<string> vals;
            StreamReader csvfile;
            try
            {
                csvfile = new StreamReader(csv.Text);      //(@"C:\temp\res.csv");
                header = csvfile.ReadLine(); // header
                nextline = csvfile.ReadLine();
            }
            catch (Exception)
            {
                MessageBox.Show("cannot read labels from " + csv.Text);                
                return;
            }
            labels = header.Split(',').ToList<string>();
            bool validfirstline = nextline != null;
            if (validfirstline) vals = nextline.Split(',').ToList<string>();
            else vals = labels; // just to get the same count; won't be used. 
            stat_field.DataSource = null;
            stat_field.Items.Clear();
            decimal res;
            // only include labels that the entry in the next line is either a number or empty.
            // We use decimal because it permits e.g. 1.3E7
            for (int i = 0; i < labels.Count() && i < vals.Count(); ++i) 
                if (!validfirstline || decimal.TryParse(vals[i], NumberStyles.Any, CultureInfo.InvariantCulture, out res) || vals[i] == "") 
                    stat_field.Items.Add(labels[i]);
            csvfile.Close(); 
        }


        void readBenchmarkNamesFromCsv()
        {       
            string line, res;
            StreamReader csvfile;
            try {
                csvfile = new StreamReader(csv.Text);      //(@"C:\temp\res.csv");
                csvfile.ReadLine(); // header
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n seems that " + csv.Text + " is in use");
                throw;
            }
                        
            while ((line = csvfile.ReadLine()) != null)
            {  
                res = getid(getfield(line, header_fields.param), getfield(line, header_fields.dir), getfield(line, header_fields.bench),"");
                BenchmarkNamesFromCsv.Add(res);            
            }
            csvfile.Close();
        }

        string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private void build_process_tree(int pid, ref List<int> kill_list)
        {
            kill_list.Add(pid);
            Process proc;
            try { proc = Process.GetProcessById(pid); }
            catch { return; } // by now it has exited. 
            bg.ReportProgress(0, "added process id = " + proc.Id + " (" + proc.ProcessName + ")");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
               ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                build_process_tree(Convert.ToInt32(mo["ProcessID"]), ref kill_list);
            }
        }

        private void KillProcessAndChildren(int pid)
        {
            List<int> kill_list = new List<int>();
            build_process_tree(pid, ref kill_list);
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

        // called from background-worker thread
        void kill_process(Object stateinfo)
        {
            Process p = (Process)stateinfo;         
            if (!p.HasExited)
            {
                bg.ReportProgress(0, "timeout: process killed: " + p.StartInfo.Arguments);                
                benchmark data = (benchmark)processes[p];                
                failed_benchmarks.Add(data.name);
                failed++;
                bg.ReportProgress(5, failed.ToString());                
                data.timedout = true;
                KillProcessAndChildren(p.Id);
            }            
        }

        // called from background-worker thread
        string outfile(string filename, string param)
        {
            return filename + "." + normalize_string(param) + ".out";
        }

        void scrolldown()
        {
            int visibleItems = listBox1.ClientSize.Height / listBox1.ItemHeight;
            listBox1.TopIndex = Math.Max(listBox1.Items.Count - visibleItems + 1, 0);
        }

        // called from background-worker thread
        void Log(string msg, bool tofile = true)
        {
            listBox1.Items.Add(msg); 
            listBox1.Refresh();
            scrolldown();
            if (tofile)
            {
                logfile.WriteLine(msg);
            }
        }

        // called from background-worker thread
        bool filterOut(string outfilename) {
            return checkBox_filter_out.Checked && File.Exists(outfilename) &&
                   (!checkBox_rerun_empty_out.Checked || (new FileInfo(outfilename)).Length > 10);
            }

        /// <summary>
        /// We cannot just use normal GetFiles because it has various unexpected behaviors, e.g., *.txt also includes *.txta (or anything longer than "txt")
        /// so here we filter it out. 
        /// </summary>
        /// <returns></returns>
        List<FileInfo> getFilesInDir()
        {
            List<FileInfo> res = new List<FileInfo>();
            FileInfo[] filelist = null;
            string extension = searchPattern.Substring(searchPattern.LastIndexOf('.'));
            try
            {
                 filelist = new DirectoryInfo(benchmarksDir).GetFiles(searchPattern, checkBox_rec.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch
            {
                MessageBox.Show("cannot open directory " + benchmarksDir + ". Aborting.");
                Application.Exit();
            }
            if (extension == "") return filelist.ToList();
            int counter = int.MaxValue;
            string text="";
            maxfiles.Invoke(new Action(() => { text = maxfiles.Text; }));
            if (!int.TryParse(text, out counter))
            {
                bg.ReportProgress(0, "Non-numeric value in max-files. Putting no limits on # of files.");
                counter = int.MaxValue;
            };
            if (counter == 0) counter = int.MaxValue;
            foreach (FileInfo fi in filelist)
            {
                if (string.Compare(extension, fi.Extension, StringComparison.OrdinalIgnoreCase) != 0) continue;
                counter--;
                if (counter < 0) break;
                res.Add(fi);
            }
            return res;
        }

        #endregion

        #region work              
        /// <summary>
        /// Reads data from filename, and updates the process p.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="filename"></param>
        /// <param name="first"></param>
        /// <returns></returns>
        bool read_out_file(Process p, string filename, bool first)
        {           
            
            bool success = false;
            StreamReader file = null;
            for (int i = 0; i < 3; ++i)
            {
                try
                {
                    file = new StreamReader(filename);
                    break;
                }
                catch
                {
                    Thread.Sleep(3000);
                }
            }
            string line;

            while ((line = file.ReadLine()) != null)            
            {                
                if (line.Length >= 4 && line.Substring(0, 3) == stat_tag)
                {
                    var parts = line.Split(new char[] { ' ','\t' }, StringSplitOptions.RemoveEmptyEntries); // The RemoveEmptyEntries takes care of multiple spaces. 
                    string tag = parts[1];            

                    if (tag == abort_tag || tag == "SAT")  
                    {
                        listBox1.Items.Add("* * * * * * * * * * * * *  Abort!");
                        file.Close();                        
                        return true;
                    }

                    float res;

                    benchmark data = (benchmark)processes[p];
                    if (tag == timedout_Tag) data.timedout = true;
                    else
                    {
                        if (float.TryParse(parts[2], out res))
                        {
                            if (first)
                            {
                                Debug.Assert(!labels.Exists(x => x == tag));
                                labels.Add(tag);
                                success = true;
                            }
                            else
                            {
                                if (!labels.Exists(x => x == tag))
                                {
                                    listBox1.Items.Add("Warning: label " + tag + " in file " + filename + " did not appear in the first file. This will lead to non alighed data in the csv file. ");
                                    //   throw(new Exception("incompatible labels"));                                
                                    labels.Add(tag);
                                    //  return true;
                                }
                            }

                            data.res.Add(res);

                        }
                        else listBox1.Items.Add("skipping non-numerical data: " + parts[2]);
                    }
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

        /// <summary>
        ///  same as read_out_file, but updates del=true if the file should be erased because
        ///  it is SAT, too easy (less than 30 sec.) or too hard (timed out).
        /// </summary>
        /// <param name="p"></param>
        /// <param name="filename"></param>
        /// <param name="first"></param>
        /// <param name="del"></param>
        /// <returns></returns>
        bool read_out_file_del(Process p, string filename, bool first, out bool del)
        {
            if ((new FileInfo(filename)).Length <= 10)
            {
                listBox1.Items.Add("removing " + filename + ". Could not be solved.");
                del = true;
                return false; // !! delete files that cannot be solved within the timeout. 
            }

            bool success = false;
            StreamReader file = new StreamReader(filename);
            string line;

            while ((line = file.ReadLine()) != null)
            {
                if (line.Length >= 4 && line.Substring(0, 3) == stat_tag)
                {
                    var parts = line.Split(new char[] { ' ' });
                    string tag = parts[1];

                    // RemoveSAT
                    if (tag == "SAT") // uncomment if we want to erase benchmarks that are SAT.
                    {
                        listBox1.Items.Add("* * * * * * * * * * * * *  SAT!");
                        file.Close();
                        del = true;
                        return false;
                    }

                    if (tag == abort_tag || tag == "SAT")
                    {
                        listBox1.Items.Add("* * * * * * * * * * * * *  Abort!");
                        file.Close();
                        del = false;
                        return true;
                    }


                    float res;
                    if (float.TryParse(parts[2], out res))
                    {
                        if (first)
                        {
                            Debug.Assert(!labels.Exists(x => x == tag));
                            labels.Add(tag);
                            success = true;
                        }
                        else
                        {
                            if (!labels.Exists(x => x == tag))
                            {
                                listBox1.Items.Add("label " + tag + " in file " + filename + " did not appear in the first file. Aborting");
                                throw (new Exception("incompatible labels"));
                                //  return true;
                            }
                        }

                        if (tag == "time" && res < 30.0)
                        {
                            listBox1.Items.Add("removing " + filename + ". Too easy.");
                            del = true;
                            return false; // !! remove easy instances
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
            del = false;
            return success;
        }

        // called from background-worker thread
        void wait_for_remote_Termination()
        {
            string remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
            int res;  
            
            while (true)
            {
                res = run_remote("ssh ", remote_user + " \"qstat -u " + ConfigurationManager.AppSettings["remote_user"] + "| grep \"" + ConfigurationManager.AppSettings["remote_user"] + "\"").Item1;
                if (res != 0) break;                
                Thread.Sleep(10000); // 10 seconds wait                        
            }
            bg.ReportProgress(0, "* All remote processes terminated *");
        }

        // called from background-worker thread
        void wait_for_Termination()
        {
            foreach (DictionaryEntry entry in processes)
            {
                Process p1 = (Process)entry.Key;
                if (!p1.HasExited)  p1.WaitForExit();
            }
        }

        void prepareDataForCsv()
        {
            int in_csv = 0;

            filter_str.Invoke(new Action(() => { searchPattern = filter_str.Text; }));
            dir.Invoke(new Action(() => { benchmarksDir = dir.Text; }));
            var fileEntries = getFilesInDir();                
            if (fileEntries.Count == 0) listBox1.Items.Add("empty file list\n");

            BenchmarkNamesFromCsv.Clear();
            if (checkBox_filter_csv.Checked && File.Exists(csv.Text))
                readBenchmarkNamesFromCsv();            

            bool first = true;

            expand_param_list();
            for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
            {
                foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                {
                    string fileName = fileinfo.FullName;
                    string id = getid(ext_param_list[par], fileName);
                    if (BenchmarkNamesFromCsv.Contains(id)) { in_csv++; continue; }
                    string outfileName = outfile(fileName, ext_param_list[par]); // we import from the same directory as the source cnf file;                    

                    if (File.Exists(outfileName))
                    {
                        bool exists = false;
                        Process p1 = null;
                        // Adding a 'timedout' label to output files of processes that timed-out. 
                        foreach (Process p in processes.Keys)
                        {
                            benchmark bench = processes[p] as benchmark;
                            if (bench.param != ext_param_list[par] || bench.name != fileName) continue;
                            exists = true;
                            p1 = p;
                            if (bench.timedout)
                            {
                                using (StreamWriter sw = new StreamWriter(outfileName, true))
                                {
                                    sw.WriteLine(stat_tag + " " + timedout_Tag + " 1");
                                }
                                bench.res.Clear();
                            }
                            break;
                        }

                        if (!exists) // this happens only when there is already an .out file, hence a new process is not added to processes
                        {
                            p1 = new Process(); // we are only using this process as a carrier of the information from the file, so we can use the buildcsv function. 
                            List<float> l = new List<float>();
                            processes[p1] = new benchmark(ext_param_list[par], fileName, l);
                        }

                        bool res = read_out_file(p1, outfileName, first);

                        // uncomment the following to delete benchmark files that are SAT/too easy/too hard (see read_out_file_del)
                        //bool del; // whether to delete the benchmark itself
                        //bool res = read_out_file_del(p, outfileName, first, out del);
                        //try  
                        //{
                        //    if (del) // result is SAT
                        //    {
                        //        //listBox1.Items.Add(fileName + " is SAT. Deleting.");
                        //        File.Delete(fileName);
                        //        processes.Remove(p);
                        //    }
                        //}
                        //catch { return; } // we get here if there is inconsistencies in the labels
                        if (first && res) first = false;  // we want to keep it 'first' as long as we did not read labels. 
                    }
                    else
                    {
                        listBox1.Items.Add(outfileName + " is missing");
                    }
                }
            }
        }

        void buildcsv()
        {
            bool Addheader = /*!checkBox_filter_csv.Checked ||*/ chk_resetcsv.Checked || !File.Exists(csv.Text);
            string csvheader = "", csvtext = "";
            int cnt_wrong_label = 0;
            string exedate = "";
            if (!checkBox_remote.Checked) exedate = File.GetLastWriteTime(exe.Text).ToString();

            prepareDataForCsv(); // this fills 'processes'.
            try
            {
                csvfile = new StreamWriter(csv.Text, checkBox_filter_csv.Checked || !chk_resetcsv.Checked);
            }
            catch
            {
                MessageBox.Show("Cannot open " + csv.Text);
                throw;
            }
            foreach (DictionaryEntry entry in processes)
            {                
                benchmark bm = entry.Value as benchmark;
                Process p1 = (Process)entry.Key;
                
                List<float> l = bm.res;
                csvtext += exedate + ",";                
                csvtext += getid(bm.param, bm.name) + ","; // benchmark
                if (l.Count > 0)
                {
                    csvtext += ",,"; // for the 'fail/timedout' column
                    for (int i = 0; i < l.Count; ++i)
                        csvtext += l[i].ToString() + ",";
                }
                else   // in case of timeout / other fails (mem-out / whatever)
                {
                    if (bm.timedout) csvtext += ",1,";
                    else csvtext += "1,,";
                    for (int j = 0; j < labels.Count; ++j) csvtext += ","; // creating empty columns, because we later may add a last column 'failed with some param' so all rows must be aligned. 
                 }
                //csvtext = csvtext.Substring(0, csvtext.Length - 1); // remove last ','
                csvtext += "\n";
            }
            if (cnt_wrong_label > 0) listBox1.Items.Add("Warning: \"" + stat_field.Text + "\" (the name of the statistics field specified below) is not a column in " + cnt_wrong_label + " out files. Will not add data to statistics.");

            stat_field.DataSource = null;
            stat_field.Items.Clear();
            foreach (string lbl in labels) stat_field.Items.Add(lbl);
            try
            {
                if (Addheader)
                {
                    foreach (string lbl in labels) csvheader += lbl + ",";
                    csvheader = String.Join(",", Enum.GetNames(typeof(header_fields))) + "," + csvheader;
                    csvheader = csvheader.Substring(0, csvheader.Length - 1); // remove last ','
                    csvfile.Write(csvheader);
                    csvfile.WriteLine();
                }
                csvfile.Write(csvtext);
            }
            catch
            {
                MessageBox.Show("seems that " + csv.Text + " is in use");
                throw;
            }
            csvfile.Close();            
            if (ConfigurationManager.AppSettings["add_fails_column"] == "true") button_mark_fails_Click(null, EventArgs.Empty);
            listBox1.Items.Add("updated csv file");
        }


        bool prepare_plot_data_fromCSV()
        {
            string line;
            StreamReader csvfile;
            List<Forplot> forplot = new List<Forplot>();  // saves information that is later used for generating the csv files for the plots. 
            Forplot fp;
            float maxval = 0;

            if (stat_field.Text == "")
            {
                MessageBox.Show("Please select a statistics field");
                return false;
            }

            if (IsFileLocked(new FileInfo(csv.Text)))
            {
                MessageBox.Show(csv.Text + " is in use\n");
                return false;
            }
            
            init_plot_files();

            csvfile = new StreamReader(csv.Text);      //(@"C:\temp\res.csv");
            string header = csvfile.ReadLine(); // header
            string[] cols = header.Split(',');

            int stat_field_col = Array.IndexOf(cols, stat_field.Text);
            if (stat_field_col < 0)
            {
                MessageBox.Show(stat_field.Text + " is not in the header of " + csv.Text);
                foreach (var key in csv4plot.Keys) ((StreamWriter)csv4plot[key]).Close();
                csvfile.Close();
                return false;

            }
            Regex rgx = new Regex(filter_str.Text.Replace(".", @"\.").Replace("*", @".*")); 
                

            while ((line = csvfile.ReadLine()) != null)
            {
                float val;
                if (!rgx.IsMatch(line)) continue;                
                cols = line.Split(',');
                if (cols.Length - 1 < stat_field_col) continue;
                string param = strip_id_prefix(getfield(line, header_fields.param));
                string key = normalize_string(param);
                if (!csv4plot.Contains(key)) continue; // This can happen if the csv file contains entries different than what appear in the GUI list. 
                if (cols[stat_field_col] == "") continue; // timeout cases
                fp = new Forplot(
                    Path.Combine(getfield(line, header_fields.dir), getfield(line, header_fields.bench)), 
                    param, 
                    cols[stat_field_col] 
                    );
                forplot.Add(fp);
                if (float.TryParse(cols[stat_field_col], out val) && val > maxval) maxval = val;
            }
            if (forplot.Count == 0)
            {
                MessageBox.Show("no line in the csv file matches the regular expression " + filter_str.Text);
                foreach (var key in csv4plot.Keys) ((StreamWriter)csv4plot[key]).Close();
                csvfile.Close();
                return false;
            }
            maxval++; // we add one because if there is one dot (or all the dots have the same vlaue, it creates a problem in latex' pgfplot). 
            HashSet<string> keys = new HashSet<string>();
            foreach (Forplot forp in forplot)
            {
                if (forp.Val == "") continue;
                string key = normalize_string(forp.Param);
                keys.Add(key);
                Debug.Assert(csv4plot.Contains(key)); 
                ((StreamWriter)csv4plot[key]).WriteLine(
                forp.Bench + "," + // full benchmark path
                key + "," + // param
                forp.Val 
                + "," +
                maxval + "s");
            }


            // copying keys into a temp list. We cannot iterate directly on keys and remove one of the items. 
            List<string> temp = new List<string>();
            foreach (var k in csv4plot.Keys)
            {
                temp.Add(k.ToString());
            }            
            
            foreach (string key in temp)
            {
                if (!keys.Contains(key))
                {
                    listBox1.Items.Add("Warning: key " + key + " has no entries in the csv file. Skipping.");
                    ((StreamWriter)csv4plot[key]).Close();
                    csv4plot.Remove(key);
                    continue;
                }
            }

            foreach (var key in csv4plot.Keys)
            {    
                ((StreamWriter)csv4plot[key]).Close();
            }
             csvfile.Close();
            return true;
        }

     
        // called from background-worker thread
        string remove_label(string args)
        {
            string str = args;             
            bool ok = false;
            while (!ok)
            {
                ok = true;
                int s = str.IndexOf(labelTag);
                if (s >= 0)
                {
                    ok = false;
                    int l = str.Substring(s).IndexOf(' ');
                    if (l == -1) str = str.Remove(s); // when the label is at the end, it is not ending with a space
                    else str = str.Remove(s, l);
                }
            }
            return str;
        }

        // called from background-worker thread
        Tuple<int, string, string> run_remote(string cmd, string args) // for unix commands. Synchronous. 
        {
            string local_dir_Text="";
            Process p = new Process();
                                 
            p.StartInfo.FileName = cmd;
            p.StartInfo.Arguments = remove_label(args);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;// false;
            p.StartInfo.CreateNoWindow = true;
            dir.Invoke(new Action(() => { local_dir_Text = dir.Text; }));
            p.StartInfo.WorkingDirectory = local_dir_Text;    // when executing a scp command, this will bring the files to the benchmarks dir. 
             

            try
            {
                p.Start();
            }
            catch { MessageBox.Show("cannot start process" + p.StartInfo.FileName); throw; }
            string output = p.StandardOutput.ReadToEnd();            
            p.WaitForExit();
            // returns <exist-status, command, output of command>
            return new Tuple<int,string,string>(p.ExitCode, "> " + p.StartInfo.FileName + " " + p.StartInfo.Arguments, output);
        }

        // called from background-worker thread
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
            List<FileInfo> fileEntries = getFilesInDir();
            if (fileEntries.Count == 0) bg.ReportProgress(0, "empty file list\n");
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
                    listBox1.Items.Add("Warning: param " + ext_param_list[par] + " does not include a %f directive. Skipping");
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
                        bg.ReportProgress(0,"Skipping " + fileName + "; it timed-out with a previous configuration.");
                        continue;
                    }

                    string outfilename = outfile(fileName, ext_param_list[par]);                         
                    if (filterOut(outfilename)) {
                        bg.ReportProgress(0, "Skipping " + fileName + " due to existing out file.");
                        continue;
                    }                    

                    string id = getid(ext_param_list[par], fileName);
                    if (BenchmarkNamesFromCsv.Contains(id)) continue;                    
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
                                        if (ConfigurationManager.AppSettings["remote_bench_dir"].LastIndexOf("/") != ConfigurationManager.AppSettings["remote_bench_dir"].Length - 1)
                                        {
                                            MessageBox.Show("remote_bench_dir as defined in .config file has to terminate with a '/'. Aborting.");
                                            e.Cancel = true;
                                            return;
                                        }
                                        bg.ReportProgress(0, "Remote path (defined in App.config): " + ConfigurationManager.AppSettings["remote_bench_dir"]);
                                        string relativepath = GetRelativePath(fileName, benchmarksDir).Replace("\\", "/"); 
                                        string bench = Path.GetFileName(fileName);
                                        if (copy_to_remote)
                                        {                                         
                                            string target = remote_bench_path + relativepath;
                                            Tuple<int, string, string> res = run_remote("scp ", relativepath + " " + target);
                                            if (res.Item1 != 0)
                                            {
                                                bg.ReportProgress(0, "*** Failed copying to remote dir " + target + ".");
                                                bg.ReportProgress(0, "*** Check if the existing remote dir structure is identical to the source dir structure and that ");
                                                bg.ReportProgress(0, "*** destination dirs have write permissions. Aborting.");
                                                e.Cancel = true;  // will be referred to in backgroundWorker1_Completed
                                                return;
                                            }                                            
                                            outText = res.Item2;//" ofers@tamnun.technion.ac.il:~/hmuc/test");
                                            bg.ReportProgress(0, outText);
                                            res = run_remote("ssh ", remote_user + " \"chmod 644 " + ConfigurationManager.AppSettings["remote_bench_dir"] + relativepath + "\"");
                                            if (res.Item1 != 0)
                                            {
                                                bg.ReportProgress(0, "*** Failed to change mode. Aborting");
                                                e.Cancel = true;
                                                return;
                                            }
                                            //File.Delete(bench);
                                        }

                                        string bench_remote_path = ConfigurationManager.AppSettings["remote_bench_dir"] + relativepath;                                      
                                        bg.ReportProgress(0, "running " + fileName + " remotely. ");
                                        cnt++;
                                        bg.ReportProgress(3, cnt.ToString()); // label_cnt.Text                                         
                                        Tuple<int, string, string> outTuple = run_remote(
                                            "ssh ",
                                            remote_user + " \"" + expand_string(ConfigurationManager.AppSettings["remote_ssh_cmd"], bench_remote_path, remove_label(ext_param_list[par]), outfile(bench_remote_path, ext_param_list[par]))
                                            );
                                        bg.ReportProgress(0, outTuple.Item2); // command
                                        bg.ReportProgress(0, outTuple.Item3); // output
                                    }
                                    else                               
                                    {
                                        bg.ReportProgress(0, "running " + fileName + " on core " + i.ToString());
                                        cnt++;
                                        bg.ReportProgress(3, cnt.ToString()); // label_cnt.Text 
                                        string local_exe_Text = "";
                                        exe.Invoke(new Action(() => { local_exe_Text = exe.Text; })); // since we are not on the form's thread, this is a safe way to get information from there. Without it we may get an exception.
                                                                                                      // string local_param_list_text = "";
                                                                                                      //param_list[par].Invoke(new Action(() => { local_param_list_text = ext_param_list[par]; })); // since we are not on the form's thread, this is a safe way to get information from there. Without it we may get an exception.
                                        p[i] = run(local_exe_Text, expand_string(ext_param_list[par], fileName), outfilename, 1 << (i - 1));
                                        List<float> l = new List<float>();
                                        processes[p[i]] = new benchmark(ext_param_list[par], fileName, l);
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
            if (cnt == 0) return;
            if (checkBox_remote.Checked)
            {
                bg.ReportProgress(0, "Waiting for remote termination... "); 
                wait_for_remote_Termination();                
            }
            else
            {
                wait_for_Termination();

                bg.ReportProgress(0, "* all processes finished *");
                stopwatch.Stop();

                string time = (Convert.ToSingle(stopwatch.ElapsedMilliseconds) / 1000.0).ToString();
                bg.ReportProgress(0, "# of benchmarks:" + cnt);

                bg.ReportProgress(0, "parallel time = " + time);
                bg.ReportProgress(0, "============================");

                bg.ReportProgress(1, time); //label_paralel_time.Text            
                bg.ReportProgress(5, failed.ToString());
            }            
        }

        private Tuple<int, string, string> run_remote(string v, object p)
        {
            throw new NotImplementedException();
        }

        private string expand_string(string v1, string bench_remote_path, object p, string v2)
        {
            throw new NotImplementedException();
        }

        // called from background-worker thread
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
            }
        }

        #endregion

        #region GUI

        void init_plot_files()
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
                MessageBox.Show("Cannot create csv files for cpbm in " + graphDir + ".\n Here is the exception text:\n" + ex.ToString());
                return;
            }
        }

        bool test_dir_compatibility()
        {
            string remote_bench_path = ConfigurationManager.AppSettings["remote_bench_dir"];
            string remote_bench_dir = Path.GetFileName(Path.GetDirectoryName(remote_bench_path));
            if (dir.Text[dir.Text.Length - 1] != '\\') dir.Text += "\\";
            string local_bench_dir = Path.GetFileName(Path.GetDirectoryName(dir.Text));
            if (remote_bench_dir != local_bench_dir)
                if (MessageBox.Show("Remote bench dir = " + remote_bench_dir + ", local dir = " + local_bench_dir + ". Continue ? ", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.Cancel) return false;
            return true;
        }

        private void button_start_Click(object sender, EventArgs e)
        {            
            if (File.Exists(csv.Text) && IsFileLocked(new FileInfo(csv.Text))) { 
                MessageBox.Show("seems that " + csv.Text + " is in use. Close it and try again.");
                return;
            }
            if (checkBox_remote.Checked &&  !test_dir_compatibility()) return;
            label_paralel_time.Text = "";            
            label_cnt.Text = "";
            label_fails.Text = "";
            labels.Clear();            
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
                BenchmarkNamesFromCsv.Clear();
                if (checkBox_filter_csv.Checked && File.Exists(csv.Text)) readBenchmarkNamesFromCsv();
                
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
            bg.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker1_Completed);

            if (!checkBox_remote.Checked)
            {
                if (preserveFirstCores)
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

            // update before run, in case we changed. 
            searchPattern = filter_str.Text;
            benchmarksDir = dir.Text;
            bg.RunWorkerAsync();
            
        }

        private void backgroundWorker1_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error!= null) return;
            if (checkBox_remote.Checked)
            {
                try { import_remote_out(); }
                catch { return; }
            }
            
            buildcsv();
            scrolldown();
        }

        private void button_kill_Click(object sender, EventArgs e)
        {
            if (checkBox_remote.Checked)
            {

                // the following command produeces, e.g., qstat -uofers | grep "ofers" | cut -d"." -f1 | xargs qdel, which kills all prcesses by user ofers.
                string remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
                if (MessageBox.Show("Delete all processes of user " + remote_user + "?", "Confirm kill processes", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    string outText = run_remote("ssh ", remote_user + " \"qstat -u" + ConfigurationManager.AppSettings["remote_user"] + "| grep \"" +
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

                if (preserveFirstCores) // we changed affinity of other processes, now we retrieve it. 
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
            scrolldown();
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
        
        // called from background-worker thread
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
                    foreach (string st in sets.Last())
                    {
                        if (st == "")
                        {
                            MessageBox.Show("Warning: Empty element in parameter set " + sets.Count);
                            return;
                        }
                    }
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
                            if (i < indices.Count - 1) res += str.Substring(indices[i].Item2 + 1, indices[i + 1].Item1 - 1 - indices[i].Item2); // e.g., res = "-par1 = 1 -par2 = "
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
            // pre-conditions
            int param1 = getCheckedRadioButton(scatter1);
            if (param1 == -1) return;
            int param2 = getCheckedRadioButton(scatter2);
            if (param2 == -1) return;
            if (param1 == param2) { MessageBox.Show("Please choose 2 different params."); return; } 
            if (param_list[param1].Text.IndexOf("{") != -1 || param_list[param2].Text.IndexOf("{") != -1) { MessageBox.Show("Please specify scatter graphs without { } (product) symbols."); return; }
            if (param_list[param1].Text == noOpTag || param_list[param2].Text == noOpTag) { MessageBox.Show("Param cannot be " + noOpTag); return; }

            
            //prepare_plot_data();
            try
            {
                if (!prepare_plot_data_fromCSV()) return;
            }
            catch (Exception ex)
            {
                listBox1.Items.Add(ex.ToString());
                return;
            }

            // preparing process for running cpbm's batch file. 
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "run-scatter.bat";
            string f1 = normalize_string(param_list[param1].Text), f2 = normalize_string(param_list[param2].Text);
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
            //   prepare_plot_data();
            try
            {
                if (!prepare_plot_data_fromCSV()) return;
            }
            catch { return; }


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
            timeout.Enabled = min_mem.Enabled = exe.Enabled = checkedListBox_cores.Enabled = !(((CheckBox)sender).Checked);
            checkBox_copy.Enabled = button_import.Enabled = (((CheckBox)sender).Checked);
            checkBox_CheckedChanged(sender, e);
        }

        private string getfield(string line, header_fields field)
        {
            return getfield(line, (int)field);
        }

       private string getfield(string line, int idx) // if this does not work, check it is not equivalent to the version below.
        {
            string[] fields = line.Split(',');            
            if (idx >= fields.Length) return "";
          //  string old = getfield1(line, idx);
        //    Debug.Assert(fields[idx] == old );
            return fields[idx];
        }
        // Gets field # idx in a comma-separated line
        // obselete
     /*   private string getfield1(string line, int idx)
        {
            int i=0;
            for (int j = 0; j < idx; ++j)
            {
                i = line.IndexOf(',', i + 1);
            }
            if (i == -1) return "";
            if (idx == 0) i--; 
            // getting the length of the field
            int k = line.IndexOf(',', i + 1); // location of next comma
            if (k == -1) k = line.Length; // in case idx was last
            k = k - i - 1; // subtract location of current comma
            string res = line.Substring(i+1, k); // line.IndexOf(',', i + 1)-i-1            
            return res;
        }
     */

        private void del_Allfail_benchmark()
        {            
            if (MessageBox.Show("This operation erases files. continue ? ", "confirm deletion", MessageBoxButtons.YesNo) == DialogResult.No) return;
            Hashtable benchmarks = new Hashtable();
            string fileName = csv.Text;
            HashSet<string> failed_all = new HashSet<string>();
            int cnt = 0;
            // finding failed benchmarks 
            bool readheader = true;
            try
            {
                foreach (string line in File.ReadLines(fileName))
                {
                    if (readheader)
                    {                     
                        readheader = false;
                        continue;
                    }
                    benchmarks[getfield(line, header_fields.bench)] = getfield(line, header_fields.dir);
                }
            }
            catch { MessageBox.Show("seems that " + csv.Text + "is in use"); return; }

            readheader = true;
            foreach (string line in File.ReadLines(fileName))
            {
                if (readheader)
                {
                    readheader = false;
                    continue;
                }
                if (getfield(line,header_fields.fail) == "" &&
                    getfield(line, header_fields.timedout) == "" )                
                        benchmarks.Remove(getfield(line, header_fields.bench));                
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
            scrolldown();

            var linesToKeep = File.ReadLines(fileName).Where(l => !failed_all.Contains(getfield(l, header_fields.bench)));
            
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
                        timeFieldLocation = labels1.FindIndex(x => x.Equals("time", StringComparison.OrdinalIgnoreCase));                        
                        if (timeFieldLocation == -1)
                        {
                            MessageBox.Show("cannot find field 'time' in header of " + fileName);
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
                            failed_short_once.Add(getfield(line, header_fields.bench));
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
            var linesToKeep = File.ReadLines(fileName).Where(l => (!failed_short_once.Contains(getfield(l, header_fields.bench)) || getfield(l, header_fields.param) == "param"));   // second item so it includes the header.

            var tempFile = Path.GetTempFileName();

            File.WriteAllLines(tempFile, linesToKeep);

            File.Delete(fileName);
            File.Move(tempFile, fileName);
        }

        private void button_mark_fails_Click(object sender, EventArgs e)
        {
            string fileName = csv.Text;
            HashSet<string> failed_atleast_once = new HashSet<string>();
            bool header = true;
            int cnt = 0;
            // finding failed benchmarks 
            try
            {
                foreach (string line in File.ReadLines(fileName))
                {
                    cnt++;
                    if (header) { header = false; continue; }
                    string failed = getfield(line, header_fields.fail);
                    string timedout = getfield(line, header_fields.timedout);
                    if (failed.Length == 0 && timedout.Length == 0) continue;                    
                    listBox1.Items.Add("failed/timeout benchmark: " + line);
                    failed_atleast_once.Add(Path.Combine(getfield(line, header_fields.dir), getfield(line, header_fields.bench)));
                }
            }
            catch { MessageBox.Show("seems that " + csv.Text + "is in use"); return; }

            // keeping only benchmarks that are not failed by any parameter combination.            
            StreamReader sr = new StreamReader(fileName);
            string header_line = sr.ReadLine();
            int i = 1;
            int failed_column = 0;
            string failed_column_text = "failed with some param";
            string res;
            while (true) {
                res = getfield(header_line, i);
                if (res == "") break;
                if (res == failed_column_text)
                {
                    failed_column = i;
                    break;
                }
                ++i;
            }
            if (failed_column == 0) header_line += "," + failed_column_text;
            sr.Close();
            List<string> lines = new List<string>();
            foreach (string line in File.ReadLines(fileName))
            {
                if (getfield(line, header_fields.param) == "param")
                {
                    lines.Add(header_line);
                    continue;
                }
                string line_st = line;
                if (failed_column > 0) line_st = line_st.Substring(0, line_st.LastIndexOf(",")); // removing old value, including ','
                if (!failed_atleast_once.Contains(Path.Combine(getfield(line, header_fields.dir), getfield(line, header_fields.bench))))
                {                    
                    lines.Add(line_st + ","); 
                }
                else
                {
                    lines.Add(line_st + ",1");
                }
            }        

            var tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, lines);
            File.Delete(fileName);
            File.Move(tempFile, fileName);
            scrolldown();
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
                    string failed = getfield(line, header_fields.fail);
                    if (failed.Length == 0) continue;
                    Debug.Assert(failed == "1");
                    failed = getfield(line, header_fields.timedout);
                    listBox1.Items.Add("failed line: " + line);
                    failed_atleast_once.Add(Path.Combine(getfield(line, header_fields.dir), getfield(line, header_fields.bench)));
                }
                scrolldown();
            }
            catch { MessageBox.Show("seems that " + csv.Text + "is in use"); return; }
            
            // keeping only benchmarks that are not failed by any parameter combination. 
            var linesToKeep = File.ReadLines(fileName).Where(l => (!failed_atleast_once.Contains(Path.Combine(getfield(l, header_fields.dir), getfield(l, header_fields.bench))) || getfield(l, header_fields.param) == "param"));   // second item so it includes the header.         
            
            var tempFile = Path.GetTempFileName();

            File.WriteAllLines(tempFile, linesToKeep);

            File.Delete(fileName);
            File.Move(tempFile, fileName);
            string msg = "Kept " + (linesToKeep.Count()) + " lines out of " +  cnt;
            listBox1.Items.Add(msg);

        }

        protected virtual bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }


        // import remote files
        // called from background-worker thread
        void import_remote_out()
        {
            if (!checkBox_remote.Checked) return;
            if (ConfigurationManager.AppSettings["remote_bench_dir"].LastIndexOf("/") != ConfigurationManager.AppSettings["remote_bench_dir"].Length - 1)
            {
                MessageBox.Show("remote_bench_dir as defined in .config file has to terminate with a '/'. Aborting.");             
                return;
            }

            int in_csv = 0, imported =0;
            listBox1.Items.Add("--- Importing ---");
            listBox1.Refresh();
            dir.Invoke(new Action(() => { benchmarksDir = dir.Text; }));                        
            filter_str.Invoke(new Action(() => { searchPattern = filter_str.Text; }));
            var fileEntries = getFilesInDir();
            if (fileEntries.Count == 0) listBox1.Items.Add("empty file list\n");
            
            processes.Clear();
            BenchmarkNamesFromCsv.Clear();
            if (checkBox_filter_csv.Checked && File.Exists(csv.Text)) readBenchmarkNamesFromCsv();
            
            //if (checkBox_remote.Checked) listBox1.Items.Add("Files will be imported to " + Directory.GetCurrentDirectory());
            
            string remote_user = "", remote_bench_path = "";

            remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
            remote_bench_path = remote_user + ":" + ConfigurationManager.AppSettings["remote_bench_dir"];


            expand_param_list();
            for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
            {                
                foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                {
                    string fileName = fileinfo.FullName;                    
                    string id = getid(ext_param_list[par], fileName);
                    if (BenchmarkNamesFromCsv.Contains(id)) { in_csv++; continue; }                    
                    string outfileName = outfile(fileName, ext_param_list[par]); // we import from the same directory as the source cnf file;                    

                    // download those files to the local dir. 
                    string relativefilename = fileName.Substring(dir.Text.Length).Replace('\\','/'); // e.g. suppose dir = test and the file is in test\dir1\a.cnf, then we get dir1/a.cnf
                    string remote_outfileName = outfile(relativefilename, ext_param_list[par]); // we import from the working directory (bench/bin/release/ or debug/)                        
                    if (!filterOut(outfileName))
                    {
                        int subdirLength = relativefilename.IndexOf('/');
                        string local = subdirLength == -1? "." : relativefilename.Substring(0,subdirLength );
                        Tuple<int, string,string> res = run_remote("scp ", remote_bench_path + remote_outfileName + " " + local);
                        string outText = res.Item2; // download the file
                        imported++;
                        listBox1.Items.Add(outText);
                        if (res.Item1 != 0) listBox1.Items.Add("*** Warning: exit code " + res.Item1);
                        listBox1.Refresh();                        
                    }
                }
                listBox1.Refresh();
            }

            listBox1.Items.Add(in_csv.ToString() + " benchmarks already in the csv file.");
            listBox1.Items.Add(imported.ToString() + " imported.");            
        }

        private void button_import_Click(object sender, EventArgs e)  // import out files from remote server, and process them to generate the csv + plot files. 
        {
            labels.Clear();  // since we may import more than once. 
            if (!test_dir_compatibility()) return;
            try {
                import_remote_out();
                buildcsv();
                scrolldown();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                return; }
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
            if (history[fieldValue].Contains(text)) history[fieldValue].Remove(text);
            history[fieldValue].Insert(0, text);
            ((ComboBox)sender).DataSource = history[fieldValue];
            write_history_file = true;
        }

        private void textBox_Leave(object sender, EventArgs e) // only used for param_groups
        {
            string text = ((TextBox)sender).Text;
            if (text == noOpTag) return;
            fields fieldValue = (fields)Enum.Parse(typeof(fields), "param_groups");
            if (!history.ContainsKey(fieldValue)) history[fieldValue] = new List<string>();
            // remove (if exists) and insert to put the latest first in the order. 
            if (history[fieldValue].Contains(text)) history[fieldValue].Remove(text);            
            history[fieldValue].Insert(0, text);            
            write_history_file = true;                        
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
            p.StartInfo.FileName = "notepad++";
            p.StartInfo.Arguments = "hbench.exe.config";
            //p.StartInfo.WorkingDirectory = Application.StartupPath;
            p.Start();
        }

        private void copy_Click(object sender, EventArgs e)
        {                   
                string s = "";
                foreach (object o in listBox1.Items)
                {
                    s += o.ToString() + "\n";
                }
                Clipboard.SetText(s);            
        }

        private void btn_clear_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }

        private void button_save_Click(object sender, EventArgs e)
        {
            write_history();
        }

        private void stat_field_Click(object sender, EventArgs e)
        {
            readLabelsFromCsv();
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
        public bool timedout;

        public benchmark(string param, string name, List<float> res)
        {
            this.param = param;
            this.name = name;
            this.res = res;
            this.timedout = false;
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

