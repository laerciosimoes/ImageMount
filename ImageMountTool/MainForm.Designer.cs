namespace ImageMountTool
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblDeviceList = new System.Windows.Forms.Label();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnRescanBus = new System.Windows.Forms.Button();
            this.btnShowOpened = new System.Windows.Forms.Button();
            this.btnRemoveSelected = new System.Windows.Forms.Button();
            this.btnRemoveAll = new System.Windows.Forms.Button();
            this.btnRAMDisk = new System.Windows.Forms.Button();
            this.btnMountRaw = new System.Windows.Forms.Button();
            this.btnMountMultiPartRaw = new System.Windows.Forms.Button();
            this.btnMountDiscUtils = new System.Windows.Forms.Button();
           this.DiskStateViewBindingSource = new System.Windows.Forms.BindingSource(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DiskStateViewBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // lblDeviceList
            // 
            this.lblDeviceList.AutoSize = true;
            this.lblDeviceList.Location = new System.Drawing.Point(38, 16);
            this.lblDeviceList.Name = "lblDeviceList";
            this.lblDeviceList.Size = new System.Drawing.Size(95, 25);
            this.lblDeviceList.TabIndex = 0;
            this.lblDeviceList.Text = "Deviec List";
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(41, 55);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 62;
            this.dataGridView1.RowTemplate.Height = 33;
            this.dataGridView1.Size = new System.Drawing.Size(1182, 225);
            this.dataGridView1.TabIndex = 1;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(44, 307);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(1179, 34);
            this.btnRefresh.TabIndex = 2;
            this.btnRefresh.Text = "Refresh List";
            this.btnRefresh.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // btnRescanBus
            // 
            this.btnRescanBus.Location = new System.Drawing.Point(41, 358);
            this.btnRescanBus.Name = "btnRescanBus";
            this.btnRescanBus.Size = new System.Drawing.Size(1179, 34);
            this.btnRescanBus.TabIndex = 3;
            this.btnRescanBus.Text = "Rescan SCSI bus";
            this.btnRescanBus.UseVisualStyleBackColor = true;
            this.btnRescanBus.Click += new System.EventHandler(this.btnRescanBus_Click_1);
            // 
            // btnShowOpened
            // 
            this.btnShowOpened.Location = new System.Drawing.Point(41, 410);
            this.btnShowOpened.Name = "btnShowOpened";
            this.btnShowOpened.Size = new System.Drawing.Size(1179, 34);
            this.btnShowOpened.TabIndex = 4;
            this.btnShowOpened.Text = "Show using applications";
            this.btnShowOpened.UseVisualStyleBackColor = true;
            this.btnShowOpened.Click += new System.EventHandler(this.btnShowOpened_Click_1);
            // 
            // btnRemoveSelected
            // 
            this.btnRemoveSelected.Location = new System.Drawing.Point(44, 464);
            this.btnRemoveSelected.Name = "btnRemoveSelected";
            this.btnRemoveSelected.Size = new System.Drawing.Size(1179, 34);
            this.btnRemoveSelected.TabIndex = 5;
            this.btnRemoveSelected.Text = "Remove selected";
            this.btnRemoveSelected.UseVisualStyleBackColor = true;
            this.btnRemoveSelected.Click += new System.EventHandler(this.btnRemoveSelected_Click_1);
            // 
            // btnRemoveAll
            // 
            this.btnRemoveAll.Location = new System.Drawing.Point(41, 516);
            this.btnRemoveAll.Name = "btnRemoveAll";
            this.btnRemoveAll.Size = new System.Drawing.Size(1179, 34);
            this.btnRemoveAll.TabIndex = 6;
            this.btnRemoveAll.Text = "Remove all";
            this.btnRemoveAll.UseVisualStyleBackColor = true;
            this.btnRemoveAll.Click += new System.EventHandler(this.btnRemoveAll_Click);
            // 
            // btnRAMDisk
            // 
            this.btnRAMDisk.Location = new System.Drawing.Point(41, 565);
            this.btnRAMDisk.Name = "btnRAMDisk";
            this.btnRAMDisk.Size = new System.Drawing.Size(1179, 34);
            this.btnRAMDisk.TabIndex = 7;
            this.btnRAMDisk.Text = "Create RAM disk";
            this.btnRAMDisk.UseVisualStyleBackColor = true;
            this.btnRAMDisk.Click += new System.EventHandler(this.btnRAMDisk_Click);
            // 
            // btnMountRaw
            // 
            this.btnMountRaw.Location = new System.Drawing.Point(44, 624);
            this.btnMountRaw.Name = "btnMountRaw";
            this.btnMountRaw.Size = new System.Drawing.Size(1179, 34);
            this.btnMountRaw.TabIndex = 8;
            this.btnMountRaw.Text = "Mount raw image";
            this.btnMountRaw.UseVisualStyleBackColor = true;
            this.btnMountRaw.Click += new System.EventHandler(this.btnMountRaw_Click);
            // 
            // btnMountMultiPartRaw
            // 
            this.btnMountMultiPartRaw.Location = new System.Drawing.Point(41, 676);
            this.btnMountMultiPartRaw.Name = "btnMountMultiPartRaw";
            this.btnMountMultiPartRaw.Size = new System.Drawing.Size(1179, 34);
            this.btnMountMultiPartRaw.TabIndex = 9;
            this.btnMountMultiPartRaw.Text = "Mount multi-part raw";
            this.btnMountMultiPartRaw.UseVisualStyleBackColor = true;
            this.btnMountMultiPartRaw.Click += new System.EventHandler(this.btnMountMultiPartRaw_Click);
            // 
            // btnMountDiscUtils
            // 
            this.btnMountDiscUtils.Location = new System.Drawing.Point(41, 727);
            this.btnMountDiscUtils.Name = "btnMountDiscUtils";
            this.btnMountDiscUtils.Size = new System.Drawing.Size(1179, 34);
            this.btnMountDiscUtils.TabIndex = 10;
            this.btnMountDiscUtils.Text = "Mount through DiscUtils";
            this.btnMountDiscUtils.UseVisualStyleBackColor = true;
            this.btnMountDiscUtils.Click += new System.EventHandler(this.btnMountDiscUtils_Click);
             // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1285, 884);
            this.Controls.Add(this.btnMountDiscUtils);
            this.Controls.Add(this.btnMountMultiPartRaw);
            this.Controls.Add(this.btnMountRaw);
            this.Controls.Add(this.btnRAMDisk);
            this.Controls.Add(this.btnRemoveAll);
            this.Controls.Add(this.btnRemoveSelected);
            this.Controls.Add(this.btnShowOpened);
            this.Controls.Add(this.btnRescanBus);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.lblDeviceList);
            this.Name = "MainForm";
            this.Text = "Image Mount";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load_1);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DiskStateViewBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Label lblDeviceList;
        private DataGridView dataGridView1;
        private Button btnRefresh;
        private Button btnRescanBus;
        private Button btnShowOpened;
        private Button btnRemoveSelected;
        private Button btnRemoveAll;
        private Button btnRAMDisk;
        private Button btnMountRaw;
        private Button btnMountMultiPartRaw;
        private Button btnMountDiscUtils;
        private BindingSource DiskStateViewBindingSource;
    }
}