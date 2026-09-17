namespace TithorAutomation
{
    partial class frmProducto
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
            this.lblTituloProducto = new System.Windows.Forms.Label();
            this.lblNombreProducto = new System.Windows.Forms.Label();
            this.lblCodigoProducto = new System.Windows.Forms.Label();
            this.lblAyudaCodigo = new System.Windows.Forms.Label();
            this.txtNombreProducto = new System.Windows.Forms.TextBox();
            this.txtCodigoProducto = new System.Windows.Forms.TextBox();
            this.chkProductoActivo = new System.Windows.Forms.CheckBox();
            this.lblDescripcionProducto = new System.Windows.Forms.Label();
            this.txtDescripcionProducto = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnGuardarProducto = new System.Windows.Forms.Button();
            this.btnCancelarProducto = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTituloProducto
            // 
            this.lblTituloProducto.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblTituloProducto.AutoSize = true;
            this.lblTituloProducto.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTituloProducto.ForeColor = System.Drawing.Color.White;
            this.lblTituloProducto.Location = new System.Drawing.Point(15, 18);
            this.lblTituloProducto.Name = "lblTituloProducto";
            this.lblTituloProducto.Size = new System.Drawing.Size(173, 21);
            this.lblTituloProducto.TabIndex = 0;
            this.lblTituloProducto.Text = "AGREGAR PRODUCTO";
            // 
            // lblNombreProducto
            // 
            this.lblNombreProducto.AutoSize = true;
            this.lblNombreProducto.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblNombreProducto.Location = new System.Drawing.Point(19, 79);
            this.lblNombreProducto.Name = "lblNombreProducto";
            this.lblNombreProducto.Size = new System.Drawing.Size(141, 17);
            this.lblNombreProducto.TabIndex = 1;
            this.lblNombreProducto.Text = "Nombre del producto";
            // 
            // lblCodigoProducto
            // 
            this.lblCodigoProducto.AutoSize = true;
            this.lblCodigoProducto.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCodigoProducto.Location = new System.Drawing.Point(19, 154);
            this.lblCodigoProducto.Name = "lblCodigoProducto";
            this.lblCodigoProducto.Size = new System.Drawing.Size(101, 17);
            this.lblCodigoProducto.TabIndex = 2;
            this.lblCodigoProducto.Text = "Código interno";
            // 
            // lblAyudaCodigo
            // 
            this.lblAyudaCodigo.AutoSize = true;
            this.lblAyudaCodigo.Location = new System.Drawing.Point(19, 211);
            this.lblAyudaCodigo.Name = "lblAyudaCodigo";
            this.lblAyudaCodigo.Size = new System.Drawing.Size(238, 17);
            this.lblAyudaCodigo.TabIndex = 3;
            this.lblAyudaCodigo.Text = "Ejemplo: CAMISETAS, FUNDAS, SHORTS";
            // 
            // txtNombreProducto
            // 
            this.txtNombreProducto.Location = new System.Drawing.Point(19, 108);
            this.txtNombreProducto.MaxLength = 100;
            this.txtNombreProducto.Name = "txtNombreProducto";
            this.txtNombreProducto.Size = new System.Drawing.Size(277, 25);
            this.txtNombreProducto.TabIndex = 4;
            this.txtNombreProducto.TextChanged += new System.EventHandler(this.txtNombreProducto_TextChanged);
            // 
            // txtCodigoProducto
            // 
            this.txtCodigoProducto.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.txtCodigoProducto.Location = new System.Drawing.Point(19, 183);
            this.txtCodigoProducto.MaxLength = 40;
            this.txtCodigoProducto.Name = "txtCodigoProducto";
            this.txtCodigoProducto.Size = new System.Drawing.Size(277, 25);
            this.txtCodigoProducto.TabIndex = 5;
            this.txtCodigoProducto.TextChanged += new System.EventHandler(this.txtCodigoProducto_TextChanged);
            // 
            // chkProductoActivo
            // 
            this.chkProductoActivo.AutoSize = true;
            this.chkProductoActivo.Checked = true;
            this.chkProductoActivo.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProductoActivo.Location = new System.Drawing.Point(19, 368);
            this.chkProductoActivo.Name = "chkProductoActivo";
            this.chkProductoActivo.Size = new System.Drawing.Size(118, 21);
            this.chkProductoActivo.TabIndex = 6;
            this.chkProductoActivo.Text = "Producto activo";
            this.chkProductoActivo.UseVisualStyleBackColor = true;
            // 
            // lblDescripcionProducto
            // 
            this.lblDescripcionProducto.AutoSize = true;
            this.lblDescripcionProducto.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDescripcionProducto.Location = new System.Drawing.Point(19, 248);
            this.lblDescripcionProducto.Name = "lblDescripcionProducto";
            this.lblDescripcionProducto.Size = new System.Drawing.Size(80, 17);
            this.lblDescripcionProducto.TabIndex = 9;
            this.lblDescripcionProducto.Text = "Descripción";
            // 
            // txtDescripcionProducto
            // 
            this.txtDescripcionProducto.Location = new System.Drawing.Point(19, 268);
            this.txtDescripcionProducto.MaxLength = 300;
            this.txtDescripcionProducto.Multiline = true;
            this.txtDescripcionProducto.Name = "txtDescripcionProducto";
            this.txtDescripcionProducto.Size = new System.Drawing.Size(277, 94);
            this.txtDescripcionProducto.TabIndex = 10;
            this.txtDescripcionProducto.TextChanged += new System.EventHandler(this.txtDescripcionProducto_TextChanged);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(49)))), ((int)(((byte)(82)))));
            this.panel1.Controls.Add(this.lblTituloProducto);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(323, 58);
            this.panel1.TabIndex = 11;
            // 
            // btnGuardarProducto
            // 
            this.btnGuardarProducto.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGuardarProducto.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(216)))), ((int)(((byte)(68)))));
            this.btnGuardarProducto.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnGuardarProducto.FlatAppearance.BorderSize = 0;
            this.btnGuardarProducto.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnGuardarProducto.Location = new System.Drawing.Point(166, 400);
            this.btnGuardarProducto.Name = "btnGuardarProducto";
            this.btnGuardarProducto.Size = new System.Drawing.Size(130, 40);
            this.btnGuardarProducto.TabIndex = 19;
            this.btnGuardarProducto.Text = "Guardar producto";
            this.btnGuardarProducto.UseVisualStyleBackColor = false;
            this.btnGuardarProducto.Click += new System.EventHandler(this.btnGuardarProducto_Click);
            // 
            // btnCancelarProducto
            // 
            this.btnCancelarProducto.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelarProducto.BackColor = System.Drawing.Color.White;
            this.btnCancelarProducto.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancelarProducto.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(48)))), ((int)(((byte)(82)))));
            this.btnCancelarProducto.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancelarProducto.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(48)))), ((int)(((byte)(82)))));
            this.btnCancelarProducto.Location = new System.Drawing.Point(19, 400);
            this.btnCancelarProducto.Name = "btnCancelarProducto";
            this.btnCancelarProducto.Size = new System.Drawing.Size(130, 40);
            this.btnCancelarProducto.TabIndex = 20;
            this.btnCancelarProducto.Text = "Cancelar";
            this.btnCancelarProducto.UseVisualStyleBackColor = false;
            this.btnCancelarProducto.Click += new System.EventHandler(this.btnCancelarProducto_Click);
            // 
            // frmProducto
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(323, 452);
            this.Controls.Add(this.btnCancelarProducto);
            this.Controls.Add(this.btnGuardarProducto);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.txtDescripcionProducto);
            this.Controls.Add(this.lblDescripcionProducto);
            this.Controls.Add(this.chkProductoActivo);
            this.Controls.Add(this.txtCodigoProducto);
            this.Controls.Add(this.txtNombreProducto);
            this.Controls.Add(this.lblAyudaCodigo);
            this.Controls.Add(this.lblCodigoProducto);
            this.Controls.Add(this.lblNombreProducto);
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmProducto";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Agregar producto";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblTituloProducto;
        private System.Windows.Forms.Label lblNombreProducto;
        private System.Windows.Forms.Label lblCodigoProducto;
        private System.Windows.Forms.Label lblAyudaCodigo;
        private System.Windows.Forms.TextBox txtNombreProducto;
        private System.Windows.Forms.TextBox txtCodigoProducto;
        private System.Windows.Forms.CheckBox chkProductoActivo;
        private System.Windows.Forms.Label lblDescripcionProducto;
        private System.Windows.Forms.TextBox txtDescripcionProducto;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnGuardarProducto;
        private System.Windows.Forms.Button btnCancelarProducto;
    }
}