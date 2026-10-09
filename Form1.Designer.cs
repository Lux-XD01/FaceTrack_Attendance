namespace Proyecto_ReconocimientoFacial_0._1
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.dgvAlumnos = new System.Windows.Forms.DataGridView();
            this.txtBuscar = new System.Windows.Forms.TextBox();
            this.btnRegistrar = new System.Windows.Forms.Button();
            this.btnBorrarTodo = new System.Windows.Forms.Button();
            this.btnGuardarEdiciones = new System.Windows.Forms.Button();
            this.btnEliminarSeleccionado = new System.Windows.Forms.Button();
            this.btnActualizarGrilla = new System.Windows.Forms.Button();
            this.lblIP = new System.Windows.Forms.Label();
            this.lblIndicadorConexion = new System.Windows.Forms.Label();
            this.lblEstadoConexion = new System.Windows.Forms.Label();
            this.panelTemperatura = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAlumnos)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(12, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(682, 414);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(12, 420);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox1.Size = new System.Drawing.Size(715, 55);
            this.textBox1.TabIndex = 1;
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(12, 481);
            this.textBox2.Multiline = true;
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox2.Size = new System.Drawing.Size(354, 159);
            this.textBox2.TabIndex = 2;
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(372, 481);
            this.textBox3.Multiline = true;
            this.textBox3.Name = "textBox3";
            this.textBox3.ReadOnly = true;
            this.textBox3.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox3.Size = new System.Drawing.Size(355, 159);
            this.textBox3.TabIndex = 3;
            // 
            // dgvAlumnos
            // 
            this.dgvAlumnos.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAlumnos.Cursor = System.Windows.Forms.Cursors.Hand;
            this.dgvAlumnos.Location = new System.Drawing.Point(700, 12);
            this.dgvAlumnos.Name = "dgvAlumnos";
            this.dgvAlumnos.RowHeadersWidth = 51;
            this.dgvAlumnos.RowTemplate.Height = 24;
            this.dgvAlumnos.Size = new System.Drawing.Size(835, 402);
            this.dgvAlumnos.TabIndex = 4;
            // 
            // txtBuscar
            // 
            this.txtBuscar.Location = new System.Drawing.Point(733, 420);
            this.txtBuscar.Multiline = true;
            this.txtBuscar.Name = "txtBuscar";
            this.txtBuscar.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtBuscar.Size = new System.Drawing.Size(541, 55);
            this.txtBuscar.TabIndex = 5;
            // 
            // btnRegistrar
            // 
            this.btnRegistrar.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.btnRegistrar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.btnRegistrar.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRegistrar.Location = new System.Drawing.Point(930, 481);
            this.btnRegistrar.Name = "btnRegistrar";
            this.btnRegistrar.Size = new System.Drawing.Size(134, 34);
            this.btnRegistrar.TabIndex = 7;
            this.btnRegistrar.Text = "Registrar";
            this.btnRegistrar.UseVisualStyleBackColor = false;
            this.btnRegistrar.Click += new System.EventHandler(this.btnRegistrar_Click);
            // 
            // btnBorrarTodo
            // 
            this.btnBorrarTodo.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.btnBorrarTodo.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.btnBorrarTodo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBorrarTodo.Location = new System.Drawing.Point(1113, 481);
            this.btnBorrarTodo.Name = "btnBorrarTodo";
            this.btnBorrarTodo.Size = new System.Drawing.Size(134, 34);
            this.btnBorrarTodo.TabIndex = 8;
            this.btnBorrarTodo.Text = "Borrar Todo";
            this.btnBorrarTodo.UseVisualStyleBackColor = false;
            this.btnBorrarTodo.Click += new System.EventHandler(this.btnBorrarTodo_Click);
            // 
            // btnGuardarEdiciones
            // 
            this.btnGuardarEdiciones.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.btnGuardarEdiciones.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.btnGuardarEdiciones.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnGuardarEdiciones.Location = new System.Drawing.Point(733, 547);
            this.btnGuardarEdiciones.Name = "btnGuardarEdiciones";
            this.btnGuardarEdiciones.Size = new System.Drawing.Size(134, 34);
            this.btnGuardarEdiciones.TabIndex = 9;
            this.btnGuardarEdiciones.Text = "Guardar Ediciones";
            this.btnGuardarEdiciones.UseVisualStyleBackColor = false;
            this.btnGuardarEdiciones.Click += new System.EventHandler(this.btnGuardarEdiciones_Click);
            // 
            // btnEliminarSeleccionado
            // 
            this.btnEliminarSeleccionado.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.btnEliminarSeleccionado.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.btnEliminarSeleccionado.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnEliminarSeleccionado.Location = new System.Drawing.Point(930, 547);
            this.btnEliminarSeleccionado.Name = "btnEliminarSeleccionado";
            this.btnEliminarSeleccionado.Size = new System.Drawing.Size(134, 34);
            this.btnEliminarSeleccionado.TabIndex = 10;
            this.btnEliminarSeleccionado.Text = "Eliminar";
            this.btnEliminarSeleccionado.UseVisualStyleBackColor = false;
            this.btnEliminarSeleccionado.Click += new System.EventHandler(this.btnEliminarSeleccionado_Click);
            // 
            // btnActualizarGrilla
            // 
            this.btnActualizarGrilla.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.btnActualizarGrilla.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.btnActualizarGrilla.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnActualizarGrilla.Location = new System.Drawing.Point(1113, 547);
            this.btnActualizarGrilla.Name = "btnActualizarGrilla";
            this.btnActualizarGrilla.Size = new System.Drawing.Size(134, 34);
            this.btnActualizarGrilla.TabIndex = 11;
            this.btnActualizarGrilla.Text = "Actualizar";
            this.btnActualizarGrilla.UseVisualStyleBackColor = false;
            this.btnActualizarGrilla.Click += new System.EventHandler(this.btnActualizarGrilla_Click);
            // 
            // lblIP
            // 
            this.lblIP.AutoSize = true;
            this.lblIP.Location = new System.Drawing.Point(74, 672);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(44, 16);
            this.lblIP.TabIndex = 12;
            this.lblIP.Text = "label1";
            // 
            // lblIndicadorConexion
            // 
            this.lblIndicadorConexion.AutoSize = true;
            this.lblIndicadorConexion.Location = new System.Drawing.Point(226, 672);
            this.lblIndicadorConexion.Name = "lblIndicadorConexion";
            this.lblIndicadorConexion.Size = new System.Drawing.Size(44, 16);
            this.lblIndicadorConexion.TabIndex = 13;
            this.lblIndicadorConexion.Text = "label2";
            // 
            // lblEstadoConexion
            // 
            this.lblEstadoConexion.AutoSize = true;
            this.lblEstadoConexion.Location = new System.Drawing.Point(411, 672);
            this.lblEstadoConexion.Name = "lblEstadoConexion";
            this.lblEstadoConexion.Size = new System.Drawing.Size(44, 16);
            this.lblEstadoConexion.TabIndex = 14;
            this.lblEstadoConexion.Text = "label3";
            // 
            // panelTemperatura
            // 
            this.panelTemperatura.Location = new System.Drawing.Point(757, 608);
            this.panelTemperatura.Name = "panelTemperatura";
            this.panelTemperatura.Size = new System.Drawing.Size(550, 195);
            this.panelTemperatura.TabIndex = 0;
            this.panelTemperatura.Paint += new System.Windows.Forms.PaintEventHandler(this.panelTemperatura_Paint);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1633, 875);
            this.Controls.Add(this.panelTemperatura);
            this.Controls.Add(this.lblEstadoConexion);
            this.Controls.Add(this.lblIndicadorConexion);
            this.Controls.Add(this.lblIP);
            this.Controls.Add(this.dgvAlumnos);
            this.Controls.Add(this.btnActualizarGrilla);
            this.Controls.Add(this.btnEliminarSeleccionado);
            this.Controls.Add(this.btnGuardarEdiciones);
            this.Controls.Add(this.btnBorrarTodo);
            this.Controls.Add(this.btnRegistrar);
            this.Controls.Add(this.txtBuscar);
            this.Controls.Add(this.textBox3);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.pictureBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAlumnos)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.DataGridView dgvAlumnos;
        private System.Windows.Forms.TextBox txtBuscar;
        private System.Windows.Forms.Button btnRegistrar;
        private System.Windows.Forms.Button btnBorrarTodo;
        private System.Windows.Forms.Button btnGuardarEdiciones;
        private System.Windows.Forms.Button btnEliminarSeleccionado;
        private System.Windows.Forms.Button btnActualizarGrilla;
        private System.Windows.Forms.Label lblIP;
        private System.Windows.Forms.Label lblIndicadorConexion;
        private System.Windows.Forms.Label lblEstadoConexion;
        private System.Windows.Forms.Panel panelTemperatura;
    }
}

