﻿namespace bench
{
    partial class filter
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.button1 = new System.Windows.Forms.Button();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label_paralel_time = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label_cnt = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label_fails = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.button_kill = new System.Windows.Forms.Button();
            this.button_opencsv = new System.Windows.Forms.Button();
            this.checkedListBox_cores = new System.Windows.Forms.CheckedListBox();
            this.label13 = new System.Windows.Forms.Label();
            this.checkBox_rec = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.checkBox_filter_csv = new System.Windows.Forms.CheckBox();
            this.button_scatter = new System.Windows.Forms.Button();
            this.button_cactus = new System.Windows.Forms.Button();
            this.checkBox_remote = new System.Windows.Forms.CheckBox();
            this.button_import = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.checkBox_filter_out = new System.Windows.Forms.CheckBox();
            this.checkBox_rerun_empty_out = new System.Windows.Forms.CheckBox();
            this.checkBox_skip_long_runs = new System.Windows.Forms.CheckBox();
            this.checkBox_copy = new System.Windows.Forms.CheckBox();
            this.label10 = new System.Windows.Forms.Label();
            this.button4 = new System.Windows.Forms.Button();
            this.exe = new System.Windows.Forms.ComboBox();
            this.dir = new System.Windows.Forms.ComboBox();
            this.filter_str = new System.Windows.Forms.ComboBox();
            this.csv = new System.Windows.Forms.ComboBox();
            this.param_groups = new System.Windows.Forms.ComboBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.cleanupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cleanupToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteShortsFromCsvToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteAllfailBenchmarksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editHistoryFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refreshMenusToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.configToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label7 = new System.Windows.Forms.Label();
            this.stat_field = new System.Windows.Forms.ComboBox();
            this.timeout = new System.Windows.Forms.ComboBox();
            this.min_mem = new System.Windows.Forms.ComboBox();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.SystemColors.Control;
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.button1.Location = new System.Drawing.Point(453, 579);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(86, 26);
            this.button1.TabIndex = 1;
            this.button1.Text = "start";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button_start_Click);
            // 
            // listBox1
            // 
            this.listBox1.FormattingEnabled = true;
            this.listBox1.HorizontalScrollbar = true;
            this.listBox1.Location = new System.Drawing.Point(4, 26);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(535, 173);
            this.listBox1.TabIndex = 2;
            this.listBox1.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 490);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "parallel Time:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 341);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(44, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "params:";
            // 
            // label_paralel_time
            // 
            this.label_paralel_time.AutoSize = true;
            this.label_paralel_time.Location = new System.Drawing.Point(92, 491);
            this.label_paralel_time.Name = "label_paralel_time";
            this.label_paralel_time.Size = new System.Drawing.Size(16, 13);
            this.label_paralel_time.TabIndex = 6;
            this.label_paralel_time.Text = "...";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(8, 261);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(26, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "filter";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(5, 234);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "directory";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(8, 208);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(24, 13);
            this.label6.TabIndex = 12;
            this.label6.Text = "exe";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(276, 490);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 13);
            this.label3.TabIndex = 15;
            this.label3.Text = "# bench.:";
            // 
            // label_cnt
            // 
            this.label_cnt.AutoSize = true;
            this.label_cnt.Location = new System.Drawing.Point(330, 491);
            this.label_cnt.Name = "label_cnt";
            this.label_cnt.Size = new System.Drawing.Size(16, 13);
            this.label_cnt.TabIndex = 16;
            this.label_cnt.Text = "...";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(10, 316);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(70, 13);
            this.label8.TabIndex = 19;
            this.label8.Text = "time-out (sec)";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(195, 316);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(111, 13);
            this.label9.TabIndex = 20;
            this.label9.Text = "min-mem to start (MB):";
            // 
            // label_fails
            // 
            this.label_fails.AutoSize = true;
            this.label_fails.Location = new System.Drawing.Point(420, 491);
            this.label_fails.Name = "label_fails";
            this.label_fails.Size = new System.Drawing.Size(16, 13);
            this.label_fails.TabIndex = 22;
            this.label_fails.Text = "...";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(380, 491);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(38, 13);
            this.label12.TabIndex = 23;
            this.label12.Text = "# fails:";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(13, 520);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(43, 13);
            this.label11.TabIndex = 25;
            this.label11.Text = "csv file:";
            // 
            // button_kill
            // 
            this.button_kill.Location = new System.Drawing.Point(453, 550);
            this.button_kill.Name = "button_kill";
            this.button_kill.Size = new System.Drawing.Size(86, 26);
            this.button_kill.TabIndex = 26;
            this.button_kill.Text = "kill-all";
            this.button_kill.UseVisualStyleBackColor = true;
            this.button_kill.Click += new System.EventHandler(this.button_kill_Click);
            // 
            // button_opencsv
            // 
            this.button_opencsv.Location = new System.Drawing.Point(276, 517);
            this.button_opencsv.Name = "button_opencsv";
            this.button_opencsv.Size = new System.Drawing.Size(70, 23);
            this.button_opencsv.TabIndex = 27;
            this.button_opencsv.Text = "open csv";
            this.button_opencsv.UseVisualStyleBackColor = true;
            this.button_opencsv.Click += new System.EventHandler(this.button_csv_Click);
            // 
            // checkedListBox_cores
            // 
            this.checkedListBox_cores.CheckOnClick = true;
            this.checkedListBox_cores.FormattingEnabled = true;
            this.checkedListBox_cores.Location = new System.Drawing.Point(443, 258);
            this.checkedListBox_cores.Name = "checkedListBox_cores";
            this.checkedListBox_cores.Size = new System.Drawing.Size(72, 79);
            this.checkedListBox_cores.TabIndex = 29;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(399, 261);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(37, 13);
            this.label13.TabIndex = 30;
            this.label13.Text = "Cores:";
            // 
            // checkBox_rec
            // 
            this.checkBox_rec.AutoSize = true;
            this.checkBox_rec.Location = new System.Drawing.Point(493, 234);
            this.checkBox_rec.Name = "checkBox_rec";
            this.checkBox_rec.Size = new System.Drawing.Size(41, 17);
            this.checkBox_rec.TabIndex = 31;
            this.checkBox_rec.Text = "rec";
            this.checkBox_rec.UseVisualStyleBackColor = true;
            this.checkBox_rec.Leave += new System.EventHandler(this.checkBox_CheckedChanged);
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Location = new System.Drawing.Point(15, 363);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(522, 123);
            this.panel1.TabIndex = 32;
            // 
            // checkBox_filter_csv
            // 
            this.checkBox_filter_csv.AutoSize = true;
            this.checkBox_filter_csv.Checked = true;
            this.checkBox_filter_csv.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_filter_csv.Location = new System.Drawing.Point(137, 289);
            this.checkBox_filter_csv.Name = "checkBox_filter_csv";
            this.checkBox_filter_csv.Size = new System.Drawing.Size(59, 17);
            this.checkBox_filter_csv.TabIndex = 33;
            this.checkBox_filter_csv.Text = "csv file";
            this.toolTip1.SetToolTip(this.checkBox_filter_csv, "Excludes benchmarks for which there is an entry in the csv file");
            this.checkBox_filter_csv.UseVisualStyleBackColor = true;
            this.checkBox_filter_csv.Leave += new System.EventHandler(this.checkBox_CheckedChanged);
            // 
            // button_scatter
            // 
            this.button_scatter.Location = new System.Drawing.Point(15, 559);
            this.button_scatter.Name = "button_scatter";
            this.button_scatter.Size = new System.Drawing.Size(75, 23);
            this.button_scatter.TabIndex = 34;
            this.button_scatter.Text = "scatter";
            this.button_scatter.UseVisualStyleBackColor = true;
            this.button_scatter.Click += new System.EventHandler(this.button_scatter_Click);
            // 
            // button_cactus
            // 
            this.button_cactus.Location = new System.Drawing.Point(96, 559);
            this.button_cactus.Name = "button_cactus";
            this.button_cactus.Size = new System.Drawing.Size(75, 23);
            this.button_cactus.TabIndex = 35;
            this.button_cactus.Text = "cactus";
            this.button_cactus.UseVisualStyleBackColor = true;
            this.button_cactus.Click += new System.EventHandler(this.button_cactus_Click);
            // 
            // checkBox_remote
            // 
            this.checkBox_remote.AutoSize = true;
            this.checkBox_remote.Location = new System.Drawing.Point(348, 559);
            this.checkBox_remote.Name = "checkBox_remote";
            this.checkBox_remote.Size = new System.Drawing.Size(58, 17);
            this.checkBox_remote.TabIndex = 37;
            this.checkBox_remote.Text = "remote";
            this.toolTip1.SetToolTip(this.checkBox_remote, "Run on a remote machine.");
            this.checkBox_remote.UseVisualStyleBackColor = true;
            this.checkBox_remote.CheckedChanged += new System.EventHandler(this.checkBox_remote_CheckedChanged);
            // 
            // button_import
            // 
            this.button_import.Location = new System.Drawing.Point(352, 517);
            this.button_import.Name = "button_import";
            this.button_import.Size = new System.Drawing.Size(70, 23);
            this.button_import.TabIndex = 38;
            this.button_import.Text = "import";
            this.toolTip1.SetToolTip(this.button_import, "from .out files to csv file + plot files.  out files for remote are in the releas" +
        "e/ directory. ");
            this.button_import.UseVisualStyleBackColor = true;
            this.button_import.Click += new System.EventHandler(this.button_import_Click);
            // 
            // checkBox_filter_out
            // 
            this.checkBox_filter_out.AutoSize = true;
            this.checkBox_filter_out.Location = new System.Drawing.Point(202, 289);
            this.checkBox_filter_out.Name = "checkBox_filter_out";
            this.checkBox_filter_out.Size = new System.Drawing.Size(62, 17);
            this.checkBox_filter_out.TabIndex = 41;
            this.checkBox_filter_out.Text = "out files";
            this.toolTip1.SetToolTip(this.checkBox_filter_out, "Excludes benchmarks for which there is already an out file");
            this.checkBox_filter_out.UseVisualStyleBackColor = true;
            this.checkBox_filter_out.CheckedChanged += new System.EventHandler(this.checkBox_out_CheckedChanged);
            // 
            // checkBox_rerun_empty_out
            // 
            this.checkBox_rerun_empty_out.AutoSize = true;
            this.checkBox_rerun_empty_out.Location = new System.Drawing.Point(276, 289);
            this.checkBox_rerun_empty_out.Name = "checkBox_rerun_empty_out";
            this.checkBox_rerun_empty_out.Size = new System.Drawing.Size(99, 17);
            this.checkBox_rerun_empty_out.TabIndex = 43;
            this.checkBox_rerun_empty_out.Text = "rerun empty out";
            this.toolTip1.SetToolTip(this.checkBox_rerun_empty_out, "ignores empty out files");
            this.checkBox_rerun_empty_out.UseVisualStyleBackColor = true;
            this.checkBox_rerun_empty_out.Leave += new System.EventHandler(this.checkBox_CheckedChanged);
            // 
            // checkBox_skip_long_runs
            // 
            this.checkBox_skip_long_runs.AutoSize = true;
            this.checkBox_skip_long_runs.Checked = true;
            this.checkBox_skip_long_runs.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_skip_long_runs.Location = new System.Drawing.Point(276, 261);
            this.checkBox_skip_long_runs.Name = "checkBox_skip_long_runs";
            this.checkBox_skip_long_runs.Size = new System.Drawing.Size(93, 17);
            this.checkBox_skip_long_runs.TabIndex = 44;
            this.checkBox_skip_long_runs.Text = "Skip long runs";
            this.toolTip1.SetToolTip(this.checkBox_skip_long_runs, "Once a benchmark fails with some config. it will not be attempted again");
            this.checkBox_skip_long_runs.UseVisualStyleBackColor = true;
            this.checkBox_skip_long_runs.Leave += new System.EventHandler(this.checkBox_CheckedChanged);
            // 
            // checkBox_copy
            // 
            this.checkBox_copy.AutoSize = true;
            this.checkBox_copy.Location = new System.Drawing.Point(348, 582);
            this.checkBox_copy.Name = "checkBox_copy";
            this.checkBox_copy.Size = new System.Drawing.Size(96, 17);
            this.checkBox_copy.TabIndex = 58;
            this.checkBox_copy.Text = "copy to remote";
            this.toolTip1.SetToolTip(this.checkBox_copy, "Copy benchmark to remote machine. Uncheck if the benchmarks are already there. ");
            this.checkBox_copy.UseVisualStyleBackColor = true;
            this.checkBox_copy.Leave += new System.EventHandler(this.checkBox_CheckedChanged);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(54, 291);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(77, 13);
            this.label10.TabIndex = 42;
            this.label10.Text = "Filter entries in:";
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(443, 232);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(32, 21);
            this.button4.TabIndex = 46;
            this.button4.Text = "...";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // exe
            // 
            this.exe.FormattingEnabled = true;
            this.exe.Location = new System.Drawing.Point(57, 205);
            this.exe.Name = "exe";
            this.exe.Size = new System.Drawing.Size(482, 21);
            this.exe.TabIndex = 48;
            this.exe.SelectionChangeCommitted += new System.EventHandler(this.combo_SelectedIndexChanged);
            this.exe.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // dir
            // 
            this.dir.FormattingEnabled = true;
            this.dir.Location = new System.Drawing.Point(57, 233);
            this.dir.Name = "dir";
            this.dir.Size = new System.Drawing.Size(379, 21);
            this.dir.TabIndex = 49;
            this.dir.SelectionChangeCommitted += new System.EventHandler(this.combo_SelectedIndexChanged);
            this.dir.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // filter_str
            // 
            this.filter_str.FormattingEnabled = true;
            this.filter_str.Location = new System.Drawing.Point(58, 259);
            this.filter_str.Name = "filter_str";
            this.filter_str.Size = new System.Drawing.Size(207, 21);
            this.filter_str.TabIndex = 50;
            this.filter_str.SelectionChangeCommitted += new System.EventHandler(this.combo_SelectedIndexChanged);
            this.filter_str.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // csv
            // 
            this.csv.FormattingEnabled = true;
            this.csv.Location = new System.Drawing.Point(65, 517);
            this.csv.Name = "csv";
            this.csv.Size = new System.Drawing.Size(205, 21);
            this.csv.TabIndex = 51;
            this.csv.SelectionChangeCommitted += new System.EventHandler(this.combo_SelectedIndexChanged);
            this.csv.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // param_groups
            // 
            this.param_groups.FormattingEnabled = true;
            this.param_groups.Location = new System.Drawing.Point(57, 339);
            this.param_groups.Name = "param_groups";
            this.param_groups.Size = new System.Drawing.Size(482, 21);
            this.param_groups.TabIndex = 52;
            this.param_groups.SelectedIndexChanged += new System.EventHandler(this.param_groups_SelectedIndexChanged);
            this.param_groups.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cleanupToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(590, 24);
            this.menuStrip1.TabIndex = 53;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // cleanupToolStripMenuItem
            // 
            this.cleanupToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cleanupToolStripMenuItem1,
            this.editHistoryFileToolStripMenuItem,
            this.configToolStripMenuItem});
            this.cleanupToolStripMenuItem.Name = "cleanupToolStripMenuItem";
            this.cleanupToolStripMenuItem.Size = new System.Drawing.Size(50, 20);
            this.cleanupToolStripMenuItem.Text = "Menu";
            // 
            // cleanupToolStripMenuItem1
            // 
            this.cleanupToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1,
            this.deleteShortsFromCsvToolStripMenuItem,
            this.toolStripSeparator1,
            this.deleteAllfailBenchmarksToolStripMenuItem});
            this.cleanupToolStripMenuItem1.Name = "cleanupToolStripMenuItem1";
            this.cleanupToolStripMenuItem1.Size = new System.Drawing.Size(169, 22);
            this.cleanupToolStripMenuItem1.Text = "Cleanup";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(211, 22);
            this.toolStripMenuItem1.Text = "Delete fails from csv";
            this.toolStripMenuItem1.ToolTipText = "Delete lines from the csv file, that correspond to a benchmark that was not solve" +
    "d by ALL parameters.";
            this.toolStripMenuItem1.Click += new System.EventHandler(this.button_del_fails_Click);
            // 
            // deleteShortsFromCsvToolStripMenuItem
            // 
            this.deleteShortsFromCsvToolStripMenuItem.Name = "deleteShortsFromCsvToolStripMenuItem";
            this.deleteShortsFromCsvToolStripMenuItem.Size = new System.Drawing.Size(211, 22);
            this.deleteShortsFromCsvToolStripMenuItem.Text = "Delete shorts from csv";
            this.deleteShortsFromCsvToolStripMenuItem.ToolTipText = "Delete from csv benchmarks that their runtime is < 1 sec. in at least one of the " +
    "parameters";
            this.deleteShortsFromCsvToolStripMenuItem.Click += new System.EventHandler(this.button_del_shorts_click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(208, 6);
            // 
            // deleteAllfailBenchmarksToolStripMenuItem
            // 
            this.deleteAllfailBenchmarksToolStripMenuItem.Name = "deleteAllfailBenchmarksToolStripMenuItem";
            this.deleteAllfailBenchmarksToolStripMenuItem.Size = new System.Drawing.Size(211, 22);
            this.deleteAllfailBenchmarksToolStripMenuItem.Text = "Delete all-fail benchmarks";
            this.deleteAllfailBenchmarksToolStripMenuItem.ToolTipText = "delete *benchmark files* for which all params fails according to the data in the " +
    "csv files";
            this.deleteAllfailBenchmarksToolStripMenuItem.Click += new System.EventHandler(this.button_del_allfail_Click);
            // 
            // editHistoryFileToolStripMenuItem
            // 
            this.editHistoryFileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editToolStripMenuItem,
            this.refreshMenusToolStripMenuItem});
            this.editHistoryFileToolStripMenuItem.Name = "editHistoryFileToolStripMenuItem";
            this.editHistoryFileToolStripMenuItem.Size = new System.Drawing.Size(169, 22);
            this.editHistoryFileToolStripMenuItem.Text = "History file";
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.editToolStripMenuItem.Text = "Edit";
            this.editToolStripMenuItem.Click += new System.EventHandler(this.editHistoryFileToolStripMenuItem_Click);
            // 
            // refreshMenusToolStripMenuItem
            // 
            this.refreshMenusToolStripMenuItem.Name = "refreshMenusToolStripMenuItem";
            this.refreshMenusToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.refreshMenusToolStripMenuItem.Text = "Refresh menus";
            this.refreshMenusToolStripMenuItem.Click += new System.EventHandler(this.refreshMenusToolStripMenuItem_Click);
            // 
            // configToolStripMenuItem
            // 
            this.configToolStripMenuItem.Name = "configToolStripMenuItem";
            this.configToolStripMenuItem.Size = new System.Drawing.Size(169, 22);
            this.configToolStripMenuItem.Text = "Advanced Config.";
            this.configToolStripMenuItem.ToolTipText = "Requires restart after change.";
            this.configToolStripMenuItem.Click += new System.EventHandler(this.configToolStripMenuItem_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(11, 592);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(84, 13);
            this.label7.TabIndex = 54;
            this.label7.Text = "Do stat. for field:";
            // 
            // stat_field
            // 
            this.stat_field.FormattingEnabled = true;
            this.stat_field.Location = new System.Drawing.Point(95, 589);
            this.stat_field.Name = "stat_field";
            this.stat_field.Size = new System.Drawing.Size(143, 21);
            this.stat_field.TabIndex = 55;
            this.stat_field.SelectionChangeCommitted += new System.EventHandler(this.combo_SelectedIndexChanged);
            this.stat_field.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // timeout
            // 
            this.timeout.FormattingEnabled = true;
            this.timeout.Location = new System.Drawing.Point(87, 312);
            this.timeout.Name = "timeout";
            this.timeout.Size = new System.Drawing.Size(97, 21);
            this.timeout.TabIndex = 56;
            this.timeout.SelectionChangeCommitted += new System.EventHandler(this.combo_SelectedIndexChanged);
            this.timeout.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // min_mem
            // 
            this.min_mem.FormattingEnabled = true;
            this.min_mem.Location = new System.Drawing.Point(307, 312);
            this.min_mem.Name = "min_mem";
            this.min_mem.Size = new System.Drawing.Size(122, 21);
            this.min_mem.TabIndex = 57;
            this.min_mem.SelectionChangeCommitted += new System.EventHandler(this.combo_SelectedIndexChanged);
            this.min_mem.Leave += new System.EventHandler(this.comboBox_Leave);
            // 
            // filter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(590, 622);
            this.Controls.Add(this.checkBox_copy);
            this.Controls.Add(this.min_mem);
            this.Controls.Add(this.timeout);
            this.Controls.Add(this.stat_field);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.param_groups);
            this.Controls.Add(this.csv);
            this.Controls.Add(this.filter_str);
            this.Controls.Add(this.dir);
            this.Controls.Add(this.exe);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.checkBox_skip_long_runs);
            this.Controls.Add(this.checkBox_rerun_empty_out);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.checkBox_filter_out);
            this.Controls.Add(this.button_import);
            this.Controls.Add(this.checkBox_remote);
            this.Controls.Add(this.button_cactus);
            this.Controls.Add(this.button_scatter);
            this.Controls.Add(this.checkBox_filter_csv);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.checkBox_rec);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.checkedListBox_cores);
            this.Controls.Add(this.button_opencsv);
            this.Controls.Add(this.button_kill);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label_fails);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label_cnt);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label_paralel_time);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "filter";
            this.Text = "HBench";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label_paralel_time;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label_cnt;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label_fails;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Button button_kill;
        private System.Windows.Forms.Button button_opencsv;
        private System.Windows.Forms.CheckedListBox checkedListBox_cores;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.CheckBox checkBox_rec;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckBox checkBox_filter_csv;
        private System.Windows.Forms.Button button_scatter;
        private System.Windows.Forms.Button button_cactus;
        private System.Windows.Forms.CheckBox checkBox_remote;
        private System.Windows.Forms.Button button_import;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox checkBox_filter_out;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.CheckBox checkBox_rerun_empty_out;
        private System.Windows.Forms.CheckBox checkBox_skip_long_runs;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.ComboBox exe;
        private System.Windows.Forms.ComboBox dir;
        private System.Windows.Forms.ComboBox filter_str;
        private System.Windows.Forms.ComboBox csv;
        private System.Windows.Forms.ComboBox param_groups;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem cleanupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cleanupToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem deleteShortsFromCsvToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteAllfailBenchmarksToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editHistoryFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refreshMenusToolStripMenuItem;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox stat_field;
        private System.Windows.Forms.ToolStripMenuItem configToolStripMenuItem;
        private System.Windows.Forms.ComboBox timeout;
        private System.Windows.Forms.ComboBox min_mem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.CheckBox checkBox_copy;
    }
}

