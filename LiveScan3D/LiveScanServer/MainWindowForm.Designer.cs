﻿namespace KinectServer
{
    partial class MainWindowForm
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
            this.btCalibrate = new System.Windows.Forms.Button();
            this.btRecord = new System.Windows.Forms.Button();
            this.lClientListBox = new System.Windows.Forms.ListBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.recordingWorker = new System.ComponentModel.BackgroundWorker();
            this.txtSeqName = new System.Windows.Forms.TextBox();
            this.btRefineCalib = new System.Windows.Forms.Button();
            this.OpenGLWorker = new System.ComponentModel.BackgroundWorker();
            this.savingWorker = new System.ComponentModel.BackgroundWorker();
            this.updateWorker = new System.ComponentModel.BackgroundWorker();
            this.btShowLive = new System.Windows.Forms.Button();
            this.btSettings = new System.Windows.Forms.Button();
            this.refineWorker = new System.ComponentModel.BackgroundWorker();
            this.lbSeqName = new System.Windows.Forms.Label();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btCalibrate
            // 
            this.btCalibrate.Location = new System.Drawing.Point(13, 83);
            this.btCalibrate.Margin = new System.Windows.Forms.Padding(4);
            this.btCalibrate.Name = "btCalibrate";
            this.btCalibrate.Size = new System.Drawing.Size(127, 28);
            this.btCalibrate.TabIndex = 2;
            this.btCalibrate.Text = "Calibrate";
            this.btCalibrate.UseVisualStyleBackColor = true;
            this.btCalibrate.Click += new System.EventHandler(this.btCalibrate_Click);
            // 
            // btRecord
            // 
            this.btRecord.Location = new System.Drawing.Point(340, 119);
            this.btRecord.Margin = new System.Windows.Forms.Padding(4);
            this.btRecord.Name = "btRecord";
            this.btRecord.Size = new System.Drawing.Size(127, 28);
            this.btRecord.TabIndex = 4;
            this.btRecord.Text = "Start recording";
            this.btRecord.UseVisualStyleBackColor = true;
            this.btRecord.Click += new System.EventHandler(this.btRecord_Click);
            // 
            // lClientListBox
            // 
            this.lClientListBox.FormattingEnabled = true;
            this.lClientListBox.HorizontalScrollbar = true;
            this.lClientListBox.ItemHeight = 16;
            this.lClientListBox.Location = new System.Drawing.Point(13, 13);
            this.lClientListBox.Margin = new System.Windows.Forms.Padding(4);
            this.lClientListBox.Name = "lClientListBox";
            this.lClientListBox.Size = new System.Drawing.Size(307, 52);
            this.lClientListBox.TabIndex = 5;
            this.lClientListBox.SelectedIndexChanged += new System.EventHandler(this.lClientListBox_SelectedIndexChanged);
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 171);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 19, 0);
            this.statusStrip1.Size = new System.Drawing.Size(483, 22);
            this.statusStrip1.TabIndex = 6;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(0, 16);
            // 
            // recordingWorker
            // 
            this.recordingWorker.WorkerSupportsCancellation = true;
            this.recordingWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.recordingWorker_DoWork);
            this.recordingWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.recordingWorker_RunWorkerCompleted);
            // 
            // txtSeqName
            // 
            this.txtSeqName.Location = new System.Drawing.Point(343, 89);
            this.txtSeqName.Margin = new System.Windows.Forms.Padding(4);
            this.txtSeqName.MaxLength = 40;
            this.txtSeqName.Name = "txtSeqName";
            this.txtSeqName.Size = new System.Drawing.Size(123, 22);
            this.txtSeqName.TabIndex = 7;
            this.txtSeqName.Text = "noname";
            // 
            // btRefineCalib
            // 
            this.btRefineCalib.Location = new System.Drawing.Point(13, 119);
            this.btRefineCalib.Margin = new System.Windows.Forms.Padding(4);
            this.btRefineCalib.Name = "btRefineCalib";
            this.btRefineCalib.Size = new System.Drawing.Size(127, 28);
            this.btRefineCalib.TabIndex = 11;
            this.btRefineCalib.Text = "Refine calib";
            this.btRefineCalib.UseVisualStyleBackColor = true;
            this.btRefineCalib.Click += new System.EventHandler(this.btRefineCalib_Click);
            // 
            // OpenGLWorker
            // 
            this.OpenGLWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.OpenGLWorker_DoWork);
            this.OpenGLWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.OpenGLWorker_RunWorkerCompleted);
            // 
            // savingWorker
            // 
            this.savingWorker.WorkerSupportsCancellation = true;
            this.savingWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.savingWorker_DoWork);
            this.savingWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.savingWorker_RunWorkerCompleted);
            // 
            // updateWorker
            // 
            this.updateWorker.WorkerSupportsCancellation = true;
            this.updateWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.updateWorker_DoWork);
            // 
            // btShowLive
            // 
            this.btShowLive.Location = new System.Drawing.Point(157, 83);
            this.btShowLive.Margin = new System.Windows.Forms.Padding(4);
            this.btShowLive.Name = "btShowLive";
            this.btShowLive.Size = new System.Drawing.Size(163, 64);
            this.btShowLive.TabIndex = 12;
            this.btShowLive.Text = "Show live";
            this.btShowLive.UseVisualStyleBackColor = true;
            this.btShowLive.Click += new System.EventHandler(this.btShowLive_Click);
            // 
            // btSettings
            // 
            this.btSettings.Location = new System.Drawing.Point(340, 13);
            this.btSettings.Margin = new System.Windows.Forms.Padding(4);
            this.btSettings.Name = "btSettings";
            this.btSettings.Size = new System.Drawing.Size(127, 28);
            this.btSettings.TabIndex = 13;
            this.btSettings.Text = "Settings";
            this.btSettings.UseVisualStyleBackColor = true;
            this.btSettings.Click += new System.EventHandler(this.btSettings_Click);
            // 
            // refineWorker
            // 
            this.refineWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.refineWorker_DoWork);
            this.refineWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.refineWorker_RunWorkerCompleted);
            // 
            // lbSeqName
            // 
            this.lbSeqName.AutoSize = true;
            this.lbSeqName.Location = new System.Drawing.Point(349, 66);
            this.lbSeqName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lbSeqName.Name = "lbSeqName";
            this.lbSeqName.Size = new System.Drawing.Size(109, 16);
            this.lbSeqName.TabIndex = 14;
            this.lbSeqName.Text = "Sequence name:";
            this.lbSeqName.Click += new System.EventHandler(this.lbSeqName_Click);
            // 
            // MainWindowForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(483, 193);
            this.Controls.Add(this.lbSeqName);
            this.Controls.Add(this.btSettings);
            this.Controls.Add(this.btShowLive);
            this.Controls.Add(this.btRefineCalib);
            this.Controls.Add(this.txtSeqName);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.lClientListBox);
            this.Controls.Add(this.btRecord);
            this.Controls.Add(this.btCalibrate);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.Name = "MainWindowForm";
            this.Text = "LiveScanServer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button btCalibrate;
        private System.Windows.Forms.Button btRecord;
        private System.Windows.Forms.ListBox lClientListBox;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.ComponentModel.BackgroundWorker recordingWorker;
        private System.Windows.Forms.TextBox txtSeqName;
        private System.Windows.Forms.Button btRefineCalib;
        private System.ComponentModel.BackgroundWorker OpenGLWorker;
        private System.ComponentModel.BackgroundWorker savingWorker;
        private System.ComponentModel.BackgroundWorker updateWorker;
        private System.Windows.Forms.Button btShowLive;
        private System.Windows.Forms.Button btSettings;
        private System.ComponentModel.BackgroundWorker refineWorker;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Label lbSeqName;
    }
}

