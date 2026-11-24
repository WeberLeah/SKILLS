namespace SmartPower_Cs
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.button_Device_Nums = new System.Windows.Forms.Button();
            this.button_Output_Update = new System.Windows.Forms.Button();
            this.button_Output_Update_Ext = new System.Windows.Forms.Button();
            this.button_Output_On = new System.Windows.Forms.Button();
            this.button_Output_On_Ext = new System.Windows.Forms.Button();
            this.button_Device_Close = new System.Windows.Forms.Button();
            this.button_Output_Off = new System.Windows.Forms.Button();
            this.button_Get_Status_Ext = new System.Windows.Forms.Button();
            this.button_Get_Status = new System.Windows.Forms.Button();
            this.numericUpDown_Device_Open_Index = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown_SetVoltage = new System.Windows.Forms.NumericUpDown();
            this.richTextBox_Log = new System.Windows.Forms.RichTextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.numericUpDown_SetCurrent = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.button_Device_Open_Ext = new System.Windows.Forms.Button();
            this.button_Device_Open = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.button_Device_Nums_Ext = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_Device_Open_Index)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_SetVoltage)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_SetCurrent)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // button_Device_Nums
            // 
            this.button_Device_Nums.Location = new System.Drawing.Point(12, 12);
            this.button_Device_Nums.Name = "button_Device_Nums";
            this.button_Device_Nums.Size = new System.Drawing.Size(200, 23);
            this.button_Device_Nums.TabIndex = 0;
            this.button_Device_Nums.Text = "Device_Nums";
            this.button_Device_Nums.UseVisualStyleBackColor = true;
            this.button_Device_Nums.Click += new System.EventHandler(this.button_Device_Nums_Click);
            // 
            // button_Output_Update
            // 
            this.button_Output_Update.Location = new System.Drawing.Point(12, 217);
            this.button_Output_Update.Name = "button_Output_Update";
            this.button_Output_Update.Size = new System.Drawing.Size(200, 23);
            this.button_Output_Update.TabIndex = 1;
            this.button_Output_Update.Text = "Output_Update";
            this.button_Output_Update.UseVisualStyleBackColor = true;
            this.button_Output_Update.Click += new System.EventHandler(this.button_Output_Update_Click);
            // 
            // button_Output_Update_Ext
            // 
            this.button_Output_Update_Ext.Location = new System.Drawing.Point(12, 246);
            this.button_Output_Update_Ext.Name = "button_Output_Update_Ext";
            this.button_Output_Update_Ext.Size = new System.Drawing.Size(200, 23);
            this.button_Output_Update_Ext.TabIndex = 2;
            this.button_Output_Update_Ext.Text = "Output_Update_Ext";
            this.button_Output_Update_Ext.UseVisualStyleBackColor = true;
            this.button_Output_Update_Ext.Click += new System.EventHandler(this.button_Output_Update_Ext_Click);
            // 
            // button_Output_On
            // 
            this.button_Output_On.Location = new System.Drawing.Point(12, 159);
            this.button_Output_On.Name = "button_Output_On";
            this.button_Output_On.Size = new System.Drawing.Size(200, 23);
            this.button_Output_On.TabIndex = 3;
            this.button_Output_On.Text = "Output_On";
            this.button_Output_On.UseVisualStyleBackColor = true;
            this.button_Output_On.Click += new System.EventHandler(this.button_Output_On_Click);
            // 
            // button_Output_On_Ext
            // 
            this.button_Output_On_Ext.Location = new System.Drawing.Point(12, 188);
            this.button_Output_On_Ext.Name = "button_Output_On_Ext";
            this.button_Output_On_Ext.Size = new System.Drawing.Size(200, 23);
            this.button_Output_On_Ext.TabIndex = 4;
            this.button_Output_On_Ext.Text = "Output_On_Ext";
            this.button_Output_On_Ext.UseVisualStyleBackColor = true;
            this.button_Output_On_Ext.Click += new System.EventHandler(this.button_Output_On_Ext_Click);
            // 
            // button_Device_Close
            // 
            this.button_Device_Close.Location = new System.Drawing.Point(12, 452);
            this.button_Device_Close.Name = "button_Device_Close";
            this.button_Device_Close.Size = new System.Drawing.Size(200, 23);
            this.button_Device_Close.TabIndex = 5;
            this.button_Device_Close.Text = "Device_Close";
            this.button_Device_Close.UseVisualStyleBackColor = true;
            this.button_Device_Close.Click += new System.EventHandler(this.button_Device_Close_Click);
            // 
            // button_Output_Off
            // 
            this.button_Output_Off.Location = new System.Drawing.Point(12, 391);
            this.button_Output_Off.Name = "button_Output_Off";
            this.button_Output_Off.Size = new System.Drawing.Size(200, 23);
            this.button_Output_Off.TabIndex = 9;
            this.button_Output_Off.Text = "Output_Off";
            this.button_Output_Off.UseVisualStyleBackColor = true;
            this.button_Output_Off.Click += new System.EventHandler(this.button_Output_Off_Click);
            // 
            // button_Get_Status_Ext
            // 
            this.button_Get_Status_Ext.Location = new System.Drawing.Point(12, 328);
            this.button_Get_Status_Ext.Name = "button_Get_Status_Ext";
            this.button_Get_Status_Ext.Size = new System.Drawing.Size(200, 23);
            this.button_Get_Status_Ext.TabIndex = 8;
            this.button_Get_Status_Ext.Text = "Get_Status_Ext";
            this.button_Get_Status_Ext.UseVisualStyleBackColor = true;
            this.button_Get_Status_Ext.Click += new System.EventHandler(this.button_Get_Status_Ext_Click);
            // 
            // button_Get_Status
            // 
            this.button_Get_Status.Location = new System.Drawing.Point(12, 299);
            this.button_Get_Status.Name = "button_Get_Status";
            this.button_Get_Status.Size = new System.Drawing.Size(200, 23);
            this.button_Get_Status.TabIndex = 7;
            this.button_Get_Status.Text = "Get_Status";
            this.button_Get_Status.UseVisualStyleBackColor = true;
            this.button_Get_Status.Click += new System.EventHandler(this.button_Get_Status_Click);
            // 
            // numericUpDown_Device_Open_Index
            // 
            this.numericUpDown_Device_Open_Index.Location = new System.Drawing.Point(16, 20);
            this.numericUpDown_Device_Open_Index.Name = "numericUpDown_Device_Open_Index";
            this.numericUpDown_Device_Open_Index.Size = new System.Drawing.Size(59, 21);
            this.numericUpDown_Device_Open_Index.TabIndex = 10;
            // 
            // numericUpDown_SetVoltage
            // 
            this.numericUpDown_SetVoltage.DecimalPlaces = 3;
            this.numericUpDown_SetVoltage.Location = new System.Drawing.Point(12, 37);
            this.numericUpDown_SetVoltage.Maximum = new decimal(new int[] {
            126,
            0,
            0,
            65536});
            this.numericUpDown_SetVoltage.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericUpDown_SetVoltage.Name = "numericUpDown_SetVoltage";
            this.numericUpDown_SetVoltage.Size = new System.Drawing.Size(81, 21);
            this.numericUpDown_SetVoltage.TabIndex = 11;
            this.numericUpDown_SetVoltage.Value = new decimal(new int[] {
            38,
            0,
            0,
            65536});
            // 
            // richTextBox_Log
            // 
            this.richTextBox_Log.Location = new System.Drawing.Point(351, 32);
            this.richTextBox_Log.Name = "richTextBox_Log";
            this.richTextBox_Log.Size = new System.Drawing.Size(295, 423);
            this.richTextBox_Log.TabIndex = 13;
            this.richTextBox_Log.Text = "     性能媲美N6705C\nnA级电流测量精度,电流分辨率高达0.2nA\n超低噪声设计,底电流 <10nA (4.2V的典型值)\n1kHz~32KHz可调采样" +
    "率\n\n  www.smartxbw.com\n\n~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 12);
            this.label1.TabIndex = 14;
            this.label1.Text = "Set voltage(V)";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 66);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(89, 12);
            this.label3.TabIndex = 16;
            this.label3.Text = "Set current(A)";
            // 
            // numericUpDown_SetCurrent
            // 
            this.numericUpDown_SetCurrent.DecimalPlaces = 3;
            this.numericUpDown_SetCurrent.Location = new System.Drawing.Point(12, 83);
            this.numericUpDown_SetCurrent.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            131072});
            this.numericUpDown_SetCurrent.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            this.numericUpDown_SetCurrent.Name = "numericUpDown_SetCurrent";
            this.numericUpDown_SetCurrent.Size = new System.Drawing.Size(81, 21);
            this.numericUpDown_SetCurrent.TabIndex = 17;
            this.numericUpDown_SetCurrent.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(357, 12);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(29, 12);
            this.label2.TabIndex = 18;
            this.label2.Text = "Log:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.numericUpDown_SetVoltage);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.numericUpDown_SetCurrent);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(222, 159);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(123, 110);
            this.groupBox1.TabIndex = 19;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Set Output :";
            // 
            // button_Device_Open_Ext
            // 
            this.button_Device_Open_Ext.Location = new System.Drawing.Point(12, 111);
            this.button_Device_Open_Ext.Name = "button_Device_Open_Ext";
            this.button_Device_Open_Ext.Size = new System.Drawing.Size(200, 23);
            this.button_Device_Open_Ext.TabIndex = 20;
            this.button_Device_Open_Ext.Text = "Device_Open_Ext";
            this.button_Device_Open_Ext.UseVisualStyleBackColor = true;
            this.button_Device_Open_Ext.Click += new System.EventHandler(this.button_Device_Open_Ext_Click);
            // 
            // button_Device_Open
            // 
            this.button_Device_Open.Location = new System.Drawing.Point(12, 82);
            this.button_Device_Open.Name = "button_Device_Open";
            this.button_Device_Open.Size = new System.Drawing.Size(200, 23);
            this.button_Device_Open.TabIndex = 21;
            this.button_Device_Open.Text = "Device_Open";
            this.button_Device_Open.UseVisualStyleBackColor = true;
            this.button_Device_Open.Click += new System.EventHandler(this.button_Device_Open_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.numericUpDown_Device_Open_Index);
            this.groupBox2.Location = new System.Drawing.Point(222, 82);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(123, 52);
            this.groupBox2.TabIndex = 22;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Device Index :";
            // 
            // button_Device_Nums_Ext
            // 
            this.button_Device_Nums_Ext.Location = new System.Drawing.Point(12, 41);
            this.button_Device_Nums_Ext.Name = "button_Device_Nums_Ext";
            this.button_Device_Nums_Ext.Size = new System.Drawing.Size(200, 23);
            this.button_Device_Nums_Ext.TabIndex = 23;
            this.button_Device_Nums_Ext.Text = "Device_Nums_Ext";
            this.button_Device_Nums_Ext.UseVisualStyleBackColor = true;
            this.button_Device_Nums_Ext.Click += new System.EventHandler(this.button_Device_Nums_Ext_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(658, 499);
            this.Controls.Add(this.button_Device_Nums_Ext);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.button_Device_Open);
            this.Controls.Add(this.button_Device_Open_Ext);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.richTextBox_Log);
            this.Controls.Add(this.button_Output_Off);
            this.Controls.Add(this.button_Get_Status_Ext);
            this.Controls.Add(this.button_Get_Status);
            this.Controls.Add(this.button_Device_Close);
            this.Controls.Add(this.button_Output_On_Ext);
            this.Controls.Add(this.button_Output_On);
            this.Controls.Add(this.button_Output_Update_Ext);
            this.Controls.Add(this.button_Output_Update);
            this.Controls.Add(this.button_Device_Nums);
            this.Name = "Form1";
            this.Text = "Smart power monitor_SDK Demo ( www.smartxbw.com ) V1.3";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_Device_Open_Index)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_SetVoltage)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_SetCurrent)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button_Device_Nums;
        private System.Windows.Forms.Button button_Output_Update;
        private System.Windows.Forms.Button button_Output_Update_Ext;
        private System.Windows.Forms.Button button_Output_On;
        private System.Windows.Forms.Button button_Output_On_Ext;
        private System.Windows.Forms.Button button_Device_Close;
        private System.Windows.Forms.Button button_Output_Off;
        private System.Windows.Forms.Button button_Get_Status_Ext;
        private System.Windows.Forms.Button button_Get_Status;
        private System.Windows.Forms.NumericUpDown numericUpDown_Device_Open_Index;
        private System.Windows.Forms.NumericUpDown numericUpDown_SetVoltage;
        private System.Windows.Forms.RichTextBox richTextBox_Log;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown numericUpDown_SetCurrent;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button button_Device_Open_Ext;
        private System.Windows.Forms.Button button_Device_Open;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button button_Device_Nums_Ext;
    }
}

