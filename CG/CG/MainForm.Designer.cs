namespace CG
{
    partial class mainForm
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(mainForm));
            this.scene = new System.Windows.Forms.PictureBox();
            this.UpMovebutton = new System.Windows.Forms.Button();
            this.DownMovebutton = new System.Windows.Forms.Button();
            this.LeftMovebutton = new System.Windows.Forms.Button();
            this.RightMovebutton = new System.Windows.Forms.Button();
            this.rotXtrackBar = new System.Windows.Forms.TrackBar();
            this.rotYtrackBar = new System.Windows.Forms.TrackBar();
            this.rotZtrackBar = new System.Windows.Forms.TrackBar();
            this.xLighttextBox = new System.Windows.Forms.TextBox();
            this.zLighttextBox = new System.Windows.Forms.TextBox();
            this.yLighttextBox = new System.Windows.Forms.TextBox();
            this.changeLightbutton = new System.Windows.Forms.Button();
            this.stepLabel = new System.Windows.Forms.Label();
            this.perlinPictureBox = new System.Windows.Forms.PictureBox();
            this.Menu = new System.Windows.Forms.MenuStrip();
            this.загрузитьКартуВысотToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.загрузитьКартуВысотToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.шумПерлинаToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.холмовойАлгоритмToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.оПрограммеToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label1 = new System.Windows.Forms.Label();
            this.sizeHillsTextBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.countHillsTextBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.valleyCheckBox = new System.Windows.Forms.CheckBox();
            this.smoothCheckBox = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.button5 = new System.Windows.Forms.Button();
            this.button6 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.scene)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotXtrackBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotYtrackBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotZtrackBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.perlinPictureBox)).BeginInit();
            this.Menu.SuspendLayout();
            this.SuspendLayout();
            // 
            // scene
            // 
            this.scene.BackColor = System.Drawing.SystemColors.WindowText;
            this.scene.Location = new System.Drawing.Point(12, 38);
            this.scene.Name = "scene";
            this.scene.Size = new System.Drawing.Size(1000, 1000);
            this.scene.TabIndex = 0;
            this.scene.TabStop = false;
            this.scene.DoubleClick += new System.EventHandler(this.scene_DoubleClick);
            // 
            // UpMovebutton
            // 
            this.UpMovebutton.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.UpMovebutton.Location = new System.Drawing.Point(1691, 210);
            this.UpMovebutton.Name = "UpMovebutton";
            this.UpMovebutton.Size = new System.Drawing.Size(50, 112);
            this.UpMovebutton.TabIndex = 10;
            this.UpMovebutton.Text = "↑";
            this.UpMovebutton.UseVisualStyleBackColor = true;
            this.UpMovebutton.Click += new System.EventHandler(this.UpMovebutton_Click);
            // 
            // DownMovebutton
            // 
            this.DownMovebutton.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.DownMovebutton.Location = new System.Drawing.Point(1691, 371);
            this.DownMovebutton.Name = "DownMovebutton";
            this.DownMovebutton.Size = new System.Drawing.Size(50, 112);
            this.DownMovebutton.TabIndex = 11;
            this.DownMovebutton.Text = "↓";
            this.DownMovebutton.UseVisualStyleBackColor = true;
            this.DownMovebutton.Click += new System.EventHandler(this.DownMovebutton_Click);
            // 
            // LeftMovebutton
            // 
            this.LeftMovebutton.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.LeftMovebutton.Location = new System.Drawing.Point(1573, 321);
            this.LeftMovebutton.Name = "LeftMovebutton";
            this.LeftMovebutton.Size = new System.Drawing.Size(112, 50);
            this.LeftMovebutton.TabIndex = 12;
            this.LeftMovebutton.Text = "←";
            this.LeftMovebutton.UseVisualStyleBackColor = true;
            this.LeftMovebutton.Click += new System.EventHandler(this.LeftMovebutton_Click);
            // 
            // RightMovebutton
            // 
            this.RightMovebutton.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.RightMovebutton.Location = new System.Drawing.Point(1747, 321);
            this.RightMovebutton.Name = "RightMovebutton";
            this.RightMovebutton.Size = new System.Drawing.Size(112, 50);
            this.RightMovebutton.TabIndex = 13;
            this.RightMovebutton.Text = "→";
            this.RightMovebutton.UseVisualStyleBackColor = true;
            this.RightMovebutton.Click += new System.EventHandler(this.RightMovebutton_Click);
            // 
            // rotXtrackBar
            // 
            this.rotXtrackBar.BackColor = System.Drawing.Color.White;
            this.rotXtrackBar.Location = new System.Drawing.Point(1456, 590);
            this.rotXtrackBar.Maximum = 360;
            this.rotXtrackBar.Minimum = -360;
            this.rotXtrackBar.Name = "rotXtrackBar";
            this.rotXtrackBar.Size = new System.Drawing.Size(345, 69);
            this.rotXtrackBar.TabIndex = 14;
            this.rotXtrackBar.Scroll += new System.EventHandler(this.rotXtrackBar_Scroll);
            // 
            // rotYtrackBar
            // 
            this.rotYtrackBar.BackColor = System.Drawing.Color.White;
            this.rotYtrackBar.Location = new System.Drawing.Point(1456, 665);
            this.rotYtrackBar.Maximum = 360;
            this.rotYtrackBar.Minimum = -360;
            this.rotYtrackBar.Name = "rotYtrackBar";
            this.rotYtrackBar.Size = new System.Drawing.Size(345, 69);
            this.rotYtrackBar.TabIndex = 15;
            this.rotYtrackBar.Scroll += new System.EventHandler(this.rotYtrackBar_Scroll);
            // 
            // rotZtrackBar
            // 
            this.rotZtrackBar.BackColor = System.Drawing.Color.White;
            this.rotZtrackBar.Location = new System.Drawing.Point(1456, 740);
            this.rotZtrackBar.Maximum = 360;
            this.rotZtrackBar.Minimum = -360;
            this.rotZtrackBar.Name = "rotZtrackBar";
            this.rotZtrackBar.Size = new System.Drawing.Size(345, 69);
            this.rotZtrackBar.TabIndex = 16;
            this.rotZtrackBar.Scroll += new System.EventHandler(this.rotZtrackBar_Scroll);
            // 
            // xLighttextBox
            // 
            this.xLighttextBox.Location = new System.Drawing.Point(1613, 70);
            this.xLighttextBox.Name = "xLighttextBox";
            this.xLighttextBox.Size = new System.Drawing.Size(36, 26);
            this.xLighttextBox.TabIndex = 17;
            // 
            // zLighttextBox
            // 
            this.zLighttextBox.Location = new System.Drawing.Point(1727, 70);
            this.zLighttextBox.Name = "zLighttextBox";
            this.zLighttextBox.Size = new System.Drawing.Size(36, 26);
            this.zLighttextBox.TabIndex = 18;
            // 
            // yLighttextBox
            // 
            this.yLighttextBox.Location = new System.Drawing.Point(1669, 70);
            this.yLighttextBox.Name = "yLighttextBox";
            this.yLighttextBox.Size = new System.Drawing.Size(36, 26);
            this.yLighttextBox.TabIndex = 19;
            // 
            // changeLightbutton
            // 
            this.changeLightbutton.Location = new System.Drawing.Point(1639, 102);
            this.changeLightbutton.Name = "changeLightbutton";
            this.changeLightbutton.Size = new System.Drawing.Size(102, 39);
            this.changeLightbutton.TabIndex = 20;
            this.changeLightbutton.Text = "Обновить";
            this.changeLightbutton.UseVisualStyleBackColor = true;
            this.changeLightbutton.Click += new System.EventHandler(this.changeLightbutton_Click);
            // 
            // stepLabel
            // 
            this.stepLabel.AutoSize = true;
            this.stepLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.stepLabel.Location = new System.Drawing.Point(1562, 38);
            this.stepLabel.Name = "stepLabel";
            this.stepLabel.Size = new System.Drawing.Size(277, 25);
            this.stepLabel.TabIndex = 22;
            this.stepLabel.Text = "Направление вектора света:";
            // 
            // perlinPictureBox
            // 
            this.perlinPictureBox.BackColor = System.Drawing.SystemColors.Window;
            this.perlinPictureBox.Location = new System.Drawing.Point(1042, 38);
            this.perlinPictureBox.Name = "perlinPictureBox";
            this.perlinPictureBox.Size = new System.Drawing.Size(500, 500);
            this.perlinPictureBox.TabIndex = 24;
            this.perlinPictureBox.TabStop = false;
            // 
            // Menu
            // 
            this.Menu.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.Menu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.Menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.загрузитьКартуВысотToolStripMenuItem,
            this.оПрограммеToolStripMenuItem});
            this.Menu.Location = new System.Drawing.Point(0, 0);
            this.Menu.Name = "Menu";
            this.Menu.Size = new System.Drawing.Size(1924, 33);
            this.Menu.TabIndex = 25;
            this.Menu.Text = "Генерация";
            // 
            // загрузитьКартуВысотToolStripMenuItem
            // 
            this.загрузитьКартуВысотToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.загрузитьКартуВысотToolStripMenuItem1,
            this.шумПерлинаToolStripMenuItem,
            this.холмовойАлгоритмToolStripMenuItem});
            this.загрузитьКартуВысотToolStripMenuItem.Name = "загрузитьКартуВысотToolStripMenuItem";
            this.загрузитьКартуВысотToolStripMenuItem.Size = new System.Drawing.Size(208, 29);
            this.загрузитьКартуВысотToolStripMenuItem.Text = "Генерация ландшафта";
            // 
            // загрузитьКартуВысотToolStripMenuItem1
            // 
            this.загрузитьКартуВысотToolStripMenuItem1.Name = "загрузитьКартуВысотToolStripMenuItem1";
            this.загрузитьКартуВысотToolStripMenuItem1.Size = new System.Drawing.Size(298, 34);
            this.загрузитьКартуВысотToolStripMenuItem1.Text = "Загрузить карту высот";
            this.загрузитьКартуВысотToolStripMenuItem1.Click += new System.EventHandler(this.загрузитьКартуВысотToolStripMenuItem1_Click);
            // 
            // шумПерлинаToolStripMenuItem
            // 
            this.шумПерлинаToolStripMenuItem.Name = "шумПерлинаToolStripMenuItem";
            this.шумПерлинаToolStripMenuItem.Size = new System.Drawing.Size(298, 34);
            this.шумПерлинаToolStripMenuItem.Text = "Шум Перлина";
            this.шумПерлинаToolStripMenuItem.Click += new System.EventHandler(this.шумПерлинаToolStripMenuItem_Click);
            // 
            // холмовойАлгоритмToolStripMenuItem
            // 
            this.холмовойАлгоритмToolStripMenuItem.Name = "холмовойАлгоритмToolStripMenuItem";
            this.холмовойАлгоритмToolStripMenuItem.Size = new System.Drawing.Size(298, 34);
            this.холмовойАлгоритмToolStripMenuItem.Text = "Холмовой алгоритм";
            this.холмовойАлгоритмToolStripMenuItem.Click += new System.EventHandler(this.холмовойАлгоритмToolStripMenuItem_Click);
            // 
            // оПрограммеToolStripMenuItem
            // 
            this.оПрограммеToolStripMenuItem.Name = "оПрограммеToolStripMenuItem";
            this.оПрограммеToolStripMenuItem.Size = new System.Drawing.Size(141, 29);
            this.оПрограммеToolStripMenuItem.Text = "О программе";
            this.оПрограммеToolStripMenuItem.Click += new System.EventHandler(this.оПрограммеToolStripMenuItem_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(1052, 625);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(115, 20);
            this.label1.TabIndex = 27;
            this.label1.Text = "Размер карты";
            // 
            // sizeHillsTextBox
            // 
            this.sizeHillsTextBox.Location = new System.Drawing.Point(1056, 657);
            this.sizeHillsTextBox.Name = "sizeHillsTextBox";
            this.sizeHillsTextBox.Size = new System.Drawing.Size(100, 26);
            this.sizeHillsTextBox.TabIndex = 26;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(1052, 703);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(120, 20);
            this.label2.TabIndex = 29;
            this.label2.Text = "Кол-во холмов";
            // 
            // countHillsTextBox
            // 
            this.countHillsTextBox.Location = new System.Drawing.Point(1056, 735);
            this.countHillsTextBox.Name = "countHillsTextBox";
            this.countHillsTextBox.Size = new System.Drawing.Size(100, 26);
            this.countHillsTextBox.TabIndex = 28;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label3.Location = new System.Drawing.Point(1056, 576);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(278, 25);
            this.label3.TabIndex = 30;
            this.label3.Text = "Для Холмового Алгоритма:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label4.Location = new System.Drawing.Point(1452, 553);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(209, 25);
            this.label4.TabIndex = 31;
            this.label4.Text = "Поворот ландшафта";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label5.Location = new System.Drawing.Point(1399, 760);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(30, 25);
            this.label5.TabIndex = 32;
            this.label5.Text = "Z:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label6.Location = new System.Drawing.Point(1399, 682);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(31, 25);
            this.label6.TabIndex = 33;
            this.label6.Text = "Y:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label7.Location = new System.Drawing.Point(1399, 607);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(32, 25);
            this.label7.TabIndex = 34;
            this.label7.Text = "X:";
            // 
            // valleyCheckBox
            // 
            this.valleyCheckBox.AutoSize = true;
            this.valleyCheckBox.Location = new System.Drawing.Point(1192, 651);
            this.valleyCheckBox.Name = "valleyCheckBox";
            this.valleyCheckBox.Size = new System.Drawing.Size(93, 24);
            this.valleyCheckBox.TabIndex = 35;
            this.valleyCheckBox.Text = "Долина";
            this.valleyCheckBox.UseVisualStyleBackColor = true;
            // 
            // smoothCheckBox
            // 
            this.smoothCheckBox.AutoSize = true;
            this.smoothCheckBox.Location = new System.Drawing.Point(1192, 696);
            this.smoothCheckBox.Name = "smoothCheckBox";
            this.smoothCheckBox.Size = new System.Drawing.Size(110, 24);
            this.smoothCheckBox.TabIndex = 36;
            this.smoothCheckBox.Text = "Сгладить";
            this.smoothCheckBox.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.button1.Location = new System.Drawing.Point(1808, 590);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(51, 69);
            this.button1.TabIndex = 37;
            this.button1.Text = "90";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.button2.Location = new System.Drawing.Point(1865, 590);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(51, 69);
            this.button2.TabIndex = 38;
            this.button2.Text = "-90";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(1807, 665);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(52, 69);
            this.button3.TabIndex = 40;
            this.button3.Text = "90";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(1865, 665);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(51, 69);
            this.button4.TabIndex = 39;
            this.button4.Text = "-90";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(1864, 741);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(52, 68);
            this.button5.TabIndex = 42;
            this.button5.Text = "-90";
            this.button5.UseVisualStyleBackColor = true;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // button6
            // 
            this.button6.Location = new System.Drawing.Point(1807, 740);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(52, 69);
            this.button6.TabIndex = 41;
            this.button6.Text = "90";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.button6_Click);
            // 
            // mainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1924, 1050);
            this.Controls.Add(this.button5);
            this.Controls.Add(this.button6);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.smoothCheckBox);
            this.Controls.Add(this.valleyCheckBox);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.countHillsTextBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.sizeHillsTextBox);
            this.Controls.Add(this.perlinPictureBox);
            this.Controls.Add(this.stepLabel);
            this.Controls.Add(this.changeLightbutton);
            this.Controls.Add(this.yLighttextBox);
            this.Controls.Add(this.zLighttextBox);
            this.Controls.Add(this.xLighttextBox);
            this.Controls.Add(this.rotZtrackBar);
            this.Controls.Add(this.rotYtrackBar);
            this.Controls.Add(this.rotXtrackBar);
            this.Controls.Add(this.RightMovebutton);
            this.Controls.Add(this.LeftMovebutton);
            this.Controls.Add(this.DownMovebutton);
            this.Controls.Add(this.UpMovebutton);
            this.Controls.Add(this.scene);
            this.Controls.Add(this.Menu);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.Menu;
            this.Name = "mainForm";
            this.Text = "Ландшафт 3D";
            this.Load += new System.EventHandler(this.mainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.scene)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotXtrackBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotYtrackBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rotZtrackBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.perlinPictureBox)).EndInit();
            this.Menu.ResumeLayout(false);
            this.Menu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox scene;
        private System.Windows.Forms.Button UpMovebutton;
        private System.Windows.Forms.Button DownMovebutton;
        private System.Windows.Forms.Button LeftMovebutton;
        private System.Windows.Forms.Button RightMovebutton;
        private System.Windows.Forms.TrackBar rotXtrackBar;
        private System.Windows.Forms.TrackBar rotYtrackBar;
        private System.Windows.Forms.TrackBar rotZtrackBar;
        private System.Windows.Forms.TextBox xLighttextBox;
        private System.Windows.Forms.TextBox zLighttextBox;
        private System.Windows.Forms.TextBox yLighttextBox;
        private System.Windows.Forms.Button changeLightbutton;
        private System.Windows.Forms.Label stepLabel;
        private System.Windows.Forms.PictureBox perlinPictureBox;
        private System.Windows.Forms.MenuStrip Menu;
        private System.Windows.Forms.ToolStripMenuItem загрузитьКартуВысотToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem загрузитьКартуВысотToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem шумПерлинаToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem холмовойАлгоритмToolStripMenuItem;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox sizeHillsTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox countHillsTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ToolStripMenuItem оПрограммеToolStripMenuItem;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox valleyCheckBox;
        private System.Windows.Forms.CheckBox smoothCheckBox;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button5;
        private System.Windows.Forms.Button button6;
    }
}

