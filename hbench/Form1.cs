/*
 * HBench - a GUI-based platform for performance benchmarking
 * Author: Ofer Strichman ofers@ie.technion.ac.il
 * Distributed freely under the GPL license
 */

// TODO: use csvhelper. 


using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.VisualBasic;
using OfficeOpenXml;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace bench
{
    public partial class filter : Form
    {
        // reading from config  file: 
        string history_file = Path.Combine(System.Windows.Forms.Application.StartupPath, ConfigurationManager.AppSettings["history_filename"]);//"history.txt"
        string graphDir = ConfigurationManager.AppSettings["cpbm"]; //@"c:\temp\cpbm-0.5\";
        // If this file gets locked: use c:\temp\handle.exe to find which process locks it. 
        StreamWriter logfile = new StreamWriter(ConfigurationManager.AppSettings["log"]);
        string stat_tag = ConfigurationManager.AppSettings["stat_tag"]; // ###
        string abort_tag = ConfigurationManager.AppSettings["abort_tag"];

        // currently the timeout featured is practically turned off. 
        // The application has to create a time-out field like any other field, e.g.,
        // see chrono (by catching sigint). 
        readonly string timedout_Tag = ConfigurationManager.AppSettings["timedout_tag"];
        readonly string time_Tag = ConfigurationManager.AppSettings["time_tag"];
        bool hyperthreading = ConfigurationManager.AppSettings["hyperthreading"] == "true";
        static int param_list_size = int.Parse(ConfigurationManager.AppSettings["param_list_size"]);

        // more configurations:   
        int timeout_val = Timeout.Infinite; // will be read from history file
        int MinMem_val = 0;  // in MB. Will be read from history file        
        bool preserveFirstCores = ConfigurationManager.AppSettings["PreserveFirstCores"] == "true";
        int firstcore;
        int cores = Environment.ProcessorCount;
        List<int> active = new List<int>();
        
        // = new List<int>(cores); // {3, 5, 7 }; 
        int failed = 0;
        const string labelTag = "^"; // adding labels to the parameter list. These will not join the actual parameters. 
        const string noOpTag = "<>";
        const char setSeparator = '|';

        enum fields
        {
            exe, dir, wdir, filter_str, maxfiles, csv, param, param_groups, stat_field, core_list, timeout, min_mem,  // combos
            checkBox_skip_long_runs, checkBox_remote, checkBox_rec, checkBox_rerun_empty_out, checkBox_filter_out, checkBox_filter_csv, checkBox_copy, // checkboxes
            misc
        }; // elements maintained in the history file
        enum header_fields { exedate, param, dir, bench, fail }; // these are not reported in the out files, yet they are part of each record. 
        List<string> labels = new List<string>();  // never includes header_fields. 
        // declarations:
        readonly ConcurrentDictionary<Process, benchmark> processes = new ConcurrentDictionary<Process, benchmark>();
        List<string> failed_benchmarks;        
        // the list of labels below represents the union of lables in the various output files processed 
        // so far (up to 'reset csv') + labels added via 'mark winners'/'mark fails'.       

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
                param_list[i].Location = new Point(60, i * 25);
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
            string[] lines = new string[] { "" };
            try
            {
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
                if (line.Length >= 2 && line.Substring(0, 2) == "--")
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
                    { 
                    listBox1.Items.Add("could not find entry for " + C.Name + " in history file" );
                    }   // could be missing entry in the history file, so we let it go through. 
                }
                else if (type == typeof(CheckBox))
                {
                    try
                    {
                        fields field = (fields)Enum.Parse(typeof(fields), C.Name);
                        ((CheckBox)C).Checked = history[field][0] == "yes";
                    }
                    catch
                    {
                        listBox1.Items.Add("could not find entry for " + C.Name + " in history file");
                    }
                }
            }

            // updating core list
            try
            {
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
            foreach (fields field in Enum.GetValues(typeof(fields)))
            {

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
            // to make param a legal file name. Might have a problem with '-' because 
            // some parameters use negative values. We cannot use in the replacement 
            // string a "-" because having this in the file name makes scatter/cactus 
            // refer to this as a parameter.
            string res = s.Replace("=", "").Replace(" ", "").Replace("_", "").Replace(labelTag, "").Replace("%f", "").Replace("-", "").Replace(id_prefix, "");
            if (res == "") res = "NoArgs";
            return res;
        }


        // called from background-worker thread
        string expand_string(string s, string filename, string param = "", string outfilename = "")  // the last two are used for remote execution
        {
            string res = s.Replace("%f", filename).Replace("%p", param).Replace("%o", outfilename);
            if (res == s) return res;
            else return expand_string(res, filename, param, outfilename);  // recursive because the replacing strings may contain %directives themselves.
        }


        string strip_id_prefix(string param)
        {
            Debug.Assert(param.Substring(0, 3) == id_prefix);
            return param.Substring(3);
        }


        public static List<string> ExcelGetLine(string filePath, string sheetName, int rowidx)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Excel file not found", filePath);

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var ws = package.Workbook.Worksheets[sheetName];
                if (ws == null)
                    throw new System.Exception($"Worksheet '{sheetName}' not found");

                if (ws.Dimension == null)
                    return new List<string>(); // empty sheet

                int cols = ws.Dimension.End.Column;
                var row = new List<string>();

                for (int col = 1; col <= cols; col++)
                {
                    row.Add(ws.Cells[rowidx, col].Text);
                }

                return row;
            }
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
            string header;

            List<string> vals = new List<string>();
            StreamReader csvfile;
            int offset = 0;
            try
            {
                string ext = Path.GetExtension(csv.Text);
                if (ext == ".xlsx")
                {
                    labels = ExcelGetLine(csv.Text, ConfigurationManager.AppSettings["ExcelTabName"], 1);
                    vals = ExcelGetLine(csv.Text, ConfigurationManager.AppSettings["ExcelTabName"], 2);
                }
                else if (ext == ".csv") {                 
                    csvfile = new StreamReader(csv.Text);      //(@"C:\temp\res.csv");
                    labels = csvfile.ReadLine().Split(',').ToList<string>(); // header
                    vals = csvfile.ReadLine().Split(',').ToList<string>();                                        
                    csvfile.Close();
                }
                if (labels.Count == 0 || vals.Count == 0) throw new System.ArgumentException("fail");
                offset = Enum.GetValues(typeof(header_fields)).Length;
                labels.RemoveRange(0, offset);
            }
            catch (Exception)
            {
                MessageBox.Show("cannot read labels from " + csv.Text);
                return;
            }            
            stat_field.DataSource = null;
            stat_field.Items.Clear();
            decimal res;
            // only include labels that the entry in the next line is either a number or empty.
            // We use decimal because it permits e.g. 1.3E7
            for (int i = 0; i < labels.Count() && i < vals.Count(); ++i)
                if (decimal.TryParse(vals[i + offset], NumberStyles.Any, CultureInfo.InvariantCulture, out res) || vals[i + offset] == "")
                    stat_field.Items.Add(labels[i]);
        }


        void readBenchmarkNamesFromCsv()
        {
            List<List<string>> data = getDataFromFile();
            string res;

            foreach (var row in data)            
            {
                if (get_field(row, header_fields.param) == "") continue;
                res = getid(get_field(row, header_fields.param), get_field(row, header_fields.dir), get_field(row, header_fields.bench), "");
                BenchmarkNamesFromCsv.Add(res);
            }            
        }

        public static List<List<string>> ExcelReadAllLines(string filePath,string tab)
        {
            //ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            var result = new List<List<string>>();

            if (!File.Exists(filePath))
                return result;   // return empty list if file doesn't exist

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[tab];
                if (worksheet == null)
                    return result;

                // This returns values that can be larger than the actual values.
                int rows = worksheet.Dimension.End.Row;
                int cols = worksheet.Dimension.End.Column;

                // Here we find the real dimensions:

                // The real #rows: 
                int lastrow = rows;
                bool end = false;
                for (; lastrow >= 1 && !end; )
                {
                    for (int col = 1; col <= cols; col++)
                    {
                        if (!string.IsNullOrWhiteSpace(worksheet.Cells[lastrow, col].Text))
                        {
                            end = true;
                            break;
                        }
                    }
                    if (!end) lastrow--;
                }
                rows = lastrow;

                // The real #cols: 
                int lastcol = cols;
                end = false;
                for (; lastcol >= 1 && !end; )
                {
                    for (int row = 1; row <= rows; row++)
                    {
                        if (!string.IsNullOrWhiteSpace(worksheet.Cells[row, lastcol].Text))
                        {
                            end = true;
                            break;
                        }
                    }
                    if (!end) lastcol--;
                }
                cols = lastcol;

                // read the data
                for (int row = 1; row <= rows; row++)
                {
                    var line = new List<string>();
                    for (int col = 1; col <= cols; col++)
                    {
                        line.Add(worksheet.Cells[row, col].Text);
                    }
                    result.Add(line);
                }
            }

            return result;
        }

        List<List<string>> getDataFromFile()
        {
            List<List<string>> res = new List<List<string>>();
            List<string> row = new List<string>();
            List<string> lines = new List<string>(); // with commas
            string ext = Path.GetExtension(csv.Text);
            try
            {
                if (ext == ".csv")
                {
                    lines = File.ReadAllLines(csv.Text).ToList<string>();
                    foreach (var l in lines)
                    {
                        row = l.Split(',').ToList<string>();
                        res.Add(row);
                    }
                }

                else if (ext == ".xlsx")
                {
                    res = ExcelReadAllLines(csv.Text, ConfigurationManager.AppSettings["ExcelTabName"]);
                }
                else listBox1.Items.Add("unsupported file type: " + ext);

                if (res.Count == 0)
                {
                    listBox1.Items.Add("empty file ? ");
                    return res;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n seems that " + csv.Text + " is in use");
                throw;
            }
            return res;
        }

        // The point about this function is that it may be called after labels from other out files 
        // were added. This keeps it all aligned with the new fields. 
        List<List<string>> readAndCompleteData()
        {
            List<List<string>> res = getDataFromFile();

            int offset = Enum.GetValues(typeof(header_fields)).Length;

            foreach (var r in res.Skip(1))  // skip header
            {   
                for (int i = r.Count - offset; i < labels.Count; ++i) r.Add("-1");                
            }            
            return res;
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
                benchmark data = processes[p];
                failed_benchmarks.Add(data.name);
                failed++;
                bg.ReportProgress(5, failed.ToString());
                try
                {
                    KillProcessAndChildren(p.Id);
                }
                catch
                {
                    bg.ReportProgress(0, "could not kill process " + p.StartInfo.Arguments);
                }
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
            if (msg == null) return;
            listBox1.Items.Add(msg);
            listBox1.Refresh();
            scrolldown();
            if (tofile)
            {
                logfile.WriteLine(msg);
            }
        }

        // called from background-worker thread
        bool filterOut(string outfilename)
        {
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
            int loc = searchPattern.LastIndexOf('.');
            if (loc < 0)
            {
                listBox1.Items.Add("no '.' in filter pattern ? ");
                return res;
            }
            string extension = searchPattern.Substring(loc);
            try
            {
                filelist = new DirectoryInfo(benchmarksDir).GetFiles(searchPattern, checkBox_rec.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch
            {
                MessageBox.Show("cannot open directory " + benchmarksDir + ". Aborting.");
                System.Windows.Forms.Application.Exit();
            }

            int counter = int.MaxValue;
            string text = "";
            maxfiles.Invoke(new Action(() => { text = maxfiles.Text; }));
            if (!int.TryParse(text, out counter))
            {
                bg.ReportProgress(0, "Non-numeric value in max-files. Putting no limits on # of files.");
                counter = int.MaxValue;
            };
            if (counter == 0) counter = int.MaxValue;
            if (counter >= filelist.Count()) return filelist.ToList();
            foreach (FileInfo fi in filelist)
            {
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
                    listBox1.Items.Add("waiting to read " + filename);
                    Thread.Sleep(3000);
                }
            }
            if (file == null)
            {
                listBox1.Items.Add("cannot open " + filename);
                return false;
            }
            string line;

            while ((line = file.ReadLine()) != null)
            {
                if (line.Length <= stat_tag.Length) continue;
                if (!line.Contains(stat_tag)) continue;
                // for cases in which the previous line in the output did not have an eol. 
                if (line.Substring(0, stat_tag.Length) != stat_tag) line = line.Substring(line.IndexOf(stat_tag));

                var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); // The RemoveEmptyEntries takes care of multiple spaces. 
                Debug.Assert(parts.Length == 3); // e.g. ### Time 12.34
                string tag = parts[1];

                if (tag == abort_tag || tag == "SAT")
                {
                    listBox1.Items.Add("* * * * * * * * * * * * *  Abort!");
                    file.Close();
                    return true;
                }

                float res;

                benchmark data = processes[p];

                if (float.TryParse(parts[2], out res))
                {
                    if (!labels.Exists(x => x == tag)) labels.Add(tag);
                    success = true;
                    data.res.Add(tag, res);
                }
                else listBox1.Items.Add("skipping non-numerical data: " + parts[2]);
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

                        if (tag == time_Tag && res < 30.0)
                        {
                            listBox1.Items.Add("removing " + filename + ". Too easy.");
                            del = true;
                            return false; // !! remove easy instances
                        }

                        benchmark data = processes[p];
                        data.res.Add(tag, res);

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
                res = run_remote(ConfigurationManager.AppSettings["local_ssh_cmd"], remote_user + " \"qstat -u " + ConfigurationManager.AppSettings["remote_user"] + "| grep \"" + ConfigurationManager.AppSettings["remote_user"] + "\"").Item1;
                if (res != 0) break;
                Thread.Sleep(10000); // 10 seconds wait                        
            }
            bg.ReportProgress(0, DateTime.Now.ToString("H:mm:ss") + ": * All remote processes terminated *");
            scrolldown();
            if (bg != null)
            {
                bg.Abort();
                bg.Dispose();
            }
        }

        // called from background-worker thread
        void wait_for_Termination()
        {
            foreach (KeyValuePair<Process, benchmark> entry in processes)
            {
                Process p1 = (Process)entry.Key;
                if (!p1.HasExited) p1.WaitForExit();
            }
            if (bg != null)
            {
                bg.Abort();
                bg.Dispose();
            }
        }

        bool prepareDataForCsv()
        {
            listBox1.Items.Add("Preparing data for csv file");
            int in_csv = 0;

            filter_str.BeginInvoke(new Action(() => { searchPattern = filter_str.Text; }));
            dir.BeginInvoke(new Action(() => { benchmarksDir = dir.Text; }));
            var fileEntries = getFilesInDir();
            if (fileEntries.Count == 0)
            {
                listBox1.Items.Add("empty file list\n");
                return false;
            }

            BenchmarkNamesFromCsv.Clear();
            if (!chk_resetcsv.Checked && checkBox_filter_csv.Checked && File.Exists(csv.Text))
                readBenchmarkNamesFromCsv();

            bool first = true;

            expand_param_list();
            for (int engine = 0; engine <= 1; engine++) // we have an option to run two remote engines
            {
                if (engine == 1 && ((!checkBox_remote.Checked) || (ConfigurationManager.AppSettings["remote_ssh_cmd1"] == ""))) continue;
                for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
                {
                    string param = (engine == 0) ? ext_param_list[par] : remove_label(ext_param_list[par] ) + labelTag + ConfigurationManager.AppSettings["remote_ssh_cmd1_label"];

                    foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                    {
                        string fileName = fileinfo.FullName;
                        string id = getid(param, fileName);
                        if (BenchmarkNamesFromCsv.Contains(id))
                        {
                            in_csv++;
                            continue;
                        }
                        string outfileName = outfile(fileName, param); // we import from the same directory as the source cnf file;

                        if (File.Exists(outfileName))
                        {
                            bool exists = false;
                            Process p1 = null;
                            foreach (Process p in processes.Keys)
                            {
                                benchmark bench = processes[p] as benchmark;
                                if (bench.param != param || bench.name != fileName) continue;
                                exists = true;
                                p1 = p;
                                break;
                            }

                            if (!exists) // this happens only when there is already an .out file, hence a new process is not added to processes
                            {
                                p1 = new Process(); // we are only using this process as a carrier of the information from the file, so we can use the buildcsv function. 
                                Dictionary<string, float> l = new Dictionary<string, float>();
                                processes[p1] = new benchmark(param, fileName, l);
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
            return true;
        }

        public static void ExcelWrite(string filePath, List<List<string>> values)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Excel file not found", filePath);

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[ConfigurationManager.AppSettings["ExcelTabName"]];
                if (worksheet == null)
                    throw new System.Exception("Worksheet " + ConfigurationManager.AppSettings["ExcelTabName"] + " not found");
                                
                for (int row = 1; row <= values.Count; ++row)
                {
                    for (int col = 1; col <= values[row-1].Count; col++)
                    {
                        // The value should be inserted in the correct type, i.e. "123" as int, "123.0" as double, etc. 
                        // Try int
                        if (int.TryParse(values[row - 1][col - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                            worksheet.Cells[row, col].Value = val;
                        // Try double
                        else if (double.TryParse(values[row - 1][col - 1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double dval))
                            worksheet.Cells[row, col].Value = dval;
                        else worksheet.Cells[row, col].Value = values[row - 1][col - 1];
                    }
                }
                package.Save();
            }
        }
        
        public static void CsvWrite(string filePath, List<List<string>> table)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false
            };

            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, config))
            {
                foreach (var row in table)
                {
                    foreach (var cell in row)
                    {
                        csv.WriteField(cell);
                    }
                    csv.NextRecord();
                }
            }
        }


        void buildcsv()
        {
            if (chk_resetcsv.Checked)
            {
                DialogResult dialogResult = MessageBox.Show("reset csv ?", "reset csv ? ", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.No)
                {
                    chk_resetcsv.Checked = false;
                }
            }
            bool resetcsv = chk_resetcsv.Checked || !File.Exists(csv.Text);
            if (resetcsv) labels.Clear();
            var csvheader = new StringBuilder();
            
            string exedate = "";
            if (!checkBox_remote.Checked) exedate = File.GetLastWriteTime(exe.Text).ToString();

            if (!prepareDataForCsv()) return; // this reads the out files, fills 'labels' and then fills 'processes'. Returns false if no files were found. 
            List<List<string>> existingEntries = new List<List<string>>();
            if (!resetcsv) existingEntries = readAndCompleteData(); // it also fills missing fields with '-1'                        
            // updating the header
            if (existingEntries.Count > 0)
            {
                for (int i = 0; i < labels.Count; ++i)
                {
                    string lbl = labels[i];
                    // add labels
                    bool exists = false;
                    for (int j = 0; j < existingEntries[0].Count; ++j)
                    {
                        if (existingEntries[0][j] == lbl)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) existingEntries[0].Add(lbl);
                }
            }


            bool missingvalues = false;
            List<List<string>> table = new List<List<string>>();
            List<string> row = new List<string>();
            // add the header: 
            if (existingEntries.Count == 0)
            {
                string[] hd = (Enum.GetNames(typeof(header_fields)));
                table.Add(hd.ToList<string>());
            }
            
            foreach (var entry in processes)
            {
                benchmark bm = entry.Value as benchmark;
                Process p1 = (Process)entry.Key;

                var res = bm.res;
                
                row.Add(exedate);
                row.AddRange(getid(bm.param, bm.name).Split(',').ToList<string>()); // benchmark.  column.                
                row.Add(""); // There is an extra ',' because of the 'fail'
                
                // building the row
                for (int i = 0; i < labels.Count; ++i)
                {
                    string lbl = labels[i];
                    string st_res;
                    if (res.ContainsKey(lbl)) st_res = res[lbl].ToString();
                    else
                    {
                        st_res = "-1";
                        missingvalues = true;
                    }
                    row.Add(st_res);
                }
                table.Add(new List<string>(row));
                row.Clear();                
            }

            if (missingvalues) listBox1.Items.Add("*** Warning: Missing values found. Filled with '-1'");

            stat_field.DataSource = null;
            stat_field.Items.Clear();
            foreach (string lbl in labels) stat_field.Items.Add(lbl);
            try
            {   
                List<List<string>> Table;
                if (!resetcsv)
                {
                    existingEntries.AddRange(table);
                    Table = existingEntries;
                }
                else
                {
                    Table = table;
                }

                Write(Table); 
            }
            catch
            {
                MessageBox.Show("seems that " + csv.Text + " is in use");
                throw;
            }
            if (ConfigurationManager.AppSettings["add_fails_column"] == "true") button_mark_fails_Click(null, EventArgs.Empty);
            listBox1.Items.Add(DateTime.Now.ToString("H:mm:ss") + ": Added " + table.Count + " records to file");
        }


        void Write(List<List<string>> table)
        {
            if (Path.GetExtension(csv.Text) == ".csv")
            {
                CsvWrite(csv.Text, table);
            }
            else if (Path.GetExtension(csv.Text) == ".xlsx")
            {
                ExcelWrite(csv.Text, table);
            }

        }


        bool prepare_plot_data()
        {
            string line;
            List<List<string>> table = getDataFromFile();
                        
            List<Forplot> forplot = new List<Forplot>();  // saves information that is later used for generating the csv files for the plots. 
            Forplot fp;
            float maxval = 0;

            if (stat_field.Text == "")
            {
                MessageBox.Show("Please select a statistics field");
                return false;
            }            
            init_plot_files();
            
            // header
            List<string> header = table[0];

            int stat_field_col = header.IndexOf(stat_field.Text);
            if (stat_field_col < 0)
            {
                MessageBox.Show(stat_field.Text + " is not in the header of " + csv.Text);
                foreach (var key in csv4plot.Keys) ((StreamWriter)csv4plot[key]).Close();                
                return false;
            }
            Regex rgx = new Regex(filter_str.Text.Replace(".", @"\.").Replace("*", @".*"));

            foreach (var row in table)            
            {
                float val;
                if (!rgx.IsMatch(string.Join(",",row))) continue; // TODO: check
                //cols = line.Split(',').ToList();
                if (row.Count - 1 < stat_field_col) continue;
                string param = strip_id_prefix(get_field(row, header_fields.param));
                string key = normalize_string(param);
                if (!csv4plot.Contains(key)) continue; // This can happen if the csv file contains entries different than what appear in the GUI list. 
                if (row[stat_field_col] == "") continue; // timeout cases
                fp = new Forplot(
                    Path.Combine(get_field(row, header_fields.dir), get_field(row, header_fields.bench)),
                    param,
                    row[stat_field_col]
                    );
                forplot.Add(fp);
                if (float.TryParse(row[stat_field_col], out val) && val > maxval) maxval = val;
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
                    str = str.TrimEnd(' ');
                }
            }
            return str;
        }

        // called from background-worker thread
        Tuple<int, string, string> run_remote(string cmd, string args, bool wait = true) // for unix commands. Synchronous. 
        {            
            string local_dir_Text = "";
            Process p = new Process();

            p.StartInfo.FileName = cmd; //Note: if ssh is under a system32 folder, this won't work without admin rights. 
            p.StartInfo.Arguments = remove_label(args);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            dir.BeginInvoke(new Action(() => { local_dir_Text = dir.Text; }));
            p.StartInfo.WorkingDirectory = local_dir_Text;    // when executing a scp command, this will bring the files to the benchmarks dir. 


            try
            {
                p.Start();
            }
            catch (Exception ex) { MessageBox.Show("cannot start process " + p.StartInfo.FileName + "\n" + ex.Message); throw; }
            string output = "";
            if (wait)
            {
             //   output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                // returns <exist-status, command, output of command>                
                return new Tuple<int, string, string>(p.ExitCode, "> " + p.StartInfo.FileName + " " + p.StartInfo.Arguments, output);
            }

            return new Tuple<int, string, string>(0, "", ""); // only when we have wait = false. Not to be used. 

        }

        // called from background-worker thread
        Process run(string cmd, string args, string outfilename, int affinity = 0x007F)
        {

            Process p = new Process();
            string text = "";
            wdir.BeginInvoke(new Action(() => { text = wdir.Text; }));
            if (text != "") 
                p.StartInfo.WorkingDirectory = text;
            else
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(outfilename);

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

            var timer = new System.Threading.Timer(kill_process, p, timeout_val > 0 ? timeout_val : -1, Timeout.Infinite);           
            return p;
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            bg.ReportProgress(0,"directory = " + Directory.GetCurrentDirectory());
            int cnt_success = 0, cnt = 0;
            Process[] p = new Process[cores + 1];
            List<FileInfo> fileEntries = getFilesInDir();
            if (fileEntries.Count == 0) {
                bg.ReportProgress(0, "empty file list\n");
                e.Cancel = true;
                return;
            }
            string remote_user = "", remote_bench_path = "";
            if (checkBox_remote.Checked)
            {
                remote_user = ConfigurationManager.AppSettings["remote_user"] + "@" + ConfigurationManager.AppSettings["remote_domain"];
                remote_bench_path = remote_user + ":" + ConfigurationManager.AppSettings["remote_bench_dir"];
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool ok = false;
            bool copy_to_remote = checkBox_copy.Checked;
            expand_param_list();
            for (int engine = 0; engine <= 1; engine++) // we have an option to run two remote engines
            {
                if (engine == 1 && ((!checkBox_remote.Checked) || (ConfigurationManager.AppSettings["remote_ssh_cmd1"] == ""))) continue;
                string remote_cmd = (engine == 0) ? 
                    ConfigurationManager.AppSettings["remote_ssh_cmd"] : 
                    ConfigurationManager.AppSettings["remote_ssh_cmd1"];                
                
                for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
                {
                    if (ext_param_list[par].IndexOf("%f") == -1)
                    {
                        listBox1.Items.Add("Warning: param " + ext_param_list[par] + " does not include a %f directive. Skipping");
                        continue;
                    }
                    string param = (engine == 0) ? ext_param_list[par] : remove_label(ext_param_list[par] ) + labelTag + ConfigurationManager.AppSettings["remote_ssh_cmd1_label"];

                    bg.ReportProgress(0, "- - - - - " + param + "- - - - - ");
                    failed = 0;
                    results.Clear();
                    accum_results.Clear();
                    foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                    {
                        string fileName = fileinfo.FullName;
                        if (checkBox_skip_long_runs.Checked && failed_benchmarks.Contains(fileName))
                        {
                            bg.ReportProgress(0, "Skipping " + fileName + "; it timed-out with a previous configuration.");
                            continue;
                        }

                        string outfilename = outfile(fileName, param);
                        if (filterOut(outfilename))
                        {
                            bg.ReportProgress(0, "Skipping " + fileName + " due to existing out file.");
                            continue;
                        }

                        string id = getid(param, fileName);
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
                                                Tuple<int, string, string> res = run_remote(ConfigurationManager.AppSettings["local_scp_cmd"], relativepath + " " + target);
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
                                                res = run_remote(ConfigurationManager.AppSettings["local_ssh_cmd"], remote_user + " \"chmod 644 " + ConfigurationManager.AppSettings["remote_bench_dir"] + relativepath + "\"");
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


                                            bool runok = false;
                                            cnt++;

                                            for (int r = 0; r < 120 && !runok; ++r) // submitted too many, waiting for a process to terminate. 
                                            {
                                                Tuple<int, string, string> outTuple = run_remote(
                                                    ConfigurationManager.AppSettings["local_ssh_cmd"],
                                                    remote_user + " \"" + expand_string(remote_cmd, bench_remote_path, remove_label(param), outfile(bench_remote_path, param))
                                                    );

                                                if (outTuple.Item1 == 0)
                                                {
                                                    cnt_success++;
                                                    runok = true;
                                                    bg.ReportProgress(0, "exit code = " + outTuple.Item1); // exit status
                                                    bg.ReportProgress(0, outTuple.Item2); // command
                                                    bg.ReportProgress(0, outTuple.Item3); // output
                                                }
                                                else
                                                {
                                                    Thread.Sleep(5000);
                                                    bg.ReportProgress(0, "Trying again... (exit code " + outTuple.Item1 + ")");
                                                }
                                            }
                                            if (cnt != cnt_success) bg.ReportProgress(3, cnt_success.ToString() + "/" + cnt.ToString());
                                            else bg.ReportProgress(3, cnt_success.ToString()); // label_cnt.Text                                         

                                        }
                                        else
                                        {
                                            bg.ReportProgress(0, "running " + fileName + " on core " + i.ToString());
                                            cnt_success++;
                                            bg.ReportProgress(3, cnt_success.ToString()); // label_cnt.Text 
                                            string local_exe_Text = "";
                                            exe.BeginInvoke(new Action(() => { local_exe_Text = exe.Text; })); // since we are not on the form's thread, this is a safe way to get information from there. Without it we may get an exception.
                                                                                                          // string local_param_list_text = "";
                                                                                                          //param_list[par].BeginInvoke(new Action(() => { local_param_list_text = ext_param_list[par]; })); // since we are not on the form's thread, this is a safe way to get information from there. Without it we may get an exception.
                                            p[i] = run(local_exe_Text, expand_string(param, fileName), outfilename, 1 << (i - 1));
                                            Dictionary<string, float> l = new Dictionary<string, float>();
                                            processes[p[i]] = new benchmark(param, fileName, l);
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
            }

            // post processing

            bg.ReportProgress(4, "");
            if (cnt_success == 0) return;
            if (checkBox_remote.Checked)
            {
                bg.ReportProgress(0, DateTime.Now.ToString("H:mm:ss") + ": Waiting for remote termination... ");
                wait_for_remote_Termination();
            }
            else
            {
                wait_for_Termination();

                bg.ReportProgress(0, DateTime.Now.ToString("H:mm:ss") + ": * all processes finished *");
                stopwatch.Stop();

                string time = (Convert.ToSingle(stopwatch.ElapsedMilliseconds) / 1000.0).ToString();
                bg.ReportProgress(0, "# of benchmarks:" + cnt_success);

                bg.ReportProgress(0, "parallel time = " + time);
                bg.ReportProgress(0, "============================");

                bg.ReportProgress(1, time); //label_paralel_time.Text            
                bg.ReportProgress(5, failed.ToString());
            }
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
            if (File.Exists(csv.Text) && IsFileLocked(new FileInfo(csv.Text)))            {
                MessageBox.Show("seems that " + csv.Text + " is in use. Close it and try again.");
                return;
            }
            if (checkBox_remote.Checked && !test_dir_compatibility()) return;
            label_paralel_time.Text = "";
            label_cnt.Text = "";
            label_fails.Text = "";            
            bg = new AbortableBackgroundWorker();
            processes.Clear();
            accum_results.Clear();
            results.Clear();

            //int j = 0;
            foreach (int indexChecked in checkedListBox_cores.CheckedIndices)
            {
                active.Add(indexChecked + firstcore);
            }
            try  // in case the field contains non-numeral.
            {
                timeout_val = 1000 * Convert.ToInt32(timeout.Text); // need milliseconds.                 
            }
            catch { timeout_val = Timeout.Infinite; }

            try  // in case the field contains non-numeral.
            {
                MinMem_val = Convert.ToInt32(min_mem.Text);
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
            if (e.Cancelled || e.Error != null) return;
            if (checkBox_remote.Checked)
            {
                try { if (!import_remote_out()) return; }
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
                    string outText = run_remote(ConfigurationManager.AppSettings["local_ssh_cmd"], remote_user + " \"qstat -u" + ConfigurationManager.AppSettings["remote_user"] + "| grep \"" +
                      ConfigurationManager.AppSettings["remote_user"] + "\" | cut -d\".\" -f1 | xargs qdel\"", false).Item2;
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
                            listBox1.Items.Add("Failed to set affinity for process " + p.ProcessName);
                        }
                    }
                    listBox1.Items.Add("Retrieved Affinity");
                }
            }

            if (csvfile != null) csvfile.Close();
            button1.Enabled = true;

            if (bg != null)
            {
                bg.Abort();
                bg.Dispose();
            }
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
                    // removing spaces (e.g. {2 | 3 |4 } becomes "2","3","4" and not "2 "," 3 ","4 ")
                    // This is important in e.g. chrono, where the argument is expected to be without spaces
                    sets.Add(s.Split(setSeparator).Select(x => x.Trim()).ToArray());
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
                if (!prepare_plot_data()) return;
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
                if (!prepare_plot_data()) return;
            }
            catch { return; }


            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "run-cactus.bat";
            startInfo.Arguments = "";
            expand_param_list();
            if (ext_param_list.Count > 20) // if need more, change run-cactus.bat
                listBox1.Items.Add("Warning: only first 20 entries are sent to cactus plot");
            if (ext_param_list.Count > 9)
                listBox1.Items.Add("Warning: beyond 9 lines, 'tick' style is repeated. Change manually in the .tex.");
            for (int par = 0; par < ext_param_list.Count && (par < 20); ++par)  // for each parameter
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
            timeout.Enabled = wdir.Enabled = min_mem.Enabled = exe.Enabled = checkedListBox_cores.Enabled = !(((CheckBox)sender).Checked);
            checkBox_copy.Enabled = /*button_import.Enabled =*/ (((CheckBox)sender).Checked);
            checkBox_CheckedChanged(sender, e);
        }

        int get_field_idx(List<string> header, string title)
        {
            return header.IndexOf(title); 
        }

        //string get_field(string line, header_fields field)
        //{
        //    return get_field(line, (int)field);
        //}

        string get_field (List<string> row, header_fields field)
        {
            return row[(int)field];
        }

        string get_field(List<string> row, int idx) // if this does not work, check it is not equivalent to the version below.        {
        { 
            if (idx >= row.Count) return "";
            return row[idx];
        }

        List<string> remove_field(List<string> row, int idx)
        {            
            if (idx >= row.Count) return row;
            row.RemoveAt(idx);
            return row;
        }

        List<List<string>> remove_field(List<List<string>> table, int idx)
        {
            List<List<string>> res = new List<List<string>>();
            foreach (List<string> row in table) res.Add(remove_field(row, idx));
            return res;
        }

        private void del_Allfail_benchmark()
        {
            if (MessageBox.Show("This operation erases files. continue ? ", "confirm deletion", MessageBoxButtons.YesNo) == DialogResult.No) return;
            List<List<string>> table = getDataFromFile();
            Hashtable benchmarks = new Hashtable();
            
            HashSet<string> failed_all = new HashSet<string>();
            int cnt = 0;
            // finding failed benchmarks 
            
            List<string> header = table[0];            

            foreach (var row in table.Skip(1))
            {
                benchmarks[get_field(row, header_fields.bench)] = get_field(row, header_fields.dir);
            }

            int timedoutidx = get_field_idx(header, timedout_Tag);
            if (timedoutidx < 0)
            {
                listBox1.Items.Add("No column has the title " + timedout_Tag + ". The title is determined in the app.config file. Aborting.");
                return;
            }
            foreach (var row in table)
            {
                if (get_field(row, header_fields.fail) == "" &&
                    get_field(row, timedoutidx) == "0")
                    benchmarks.Remove(get_field(row, header_fields.bench));
            }

            foreach (string key in benchmarks.Keys)
            {
                string path = benchmarks[key] + "\\" + key;
                listBox1.Items.Add("deleting All-failed benchmark " + path);
                failed_all.Add(key);
                cnt++;
                try { File.Delete(path); }
                catch { listBox1.Items.Add("cannot delete " + path); }
            }
            listBox1.Items.Add("Deleted benchmarks: " + cnt);
            scrolldown();

            List<List<string>> linesToKeep = table.Where(row => !failed_all.Contains(get_field(row, header_fields.bench))).ToList();
            linesToKeep.Insert(0, header);
            var tempFile = Path.GetTempFileName();
            Write(linesToKeep);            
        }

        private void del_short_calls()
        {
            List<List<string>> table = getDataFromFile();
            
            HashSet<string> failed_short_once = new HashSet<string>();
            bool header = true;
            int timeFieldLocation = labels.IndexOf(stat_field.Text);


            try   {
                foreach (List<string> row in table)
                {
                    if (header)
                    {
                        List<string> labels1 = row;
                        timeFieldLocation = labels1.FindIndex(x => x.Equals(time_Tag, StringComparison.OrdinalIgnoreCase));
                        if (timeFieldLocation == -1)
                        {
                            MessageBox.Show("cannot find field 'time' in header of " + csv.Text);
                            return;
                        }
                        timeFieldLocation++; // because indexOf is 0-based
                        header = false;
                        continue;
                    }
                    string longesttime = get_field(row, timeFieldLocation);
                    double d;
                    bool isdouble = double.TryParse(longesttime, out d);
                    if (isdouble)
                    {
                        if (d < 1.0)
                        {                            
                            failed_short_once.Add(get_field(row, header_fields.bench));
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
            List<List<string>> linesToKeep = table.Where(l => (!failed_short_once.Contains(get_field(l, header_fields.bench)) || get_field(l, header_fields.param) == "param")).ToList();   // second item so it includes the header.

            Write(linesToKeep);
        }

        private void button_mark_fails_Click(object sender, EventArgs e)
        {
            const string title = "failed with some param";
            
            List<List<string>> table = getDataFromFile();
            List<string> header = table[0];
            
            if (get_field(header, header_fields.param) != "param")
            {
                listBox1.Items.Add("No heade line, Aborting.");
                return;
            }

            int idx = get_field_idx(header, title);
            if (idx >= 0)
            {
                listBox1.Items.Add("A " + title + " column already exist. Removing it...");
                table = remove_field(table, idx);
                header = table[0];
            }
            header.Add(title);
            labels.Add(title);
            table.RemoveAt(0);

            HashSet<string> failed_atleast_once = new HashSet<string>();

            int cnt = 0;

            // finding failed benchmarks 
            int timeout_idx = get_field_idx(header, timedout_Tag);

            foreach (List<string> row in table)
            {
                cnt++;
                string failed = get_field(row, header_fields.fail);
                string timedout = get_field(row, timeout_idx);
                if (failed.Length == 0 && timedout == "0") continue;
                failed_atleast_once.Add(Path.Combine(get_field(row, header_fields.dir), get_field(row, header_fields.bench)));
            }

            foreach (var row in table)
            {
                if (!failed_atleast_once.Contains(Path.Combine(get_field(row, header_fields.dir), get_field(row, header_fields.bench))))
                {
                    row.Add("0");
                }
                else
                {
                    row.Add("1");
                }
            }
            table.Insert(0, header);
            Write(table);
            listBox1.Items.Add("Finished marking benchmarks that timed-out with at least one parameter.");
            scrolldown();
        }

        private void button_del_fails_Click(object sender, EventArgs e)
        {
            
            HashSet<string> failed_atleast_once = new HashSet<string>();
            int cnt = 0;
            List<List<string>> table = getDataFromFile();

            List<string> header = table[0];
            if (get_field(header, header_fields.param) != "param")
            {
                listBox1.Items.Add("No header line, Aborting.");
                return;
            }
            
            int timedoutidx = get_field_idx(header, timedout_Tag);
            // finding failed benchmarks 
            try  {
                foreach (List<string> row in table.Skip(1))
                {
                    cnt++;
                    string failed = get_field(row, header_fields.fail);
                    if (failed.Length == 0) continue;
                    Debug.Assert(failed == "1");
                    failed = get_field(row, timedoutidx);
                    listBox1.Items.Add("failed line: " + string.Join(",",row));
                    failed_atleast_once.Add(Path.Combine(get_field(row, header_fields.dir), get_field(row, header_fields.bench)));
                }
                scrolldown();
            }
            catch { MessageBox.Show("seems that " + csv.Text + "is in use"); return; }

            // keeping only benchmarks that are not failed by any parameter combination. 

            List<List<string>> linesToKeep = table.Where(l => (!failed_atleast_once.Contains(Path.Combine(get_field(l, header_fields.dir), get_field(l, header_fields.bench))))).ToList();
            linesToKeep.Insert(0, header);

            var tempFile = Path.GetTempFileName();

            Write(linesToKeep);
            
            string msg = "Kept " + (linesToKeep.Count()) + " lines out of " + cnt;
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
        bool import_remote_out()
        {
            if (!checkBox_remote.Checked) return false;
            if (ConfigurationManager.AppSettings["remote_bench_dir"].LastIndexOf("/") != ConfigurationManager.AppSettings["remote_bench_dir"].Length - 1)
            {
                MessageBox.Show("remote_bench_dir as defined in .config file has to terminate with a '/'. Aborting.");
                return false;
            }
            if (!test_dir_compatibility()) return false;
            int in_csv = 0, imported = 0;
            listBox1.Items.Add("--- Importing ---");
            listBox1.Refresh();
            scrolldown();
            dir.BeginInvoke(new Action(() => { benchmarksDir = dir.Text; }));
            filter_str.BeginInvoke(new Action(() => { searchPattern = filter_str.Text; }));
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
            for (int engine = 0; engine <= 1; engine++) // we have an option to run two remote engines
            {
                if (engine == 1 && ((!checkBox_remote.Checked) || (ConfigurationManager.AppSettings["remote_ssh_cmd1"] == ""))) continue;

                for (int par = 0; par < ext_param_list.Count; ++par)  // for each parameter
                {
                    string param = (engine == 0) ? ext_param_list[par] : remove_label(ext_param_list[par] ) + labelTag + ConfigurationManager.AppSettings["remote_ssh_cmd1_label"];
                    if (true) // the new way: create a summary file with all lines remotely, and parse it locally. 
                        // This is much faster that doing it separately for each flie. 
                    {
                        string suffix = "*" + normalize_string(param) + ".out";
                        string cmd;
                        if (checkBox_rec.Checked) {
                            cmd = remote_user + " \"bash -c 'grep -H \\\"" + stat_tag + "\\\" -r --include=\"" + suffix + "\" " + ConfigurationManager.AppSettings["remote_bench_dir"] + "' > " + ConfigurationManager.AppSettings["remote_summary_file"] + "\"";
                                }
                        else 
                        cmd = remote_user + " \"bash -c 'grep -H \\\"" + stat_tag + "\\\" " + ConfigurationManager.AppSettings["remote_bench_dir"] + suffix + "' > " + ConfigurationManager.AppSettings["remote_summary_file"] +"\"";


                        Tuple<int, string, string> res = run_remote(ConfigurationManager.AppSettings["local_ssh_cmd"], cmd);
                        string outText = res.Item2;
                        listBox1.Items.Add(outText);
                        string local_dir_Text="";
                        dir.Invoke(new Action(() => { local_dir_Text = dir.Text; }));
                        Directory.SetCurrentDirectory(local_dir_Text);
                        res = run_remote(ConfigurationManager.AppSettings["local_scp_cmd"], remote_user + ":" + ConfigurationManager.AppSettings["remote_summary_file"] + " " + "summary.out");
                        
                        // store the data from the summary.out flie in a dictionary, where the file name is the key
                        Dictionary<string, List<string>> data = new Dictionary<string, List<string>>();
                        foreach (string line in File.ReadAllLines("summary.out"))
                        {                            
                            char[] separators = new char[] { ' ', ':' };
                            string[] cols = line.Split(separators);
                            Debug.Assert(cols.Length >= 4);
                            string filename = cols[0].Substring(cols[0].LastIndexOf('/') + 1);
                            if (BenchmarkNamesFromCsv.Contains(filename)) { in_csv++; continue; }
                            if (filterOut(filename)) continue;
                            listBox1.Items.Add($"{filename}");
                            string outline = cols[1] + " " + cols[2] + " " + cols[3] + "\n";
                            if (!data.ContainsKey(cols[0])) data[cols[0]] = new List<string>();
                            data[cols[0]].Add(outline);
                            scrolldown();
                        } 
                        
                        // create the out files
                        foreach (var d in data)
                        {
                            // The key is e.g. /home/ofers/ToDnnf/test/benchmarks/iscas85/or/c1355/c1355.aag.file.out
                            // we have to turn it into c:\...\benchmarks\iscas85\or\c1355\c1355.aag.file.out
                            
                            string text = d.Key;
                            string marker = ConfigurationManager.AppSettings["remote_bench_dir"];
                            if (text.StartsWith(marker))
                            {
                                text = text.Substring(marker.Length);
                            }
                            // so not we have e.g. /iscas85/or/c1355/c1355.aag.file.out
                            // we have to turn it into windows style path:
                            text = text.Replace('/', '\\'); // Unix to Windows
                            string fileName = Path.Combine(local_dir_Text, text);
                            if (File.Exists(fileName)) File.Delete(fileName);
                            foreach (string t in d.Value) File.AppendAllText(fileName, t);
                        }
                    }
                    else
                    {
                        foreach (FileInfo fileinfo in fileEntries)  // for each benchmark file
                        {
                            string fileName = fileinfo.FullName;
                            string id = getid(param, fileName);
                            if (BenchmarkNamesFromCsv.Contains(id)) { in_csv++; continue; }
                            string outfileName = outfile(fileName, param); // we import from the same directory as the source cnf file;                    

                            // download those files to the local dir. 
                            string relativefilename = fileName.Substring(dir.Text.Length).Replace('\\', '/'); // e.g. suppose dir = test and the file is in test\dir1\a.cnf, then we get dir1/a.cnf
                            string remote_outfileName = outfile(relativefilename, param); // we import from the working directory (bench/bin/release/ or debug/)                        
                            if (!filterOut(outfileName))
                            {
                                // grep-ing the ### lines from the out file:
                                Tuple<int, string, string> res = run_remote(ConfigurationManager.AppSettings["local_ssh_cmd"], remote_user + " \"rm /home/ofers/tmp.out\"");
                                string outText = res.Item2;
                                listBox1.Items.Add(outText);
                                if (res.Item1 != 0) listBox1.Items.Add("*** Warning: exit code " + res.Item1);
                                res = run_remote(ConfigurationManager.AppSettings["local_ssh_cmd"], remote_user + " \"grep '" + stat_tag + "' '" + ConfigurationManager.AppSettings["remote_bench_dir"] + remote_outfileName + "' > /home/ofers/tmp.out\"");
                                outText = res.Item2;
                                listBox1.Items.Add(outText);
                                if (res.Item1 != 0) listBox1.Items.Add("*** Warning: exit code " + res.Item1);
                                // downloading:
                                res = run_remote(ConfigurationManager.AppSettings["local_scp_cmd"], remote_user + ":/home/ofers/tmp.out " + remote_outfileName);
                                outText = res.Item2;
                                listBox1.Items.Add(outText);
                                if (res.Item1 != 0) listBox1.Items.Add("*** Warning: exit code " + res.Item1);
                                else imported++;
                                listBox1.Refresh();
                                scrolldown();
                            }
                        }

                        listBox1.Refresh();
                    }
                }
            }

            listBox1.Items.Add(in_csv.ToString() + " benchmarks already in the csv file.");
            listBox1.Items.Add(imported.ToString() + " imported.");
            return true;
        }

        private void button_import_Click(object sender, EventArgs e)  // import out files from remote server, and process them to generate the csv + plot files. 
        {

            if (chk_resetcsv.Checked) labels.Clear();
            processes.Clear();
            try
            {
                if (checkBox_remote.Checked && !import_remote_out()) return;
                buildcsv();
                scrolldown();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                return;
            }
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
            for (; i < param_list_size; ++i)
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

        private void reloadConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void button_export_source_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            // This may not work as it calls ssh which invokes openssh under system32, and something is blocking it. 
            // In the office computer I installed ssh / scp via cygwin and changed the path so it looks for it first. 
            // The Environment.ExpandEnvironmentVariables below is to allow, e.g., %USERNAME% in the path.
            p.StartInfo.FileName = Environment.ExpandEnvironmentVariables(ConfigurationManager.AppSettings["export_batch_file"]);
            p.Start();
        }

        public int Compare(List<string> x, List<string> y)
        {
            string x1 = get_field(x, header_fields.bench), x2 = get_field(y, header_fields.bench);
            if (x1 == x2)
            {
                return 0;
            }
            return x1.CompareTo(x2);
        }


        private void markwinner_Click(object sender, EventArgs e)
        {
            string fileName = csv.Text;

            int cnt = 0;
            const string title = "winner";
            List<List<string>> table = getDataFromFile();
            List<string> header = table[0];
            if (get_field(header, header_fields.param) != "param")
            {
                listBox1.Items.Add("No header line, Aborting.");
                return;
            }

            int idx = get_field_idx(header, title);
            if (idx >= 0)
            {
                listBox1.Items.Add("A 'winners' column already exist. Removing it...");
                table = remove_field(table, idx);
                header = table[0];
            }
            table.RemoveAt(0);
            table.Sort(Compare);

            try
            {
                string prev = "";
                float min = 10E10f;
                HashSet<int> winners = new HashSet<int>();
                int winner = 0;
                int timeidx = get_field_idx(header, time_Tag);
                foreach (var row in table)
                {
                    string bench = get_field(row, header_fields.bench);
                    if (bench != prev)
                    {
                        min = 10E10f;
                        prev = bench;
                        if (cnt > 0) winners.Add(winner);
                    }
                    float time;
                    float.TryParse(get_field(row, timeidx), out time);
                    if (time < min)
                    {
                        min = time;
                        winner = cnt;
                    }
                    cnt++;
                }
                winners.Add(winner);
                header.Add(title);
                labels.Add(title);
                for (int i = 0; i < table.Count; ++i) {
                    if (winners.Contains(i)) table[i].Add("1");
                    else table[i].Add("0");
                }
                table.Insert(0, header);
                Write(table);
                
                listBox1.Items.Add("Marked " + winners.Count + " winners");
                listBox1.Refresh();
                scrolldown();
            }
            catch { MessageBox.Show("seems that " + csv.Text + "is in use"); return; }
        }

        private void button_putty_Click(object sender, EventArgs e)
        {
            string putty = ConfigurationManager.AppSettings["putty_command"];
            string user = ConfigurationManager.AppSettings["remote_user"];
            string domain = ConfigurationManager.AppSettings["remote_domain"];
            run_remote(putty, user + "@" + domain + " -pw 4545Nkho!!", false);
        }

        private void clearParamDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string userInput = Interaction.InputBox(
            "Clear data for param: ",   // Prompt
            "Input Required",            // Title
            ""              // Default text
            );
            if (userInput != "")
            {
                // Pattern to match (e.g. all .log files)
                string pattern = "*" + normalize_string(userInput) + ".out";

                try
                {
                    // Get all files matching the pattern
                    string[] files = Directory.GetFiles(benchmarksDir, pattern);

                    foreach (string file in files)
                    {
                        File.Delete(file);
                    }
                    listBox1.Items.Add("Deleted " + files.Length + " files.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
                List<List<string>> outlines = new List<List<string>>();

                List<List<string>> table = getDataFromFile();
                foreach (List<string> row in table)
                {
                    if (!row.Contains(userInput)) {
                        outlines.Add(row);
                    }
                }
                Write(outlines);
                listBox1.Items.Add(outlines.Count + " records left");
            }
        }

        private void openOutFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string userInput = Interaction.InputBox(
            "Line number in the CSV: ",   // Prompt
            "Input Required",            // Title
            ""              // Default text
            );
            if (userInput != "")
            {
                // get the line from the csv file: 
                List<string> row;
                try
                {
                    int lineNumber = int.Parse(userInput);                    
                    row = getDataFromFile()[lineNumber - 1];
                }
                catch
                {
                    Interaction.MsgBox("Wrong line number");
                    return;
                }
                string dir = get_field(row, header_fields.dir);
                string bench = get_field(row, header_fields.bench);
                string param = get_field(row, header_fields.param);
                string outfilename = Path.Combine(dir, bench + "." + normalize_string(param.Replace("P:","")) + ".out");
                Process p = new Process();
                p.StartInfo.FileName = "notepad";
                p.StartInfo.Arguments = outfilename;
                p.Start();
            }
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
        public Dictionary<string, float> res;

        public benchmark(string param, string name, Dictionary<string, float> res)
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

