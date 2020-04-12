using System;
using System.Windows;
using System.Windows.Threading;
using SharpDX.XInput;
using System.IO.Ports;
using System.Windows.Input;

namespace controller
{
    public partial class MainWindow : Window
    {
        SerialPort sp = new SerialPort();
        DispatcherTimer _timer = new DispatcherTimer();
        private Controller _controller;
        bool controllerExist;
        public MainWindow()
        {
            InitializeComponent();
            //
            this.KeyDown += new KeyEventHandler(OnButtonKeyDown);
            this.KeyUp += new KeyEventHandler(OnButtonKeyUp);
            // load PTZ position names:
            RefreshPTZnames();

            // Get port names and put them in the comBox
            string[] ArrayComPortsNames = null;
            ArrayComPortsNames = SerialPort.GetPortNames();
            Array.Sort(ArrayComPortsNames);
            for (int i = 0; i < ArrayComPortsNames.Length; i++)
            {
                cboPorts.Items.Add(ArrayComPortsNames[i]);
            }
            cboPorts.Text = ArrayComPortsNames[0];
            // set controller
            try
            {
                _controller = new Controller(UserIndex.One);
                var state = _controller.GetState();
                controllerExist = true;
            }
            catch
            {
                controllerExist = false;
                MessageBox.Show("Gameroller is not connected");
            }
            // set timmer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += _timer_Tick;
            _timer.Start();
        }
        /// <summary>
        /// commands
        /// </summary>
        /// <param name="N"></param>
        /// <returns></returns>
        private byte[] RecallPositionCommand(int N)
        {
            byte[] bytestosend = { 0x81, 0x01, 0x04, 0x3F, 0x02, (byte)N, 0xFF };
            return bytestosend;
        }

        private byte[] SetPositionCommand(int N)
        {
            byte[] bytestosend = { 0x81, 0x01, 0x04, 0x3F, 0x01, (byte)N, 0xFF };
            return bytestosend;
        }

        private byte[] PanTiltCommand(int B1, int B2, int PL, int TL)
        {
            byte[] bytestosend = { 0x81, 0x01, 0x06, 0x01, (byte)PL, (byte)TL, (byte)B1, (byte)B2, 0xFF };
            return bytestosend;
        }

        private byte[] ZoomCommand(int C)
        {
            byte[] bytestosend = { 0x81, 0x01, 0x04, 0x07, (byte)C, 0xFF };
            return bytestosend;
        }

        private void SendACommand(byte[] message)
        {
            try
            {
                sp.Write(message, 0, message.Length);
            }
            catch{}
        }
        /// <summary>
        /// gamepad control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _timer_Tick(object sender, EventArgs e)
        {
            if(controllerExist)
            {
                try
                {
                    Gamepad2Controller();
                }
                catch {
                    controllerExist = false;
                    Disp.Text = "waiting for controller reconnection";
                }
            }
            else
            {
                try
                {
                    _controller = new Controller(UserIndex.One);
                    var state = _controller.GetState();
                    controllerExist = true;
                }
                catch
                {
                    controllerExist = false;
                    Disp.Text = "waiting for controller reconnection";
                }
            }
        }

