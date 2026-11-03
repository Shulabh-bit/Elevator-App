using System;
using System.Drawing;
using System.Windows.Forms;

namespace ElevatorApp
{
    public class LogForm : Form
    {
        private readonly DataGridView grid;
        private readonly Label status;

        public LogForm()
        {
            Text = "Elevator Operation Logs";
            ClientSize = new Size(760, 430);
            StartPosition = FormStartPosition.CenterParent;

            grid = new DataGridView
            {
                Location = new Point(10, 10),
                Size = new Size(740, 340),
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            Controls.Add(grid);

            AddButton("Delete", new Point(10, 360), Delete_Click);
            AddButton("Clear All Logs", new Point(120, 360), ClearAll_Click);
            AddButton("Refresh", new Point(240, 360), (s, e) => Reload());
            AddButton("Close", new Point(650, 360), (s, e) => Close());

            status = new Label { Location = new Point(10, 400), Size = new Size(740, 22) };
            Controls.Add(status);

            Reload();
        }

        private void AddButton(string text, Point loc, EventHandler handler)
        {
            var b = new Button { Text = text, Location = loc, Size = new Size(100, 30) };
            b.Click += handler;
            Controls.Add(b);
        }

        public void Reload()
        {
            Background.Run(LogRepository.GetAll,
                table =>
                {
                    if (IsDisposed) return;
                    grid.DataSource = table;
                    status.ForeColor = Color.Black;
                    status.Text = table.Rows.Count + " records";
                },
                ShowError);
        }

        
        private void RunThenReload(Func<int> work)
        {
            Background.Run(work, result => { if (!IsDisposed) Reload(); }, ShowError);
        }

        private void Delete_Click(object sender, EventArgs e)
        {
            if (grid.CurrentRow == null) return;
            int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            RunThenReload(() => { LogRepository.Delete(id); return 0; });
        }

        private void ClearAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Delete ALL log records?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            RunThenReload(() => { LogRepository.ClearAll(); return 0; });
        }

        private void ShowError(Exception ex)
        {
            if (IsDisposed) return;
            status.ForeColor = Color.Firebrick;
            status.Text = "Database error: " + ex.Message;
        }
    }
}