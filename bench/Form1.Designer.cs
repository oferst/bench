namespace WindowsFormsApplication1
{
    partial class Form1
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
            this.button1 = new System.Windows.Forms.Button();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label_paralel_time = new System.Windows.Forms.Label();
            this.text_filter = new System.Windows.Forms.TextBox();
            this.text_dir = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.text_exe = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label_total_time = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label_cnt = new System.Windows.Forms.Label();
            this.bg = new System.ComponentModel.BackgroundWorker();
            this.text_timeout = new System.Windows.Forms.TextBox();
            this.text_minmem = new System.Windows.Forms.TextBox();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label_fails = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.text_csv = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(464, 516);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "start";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // listBox1
            // 
            this.listBox1.FormattingEnabled = true;
            this.listBox1.HorizontalScrollbar = true;
            this.listBox1.Location = new System.Drawing.Point(4, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(535, 225);
            this.listBox1.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 493);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "parallel Time:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 347);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(44, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "params:";
            // 
            // label_paralel_time
            // 
            this.label_paralel_time.AutoSize = true;
            this.label_paralel_time.Location = new System.Drawing.Point(92, 494);
            this.label_paralel_time.Name = "label_paralel_time";
            this.label_paralel_time.Size = new System.Drawing.Size(16, 13);
            this.label_paralel_time.TabIndex = 6;
            this.label_paralel_time.Text = "...";
            // 
            // text_filter
            // 
            this.text_filter.Location = new System.Drawing.Point(60, 289);
            this.text_filter.Name = "text_filter";
            this.text_filter.Size = new System.Drawing.Size(233, 20);
            this.text_filter.TabIndex = 7;
            // 
            // text_dir
            // 
            this.text_dir.Location = new System.Drawing.Point(60, 263);
            this.text_dir.Name = "text_dir";
            this.text_dir.Size = new System.Drawing.Size(479, 20);
            this.text_dir.TabIndex = 8;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(11, 292);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(26, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "filter";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(8, 265);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "directory";
            // 
            // text_exe
            // 
            this.text_exe.Location = new System.Drawing.Point(60, 237);
            this.text_exe.Name = "text_exe";
            this.text_exe.Size = new System.Drawing.Size(479, 20);
            this.text_exe.TabIndex = 11;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(11, 239);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(24, 13);
            this.label6.TabIndex = 12;
            this.label6.Text = "exe";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(163, 492);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(34, 13);
            this.label7.TabIndex = 13;
            this.label7.Text = "Total:";
            // 
            // label_total_time
            // 
            this.label_total_time.AutoSize = true;
            this.label_total_time.Location = new System.Drawing.Point(211, 492);
            this.label_total_time.Name = "label_total_time";
            this.label_total_time.Size = new System.Drawing.Size(16, 13);
            this.label_total_time.TabIndex = 14;
            this.label_total_time.Text = "...";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(303, 493);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 13);
            this.label3.TabIndex = 15;
            this.label3.Text = "# bench.:";
            // 
            // label_cnt
            // 
            this.label_cnt.AutoSize = true;
            this.label_cnt.Location = new System.Drawing.Point(357, 494);
            this.label_cnt.Name = "label_cnt";
            this.label_cnt.Size = new System.Drawing.Size(16, 13);
            this.label_cnt.TabIndex = 16;
            this.label_cnt.Text = "...";
            // 
            // bg
            // 
            this.bg.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.bg.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);
            // 
            // text_timeout
            // 
            this.text_timeout.Location = new System.Drawing.Point(60, 316);
            this.text_timeout.Name = "text_timeout";
            this.text_timeout.Size = new System.Drawing.Size(100, 20);
            this.text_timeout.TabIndex = 17;
            // 
            // text_minmem
            // 
            this.text_minmem.Location = new System.Drawing.Point(245, 315);
            this.text_minmem.Name = "text_minmem";
            this.text_minmem.Size = new System.Drawing.Size(100, 20);
            this.text_minmem.TabIndex = 18;
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(439, 314);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(100, 20);
            this.textBox3.TabIndex = 18;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(2, 318);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(55, 13);
            this.label8.TabIndex = 19;
            this.label8.Text = "Tout (sec)";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(166, 319);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(73, 13);
            this.label9.TabIndex = 20;
            this.label9.Text = "min-mem (MB)";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(360, 316);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(73, 13);
            this.label10.TabIndex = 21;
            this.label10.Text = "max-mem(MB)";
            // 
            // label_fails
            // 
            this.label_fails.AutoSize = true;
            this.label_fails.Location = new System.Drawing.Point(439, 494);
            this.label_fails.Name = "label_fails";
            this.label_fails.Size = new System.Drawing.Size(16, 13);
            this.label_fails.TabIndex = 22;
            this.label_fails.Text = "...";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(399, 494);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(38, 13);
            this.label12.TabIndex = 23;
            this.label12.Text = "# fails:";
            // 
            // text_csv
            // 
            this.text_csv.Location = new System.Drawing.Point(352, 288);
            this.text_csv.Name = "text_csv";
            this.text_csv.Size = new System.Drawing.Size(184, 20);
            this.text_csv.TabIndex = 24;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(303, 294);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(43, 13);
            this.label11.TabIndex = 25;
            this.label11.Text = "csv file:";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(362, 516);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 26;
            this.button2.Text = "kill-all";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(556, 551);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.text_csv);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label_fails);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.textBox3);
            this.Controls.Add(this.text_minmem);
            this.Controls.Add(this.text_timeout);
            this.Controls.Add(this.label_cnt);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label_total_time);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.text_exe);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.text_dir);
            this.Controls.Add(this.text_filter);
            this.Controls.Add(this.label_paralel_time);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label_paralel_time;
        private System.Windows.Forms.TextBox text_filter;
        private System.Windows.Forms.TextBox text_dir;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox text_exe;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label_total_time;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label_cnt;
        private System.ComponentModel.BackgroundWorker bg;
        private System.Windows.Forms.TextBox text_timeout;
        private System.Windows.Forms.TextBox text_minmem;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label_fails;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox text_csv;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Button button2;
    }
}