        void Gamepad2Controller()
        {
            // pan and tilt calculation
            var state = _controller.GetState();
            int pan = state.Gamepad.LeftThumbX;
            int tilt = state.Gamepad.LeftThumbY;
            bool Shoulder = state.Gamepad.Buttons.ToString().Contains("Shoulder");
            if (state.Gamepad.LeftTrigger > 128 || state.Gamepad.RightTrigger > 128) Shoulder = true;

            int panL;
            int tiltL;
            int bit1;
            int bit2;

            var deadZone = 6000;
            int Nlevels = 14;

            int levelSize = (int)Math.Round((double)(32767 - deadZone) / Nlevels);

            if (Math.Abs(pan) > deadZone)
            {
                panL = (int)Math.Ceiling((double)((Math.Abs(pan) - deadZone) / levelSize));
                if (Shoulder) panL /= 2;
                if (pan > 0) bit1 = 2;
                else bit1 = 1;
            }
            else
            {
                panL = 0;
                bit1 = 3;
            }
            if (Math.Abs(tilt) > deadZone)
            {
                tiltL = (int)Math.Ceiling((double)((Math.Abs(tilt) - deadZone) / levelSize));
                if (Shoulder) tiltL /= 2;
                if (tilt > 0) bit2 = 1;
                else bit2 = 2;
            }
            else
            {
                tiltL = 0;
                bit2 = 3;
            }

            if (lastPTValue != panL * 10000 + tiltL * 100 + bit2 * 10 + bit1)
            {
                lastPTValue = panL * 10000 + tiltL * 100 + bit2 * 10 + bit1;
                SendACommand(PanTiltCommand(bit1, bit2, panL, tiltL));
            }

            // zoom
            int zoom = state.Gamepad.RightThumbY;
            int zoomDir;
            int zoomL;
            if (Math.Abs(zoom) < deadZone)
            {
                zoomDir = 0;
                zoomL = 0 ;
            }
            else
            {
                if (zoom > 0) zoomDir = 2*16;
                else zoomDir = 3*16;

                Nlevels = 7;
                levelSize = (int)Math.Round((double)(32767 - deadZone) / Nlevels);
                zoomL = (int)Math.Ceiling((double)((Math.Abs(zoom) - deadZone) / levelSize));
                if (Shoulder) zoomL /= 2;
            }
            if (lastZoomL != zoomL+zoomDir)
            {
                SendACommand(ZoomCommand(zoomL + zoomDir));
                lastZoomL = zoomL + zoomDir;
            }
            // display information
            if (true) //false: display debug message
            {
                Disp.Text = "Gamepad connected";
            }
            else
            {
                Disp.Text = "";
                Disp.Text += string.Format("X: {0} Y: {1}", state.Gamepad.LeftThumbX, state.Gamepad.LeftThumbY);
                Disp.Text += '\n' + string.Format("X: {0} Y: {1}", state.Gamepad.RightThumbX, state.Gamepad.RightThumbY);
                Disp.Text += '\n' + string.Format("X: {0} Y: {1}", state.Gamepad.LeftTrigger, state.Gamepad.RightTrigger);
                Disp.Text += '\n' + string.Format("{0}", state.Gamepad.Buttons);
                Disp.Text += '\n' + string.Format("Shoulder: {0}", state.Gamepad.Buttons.ToString().Contains("Shoulder"));
                Disp.Text += '\n' + string.Format("{0} {1} {2} {3}", bit1, bit2, panL, tiltL);
                Disp.Text += '\n' + string.Format("{0} {1}", zoomDir / 16, zoomL);
            }
        }
        int lastPTValue;
        int lastZoomL;

