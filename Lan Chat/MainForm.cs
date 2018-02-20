using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Lan_Chat
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        Thread listener;
        UdpClient client;
        Dictionary<string, int> ipdict;

        Dictionary<string, long> pubfiles;
        Dictionary<string, List<byte[]>> pubfiledata;

        Dictionary<string, long> sharedfiles;

        Encoding enc = new UTF8Encoding(true, true);

        System.Threading.Timer Rerequester;

        byte[] rcvdata;
        string rcvdictfile;
        string rcvfile;
        IPAddress rcvsender;
        int rcvpart;
        int rcvnextpart;

        IPAddress localaddress;

        private void worker()
        {
            client = new UdpClient(21621);
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 21621);
            while (true)
            {
                byte[] msg = client.Receive(ref ep);
                if (!ep.Address.Equals(localaddress))
                {
                    Invoke(MessageHandler, new Datagram(msg), ep);
                }
            }
        }

        private delegate void incomingMessage(Datagram data, IPEndPoint source);
        incomingMessage MessageHandler;

        private delegate void outgoingMessage(Datagram data);
        outgoingMessage MessageSender;

        private delegate void outgoingUnicastMessage(Datagram data, IPAddress dest);
        outgoingUnicastMessage MessageUnicastSender;

        private void handleMessage(Datagram data, IPEndPoint source)
        {
            int pos = 1;
            try
            {
                switch (data.Data[0])
                {
                    case 0:
                        string sender = data.ReadString(ref pos);
                        string message = data.ReadString(ref pos);
                        userip(sender, source.Address.ToString());
                        textBox2.AppendText("\r\n" + listView1.Items[ipdict[source.Address.ToString()]].SubItems[2].Text + ":\"" + sender + "\": " + message);
                        if (WindowState == FormWindowState.Minimized)
                        {
                            notifyIcon1.ShowBalloonTip(100,sender,message,ToolTipIcon.None);
                        }
                        break;
                    case 1:
                        userip(data.ReadString(ref pos), source.Address.ToString());
                        break;
                    case 2:
                        string name = data.ReadString(ref pos);
                        long size = data.Readlong(ref pos);
                        string sharerid = ipdict[source.Address.ToString()].ToString();
                        if (sharedfiles.ContainsKey(sharerid + ":" + name))
                        {
                            sharedfiles[sharerid + ":" + name] = size;
                        }
                        else
                        {
                            ListViewItem item = new ListViewItem(new string[] { name, sharerid, getFileSize(size), source.Address.ToString() });
                            listView2.Items.Insert(sharedfiles.Count, item);
                            sharedfiles.Add(sharerid + ":" + name, size);
                        }
                        break;
                    case 3:
                        string filename = data.ReadString(ref pos);
                        int filepart = data.ReadInt(ref pos);
                        data.ID = 4;
                        data.AppendBytes(pubfiledata[filename][filepart]);
                        Invoke(MessageUnicastSender, data, source.Address);
                        break;
                    case 4:
                        if (rcvfile == data.ReadString(ref pos))
                        {
                            if (rcvnextpart == data.ReadInt(ref pos))
                            {
                                Rerequester.Change(int.MaxValue, int.MaxValue);
                                if (rcvnextpart < rcvpart)
                                {
                                    Array.Copy(data.ReadBytes(ref pos, 32 * 1024), 0, rcvdata, rcvnextpart * 32 * 1024, 32 * 1024);
                                    rcvnextpart++;
                                    DownloadProgress();
                                    Rerequester.Change(100, int.MaxValue);
                                    requestnextpart();
                                }
                                else
                                {
                                    Array.Copy(data.ReadBytes(ref pos, rcvdata.Length % (32 * 1024)), 0, rcvdata, rcvnextpart * 32 * 1024, rcvdata.Length % (32 * 1024));

                                    FileStream outfile = File.OpenWrite(saveFileDialog1.FileName);
                                    button2.Text = "Schreibe Daten";
                                    outfile.Write(rcvdata, 0, rcvdata.Length);
                                    outfile.Close();
                                    MessageBox.Show("Dateidownload Fertig!\r\n" + rcvfile, "Download Abgeschlossen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    button2.Text = "Download";
                                    button2.Enabled = true;

                                }
                            }
                        }
                        break;
                }
            }
            catch { }
        }
        private string getFileSize(long bytes)
        {
            double size = bytes;
            int magnitude = 0;
            string[] suffixe = new string[] { " B", " KiB", " MiB", " GiB", " TiB" };
            while (size >= 1000)
            {
                magnitude++;
                size /= 1024;
            }
            return Math.Round(size, 1).ToString() + suffixe[magnitude];
        }
        private void userip(string user, string ip)
        {
            if (ipdict.ContainsKey(ip))
            {
                listView1.Items[ipdict[ip]].SubItems[1].Text = user;
            }
            else
            {
                ListViewItem item = new ListViewItem(new string[] { ip, user, ipdict.Count.ToString() });
                listView1.Items.Insert(ipdict.Count, item);
                ipdict.Add(ip, ipdict.Count);
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            string strHostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
            IPAddress[] addr = ipEntry.AddressList;
            notifyIcon1.Icon = Icon;
            for (int i = 0; i < addr.Length; i++)
            {
                if (addr[i].AddressFamily == AddressFamily.InterNetwork)
                    localaddress = addr[i];
            }
            ipdict = new Dictionary<string, int>();
            pubfiledata = new Dictionary<string, List<byte[]>>();
            pubfiles = new Dictionary<string, long>();
            sharedfiles = new Dictionary<string, long>();
            MessageHandler = handleMessage;
            MessageSender = sendDatagram;
            MessageUnicastSender = sendDatagram;
            Rerequester = new System.Threading.Timer(new TimerCallback(requestnextpart));
            listener = new Thread(new ThreadStart(worker));
            listener.Start();
            timer1.Start();
            textBox1.Text = Environment.UserName;
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            listener.Abort();
            client.Close();
        }
        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                if (textBox1.Text != "" && textBox3.Text != "")
                {
                    Datagram msg = new Datagram(0);
                    msg.AppendString(textBox1.Text);
                    msg.AppendString(textBox3.Text);
                    sendDatagram(msg);
                    textBox3.Text = "";
                }
            }
        }
        bool first = true;
        private void textBox3_MouseClick(object sender, MouseEventArgs e)
        {
            if (first)
            {
                textBox3.Text = "";
                first = false;
            }
        }
        bool first2 = true;
        private void textBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (first2)
            {
                textBox1.Text = "";
                first2 = false;
            }
        }
        private void sendDatagram(Datagram data)
        {
            sendDatagram(data, IPAddress.Broadcast);
        }
        private void sendDatagram(Datagram data, IPAddress dest)
        {
            client.Send(data.Data, data.Data.Length, new IPEndPoint(dest, 21621));
            Invoke(MessageHandler, data, new IPEndPoint(localaddress, 21621));
        }
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            Datagram data = new Datagram(1);
            data.AppendString(textBox1.Text);
            sendDatagram(data);
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            Datagram data = new Datagram(1);
            data.AppendString(textBox1.Text);
            Invoke(MessageSender, data);
            foreach (KeyValuePair<string, long> file in pubfiles)
            {
                Datagram filedatagram = new Datagram(2);
                filedatagram.AppendString(file.Key);
                filedatagram.AppendLong(file.Value);
                Invoke(MessageSender, filedatagram);
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string name = openFileDialog1.FileName.Substring(openFileDialog1.FileName.LastIndexOf('\\') + 1, openFileDialog1.FileName.Length - openFileDialog1.FileName.LastIndexOf('\\') - 1);
                byte[] data = File.ReadAllBytes(openFileDialog1.FileName);
                long len = data.LongLength;
                long pos = 0;
                List<byte[]> splitdata = new List<byte[]>();
                while (pos < len)
                {
                    long size = 0;
                    if (pos + 32 * 1024 > len)
                        size = len - pos;
                    else
                        size = 32 * 1024;
                    byte[] nextpart = new byte[size];
                    Array.Copy(data, pos, nextpart, 0, size);
                    splitdata.Add(nextpart);
                    pos += size;
                }
                pubfiledata.Add(name, splitdata);
                pubfiles.Add(name, len);
            }
        }
        void requestnextpart()
        {
            Datagram nextpart = new Datagram(3);
            nextpart.AppendString(rcvfile);
            nextpart.AppendInteger(rcvnextpart);
            Invoke(MessageUnicastSender, nextpart, rcvsender);
        }
        void requestnextpart(object state)
        {
            requestnextpart();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedItems.Count == 1)
            {
                rcvfile = listView2.SelectedItems[0].SubItems[0].Text;
                saveFileDialog1.FileName = rcvfile;
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    button2.Enabled = false;

                    rcvdictfile = listView2.SelectedItems[0].SubItems[1].Text + ":" + rcvfile;
                    rcvsender = IPAddress.Parse(listView2.SelectedItems[0].SubItems[3].Text);
                    rcvdata = new byte[sharedfiles[rcvdictfile]];
                    rcvpart = (int)((sharedfiles[rcvdictfile]) / (32 * 1024));
                    rcvnextpart = 0;
                    DownloadProgress();
                    requestnextpart();
                }
            }
        }
        private void DownloadProgress()
        {
            button2.Text = "Download: " + rcvfile + " " + Math.Round(100 * (rcvnextpart / (double)rcvpart), 1).ToString() + "%";
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            WindowState = FormWindowState.Normal;
            Focus();
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
                notifyIcon1.Visible = true;
            }
            else
            {
                ShowInTaskbar = true;
                notifyIcon1.Visible = false;
            }
        }

        private void notifyIcon1_BalloonTipClicked(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            Focus();
        }
    }
}