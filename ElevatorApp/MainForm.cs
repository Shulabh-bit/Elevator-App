using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ElevatorApp
{
    public class MainForm : Form, IElevatorObserver
    {
        private const int CarW = 150, CarH = 190, ShaftX = 55, ShaftW = 190;
        private const int Floor1Bottom = 520, Floor2Bottom = 250;

        private static readonly Color Bg = Color.FromArgb(24, 27, 38);
        private static readonly Color CardCol = Color.FromArgb(38, 44, 62);
        private static readonly Color Accent = Color.FromArgb(64, 132, 244);
        private static readonly Font CarFont = new Font("Segoe UI", 12, FontStyle.Bold);
        private static readonly Font LedFont = new Font("Consolas", 11, FontStyle.Bold);
        private static readonly Font FloorFont = new Font("Segoe UI", 11, FontStyle.Bold);

        private readonly Building building = new Building(1);   // one lift
        private readonly Elevator lift;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 30 };
        private readonly ToolTip tips = new ToolTip();
        private double doorOpen;                                 // 0 = closed, 1 = open
        private BuildingView view;
        private Label led, stateLabel, statusLabel;
        private LogForm logForm;

        // Double buffered panel so the animation does not flicker
        private class BuildingView : Panel
        {
            public BuildingView() { DoubleBuffered = true; ResizeRedraw = true; }
        }

        public MainForm()
        {
            Text = "Elevator Controller";
            ClientSize = new Size(840, 660);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            lift = building.Elevators[0];
            BuildGui();

            lift.Attach(this);
            lift.Attach(new LogObserver(ShowError));

            timer.Tick += Timer_Tick;
            timer.Start();
            RefreshDisplays();
        }

        // ---------------- Task 1: GUI ----------------
        private void BuildGui()
        {
            BackColor = Bg;
            ForeColor = Color.WhiteSmoke;

            // Building with the shaft, the lift and the floor displays
            view = new BuildingView { Location = new Point(160, 50), Size = new Size(300, 560) };
            view.Paint += DrawBuilding;
            Controls.Add(view);

            // Request buttons outside the lift (both show an up arrow)
            AddCallButton("\u25B2", "Floor 2 - call the lift", 2, 205);
            AddCallButton("\u25B2", "Floor 1 - call the lift", 1, 475);

            // Control panel inside the lift
            var panel = new Panel { Location = new Point(500, 50), Size = new Size(300, 320), BackColor = CardCol };
            panel.Controls.Add(new Label
            {
                Text = "CONTROL PANEL",
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(15, 10)
            });

            led = new Label
            {
                Location = new Point(15, 42),
                Size = new Size(270, 60),
                BackColor = Color.Black,
                ForeColor = Color.Red,
                Font = new Font("Consolas", 26, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(led);

            stateLabel = new Label
            {
                Location = new Point(15, 108),
                Size = new Size(270, 22),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10)
            };
            panel.Controls.Add(stateLabel);

            panel.Controls.Add(MakeButton("1", new Point(15, 140), new Size(130, 70),
                Color.FromArgb(70, 80, 110), 1, FloorButton_Click, 20));
            panel.Controls.Add(MakeButton("2", new Point(155, 140), new Size(130, 70),
                Color.FromArgb(70, 80, 110), 2, FloorButton_Click, 20));

            var open = MakeRoundButton("\u25C0|\u25B6", new Point(70, 230), 64,
                Color.FromArgb(200, 130, 40), null, DoorOpen_Click, 12);
            var close = MakeRoundButton("\u25B6|\u25C0", new Point(166, 230), 64,
                Color.FromArgb(150, 70, 70), null, DoorClose_Click, 12);
            tips.SetToolTip(open, "Open door");
            tips.SetToolTip(close, "Close door");
            panel.Controls.Add(open);
            panel.Controls.Add(close);
            Controls.Add(panel);

            Controls.Add(MakeButton("LOG", new Point(500, 390), new Size(300, 46),
                Color.FromArgb(60, 150, 100), null, LogButton_Click));

            statusLabel = new Label { Location = new Point(24, 622), Size = new Size(790, 22), ForeColor = Color.Tomato };
            Controls.Add(statusLabel);
        }

        private void AddCallButton(string arrow, string tip, int floor, int y)
        {
            var b = MakeRoundButton(arrow, new Point(50, y), 64, Accent, floor, HallCall_Click, 18);
            tips.SetToolTip(b, tip);
            Controls.Add(b);
            Controls.Add(new Label
            {
                Text = "FLOOR " + floor,
                Location = new Point(40, y + 70),
                Size = new Size(84, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            });
        }

        private Button MakeButton(string text, Point loc, Size size, Color color, object tag,
                                  EventHandler handler, float fontSize = 10f)
        {
            var b = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Tag = tag,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += handler;
            return b;
        }

        // Round button with an arrow symbol
        private Button MakeRoundButton(string text, Point loc, int size, Color color, object tag,
                                       EventHandler handler, float fontSize)
        {
            Button b = MakeButton(text, loc, new Size(size, size), color, tag, handler, fontSize);
            b.Font = new Font("Segoe UI Symbol", fontSize, FontStyle.Bold);
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, size, size);
            b.Region = new Region(path);
            return b;
        }

        // The lift car rectangle, used for drawing and for the mouse hover test
        private Rectangle CarRect()
        {
            double t = (lift.Position - 1) / (Building.Floors - 1);
            int bottom = (int)Math.Round(Floor1Bottom - t * (Floor1Bottom - Floor2Bottom));
            return new Rectangle(ShaftX + (ShaftW - CarW) / 2, bottom - CarH, CarW, CarH);
        }

        // ---------------- drawing the building ----------------
        private void DrawBuilding(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 23, 32));

            // floor slabs and labels
            using (var slab = new Pen(Color.FromArgb(90, 100, 125), 3))
            {
                g.DrawLine(slab, 0, Floor2Bottom, view.Width, Floor2Bottom);
                g.DrawLine(slab, 0, Floor1Bottom, view.Width, Floor1Bottom);
            }
            g.DrawString("F2", FloorFont, Brushes.Gray, 8, Floor2Bottom - 26);
            g.DrawString("F1", FloorFont, Brushes.Gray, 8, Floor1Bottom - 26);

            // shaft
            var shaft = new Rectangle(ShaftX, 10, ShaftW, Floor1Bottom - 10);
            using (var b = new SolidBrush(Color.FromArgb(12, 14, 20))) g.FillRectangle(b, shaft);
            using (var p = new Pen(Color.FromArgb(70, 78, 100), 2)) g.DrawRectangle(p, shaft);

            string arrow = lift.State is MovingUpState ? " \u25B2" : lift.State is MovingDownState ? " \u25BC" : "";

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                // floor status display above each landing
                foreach (int floorBottom in new[] { Floor2Bottom, Floor1Bottom })
                {
                    var disp = new Rectangle(ShaftX + 35, floorBottom - CarH - 30, 120, 24);
                    g.FillRectangle(Brushes.Black, disp);
                    g.DrawString("FLOOR " + lift.CurrentFloor + arrow, LedFont, Brushes.Red, disp, sf);
                }

                Rectangle car = CarRect();

                using (var cable = new Pen(Color.Gray, 2))
                    g.DrawLine(cable, car.X + CarW / 2, 10, car.X + CarW / 2, car.Y);

                Color c = lift.State is DoorOpenState ? Color.FromArgb(235, 150, 40)
                        : lift.State is MovingState ? Color.FromArgb(40, 170, 110)
                        : Color.FromArgb(80, 105, 150);
                using (var br = new LinearGradientBrush(car, ControlPaint.Light(c), c, 90f))
                    g.FillRectangle(br, car);
                using (var p = new Pen(Color.FromArgb(15, 15, 20), 2)) g.DrawRectangle(p, car);

                g.DrawString("LIFT" + arrow, CarFont, Brushes.White, new Rectangle(car.X, car.Y + 2, CarW, 28), sf);

                // interior and two sliding doors
                var door = new Rectangle(car.X + 12, car.Y + 34, CarW - 24, CarH - 46);
                using (var inner = new SolidBrush(Color.FromArgb(250, 236, 200))) g.FillRectangle(inner, door);
                int dw = (int)Math.Round(door.Width / 2.0 * (1 - doorOpen));
                var left = new Rectangle(door.X, door.Y, dw, door.Height);
                var right = new Rectangle(door.Right - dw, door.Y, dw, door.Height);
                using (var leaf = new SolidBrush(Color.FromArgb(170, 178, 190)))
                {
                    g.FillRectangle(leaf, left);
                    g.FillRectangle(leaf, right);
                }
                g.DrawRectangle(Pens.Black, left);
                g.DrawRectangle(Pens.Black, right);
            }
        }

        // ---------------- Task 2: event handlers ----------------
        private void HallCall_Click(object sender, EventArgs e)
        {
            building.DispatchHallCall((int)((Button)sender).Tag);
        }

        private void FloorButton_Click(object sender, EventArgs e)
        {
            building.CabinRequest(0, (int)((Button)sender).Tag);
        }

        private void DoorOpen_Click(object sender, EventArgs e) { lift.OpenDoorRequest(); }

        private void DoorClose_Click(object sender, EventArgs e) { lift.CloseDoorRequest(); }

        private void LogButton_Click(object sender, EventArgs e)
        {
            if (logForm == null || logForm.IsDisposed) { logForm = new LogForm(); logForm.Show(this); }
            else { logForm.BringToFront(); logForm.Reload(); }
        }

        // ---------------- Task 4: timer animation via delegate ----------------
        private void Timer_Tick(object sender, EventArgs e)
        {
            building.RaiseTick();   // the delegate moves the lift

            // cursor on the lift while the doors are open keeps them open
            var doorState = lift.State as DoorOpenState;
            if (doorState != null && CarRect().Contains(view.PointToClient(Cursor.Position)))
                lift.HoldDoor();

            // doors slide open at the start and close during the last 0.6 s
            doorState = lift.State as DoorOpenState;
            double target = (doorState != null && !doorState.IsClosing) ? 1 : 0;
            doorOpen += (target - doorOpen) * 0.12;   // smooth door animation
            view.Invalidate();
        }

        // ---------------- Observer callback ----------------
        public void ElevatorChanged(Elevator elevator, string logMessage) { RefreshDisplays(); }

        private void RefreshDisplays()
        {
            string arrow = lift.State is MovingUpState ? " \u25B2" : lift.State is MovingDownState ? " \u25BC" : "";
            led.Text = "Floor " + lift.CurrentFloor + arrow;
            stateLabel.Text = "Status: " + lift.State.Name;
            view.Invalidate();
        }

        private void ShowError(Exception ex)
        {
            statusLabel.Text = "Database error: " + ex.Message;
        }
    }
}