        /// <summary>
        /// serial communication control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                String portName = cboPorts.Text;
                sp.PortName = portName;
                sp.BaudRate = 9600;
                sp.Open();
                status.Text = "Connected";
            }
            catch (Exception)
            {
                MessageBox.Show("The port selected is occupied");
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                sp.Close();
                status.Text = "Disconnected";
            }
            catch (Exception)
            {
            }
        }
        /// <summary>
        /// recall buttons
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CallPositionButton0_Click(object sender, RoutedEventArgs e)
        {
            SendACommand(RecallPositionCommand(0));
        }
        private void CallPositionButton1_Click(object sender, RoutedEventArgs e)
        {
            SendACommand(RecallPositionCommand(1));
        }

        private void CallPositionButton2_Click(object sender, RoutedEventArgs e)
        {
            SendACommand(RecallPositionCommand(2));
        }

        private void CallPositionButton3_Click(object sender, RoutedEventArgs e)
        {
            SendACommand(RecallPositionCommand(3));
        }

        private void CallPositionButton4_Click(object sender, RoutedEventArgs e)
        {
            SendACommand(RecallPositionCommand(4));
        }

        private void CallPositionButton5_Click(object sender, RoutedEventArgs e)
        {
            SendACommand(RecallPositionCommand(5));
        }
        /// <summary>
        /// set PTZ positions control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbPTZnames_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                var message = "Save Current PTZ position to\nPosition ";
                PTZPosName.Text = cbPTZnames.SelectedItem.ToString();
                message += cbPTZnames.SelectedIndex.ToString();
                message += ": " + cbPTZnames.SelectedItem.ToString();
                btPTZset.Content = message;
            }
            catch { }
        }

        private void btPTZset_Click(object sender, RoutedEventArgs e)
        {
            if (cbPTZnames.SelectedIndex == 0) Properties.Settings.Default.PTZname0 = PTZPosName.Text;
            if (cbPTZnames.SelectedIndex == 1) Properties.Settings.Default.PTZname1 = PTZPosName.Text;
            if (cbPTZnames.SelectedIndex == 2) Properties.Settings.Default.PTZname2 = PTZPosName.Text;
            if (cbPTZnames.SelectedIndex == 3) Properties.Settings.Default.PTZname3 = PTZPosName.Text;
            if (cbPTZnames.SelectedIndex == 4) Properties.Settings.Default.PTZname4 = PTZPosName.Text;
            if (cbPTZnames.SelectedIndex == 5) Properties.Settings.Default.PTZname5 = PTZPosName.Text;
            SendACommand(SetPositionCommand(cbPTZnames.SelectedIndex));
            Properties.Settings.Default.Save();
            RefreshPTZnames();
        }
        private void RefreshPTZnames()
        {

            CallPositionButton0.Content = Properties.Settings.Default.PTZname0;
            CallPositionButton1.Content = Properties.Settings.Default.PTZname1;
            CallPositionButton2.Content = Properties.Settings.Default.PTZname2;
            CallPositionButton3.Content = Properties.Settings.Default.PTZname3;
            CallPositionButton4.Content = Properties.Settings.Default.PTZname4;
            CallPositionButton5.Content = Properties.Settings.Default.PTZname5;

            cbPTZnames.Items.Clear();

            cbPTZnames.Items.Add(Properties.Settings.Default.PTZname0);
            cbPTZnames.Items.Add(Properties.Settings.Default.PTZname1);
            cbPTZnames.Items.Add(Properties.Settings.Default.PTZname2);
            cbPTZnames.Items.Add(Properties.Settings.Default.PTZname3);
            cbPTZnames.Items.Add(Properties.Settings.Default.PTZname4);
            cbPTZnames.Items.Add(Properties.Settings.Default.PTZname5);
            cbPTZnames.Text = Properties.Settings.Default.PTZname0;
        }
        /// <summary>
        /// alternative control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// 
        private void PTZ_stop(object sender, RoutedEventArgs e)
        {
            SendACommand(PanTiltCommand(03, 03, 0, 0));
        }
        private void PTZup_on(object sender, RoutedEventArgs e)
        {
            SendACommand(PanTiltCommand(03, 01, 5, 5));
        }

        private void PTZdown_on(object sender, RoutedEventArgs e)
        {

            SendACommand(PanTiltCommand(03, 02, 5, 5));
        }

        int KeySpeed = 2;
        private void OnButtonKeyDown(object sender, KeyEventArgs e)
        {
            if (Tab.SelectedIndex == 2)
            {
                if (e.Key.ToString() == "W") SendACommand(PanTiltCommand(03, 01, KeySpeed * 2, KeySpeed * 2));
                if (e.Key.ToString() == "S") SendACommand(PanTiltCommand(03, 02, KeySpeed * 2, KeySpeed * 2));
                if (e.Key.ToString() == "A") SendACommand(PanTiltCommand(01, 03, KeySpeed * 2, KeySpeed * 2));
                if (e.Key.ToString() == "D") SendACommand(PanTiltCommand(02, 03, KeySpeed * 2, KeySpeed * 2));
                if (e.Key.ToString() == "OemPlus") SendACommand(ZoomCommand(KeySpeed + 2 * 16));
                if (e.Key.ToString() == "OemMinus") SendACommand(ZoomCommand(KeySpeed + 3 * 16));
                if (e.Key.ToString() == "D1") KeySpeed = 1;
                if (e.Key.ToString() == "D2") KeySpeed = 2;
                if (e.Key.ToString() == "D3") KeySpeed = 3;
                if (e.Key.ToString() == "D4") KeySpeed = 4;
                if (e.Key.ToString() == "D5") KeySpeed = 5;
            }
        }
        private void OnButtonKeyUp(object sender, KeyEventArgs e)
        {
            if (Tab.SelectedIndex == 2)
            {
                SendACommand(PanTiltCommand(03, 03, 0, 0));
                SendACommand(ZoomCommand(0));
            }
        }
    }
}
