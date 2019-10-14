using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AsyncTcpServer
{
    public partial class FormServer : Form
    {
        private bool isExit = false;
        //保存连接的所有客户端
        System.Collections.ArrayList clientList = new System.Collections.ArrayList();
        TcpListener listener;
        //用于线程间的互操作
        private delegate void SetListBoxCallback(string str);
        private SetListBoxCallback setListBoxCallback;
        private delegate void SetRichTextBoxCallback(string str);
        private SetRichTextBoxCallback setRichTextBoxCallback;
        private delegate void SetComboBoxCallback(string str);
        private SetComboBoxCallback setComboBoxCallback;
        private delegate void RemoveComboBoxItemsCallback(ReadWriteObject readWriteObject);
        private RemoveComboBoxItemsCallback removeComboBoxItemsCallback;
        //用于线程同步，初始状态设为非终止状态，使用手动重置方式
        private EventWaitHandle allDone = new EventWaitHandle(false, EventResetMode.ManualReset);
        public FormServer()
        {
            InitializeComponent();
            listBoxStatus.HorizontalScrollbar = true;
            setListBoxCallback = new SetListBoxCallback(SetListBox);
            setRichTextBoxCallback = new SetRichTextBoxCallback(SetReceiveText);
            setComboBoxCallback = new SetComboBoxCallback(setComboBox);
            removeComboBoxItemsCallback = new RemoveComboBoxItemsCallback(RemoveComboBoxItems);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            isExit = false;
            //由于服务器要为多个客户服务，所以创建一个线程监听客户端连接请求
            ThreadStart ts = new ThreadStart(AcceptConnect);
            Thread myThread = new Thread(ts);
            myThread.Start();
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;
        }

        private void AcceptConnect()
        {
            //获取本机所有IP地址
            IPAddress[] ip = Dns.GetHostAddresses(Dns.GetHostName());
            listener = new TcpListener(ip[1], 51888);
            listener.Start();
            while(isExit==false)
            {
                try
                {
                    //将事件的状态设置为非终止
                    allDone.Reset();
                    //引用在异步操作完成时调用的回调方法
                    AsyncCallback callback = new AsyncCallback(AcceptTcpClientCallback);
                    listBoxStatus.Invoke(setListBoxCallback, "开始等待客户连接");
                    //开始一个异步操作接收传入的连接尝试
                    listener.BeginAcceptTcpClient(callback, listener);
                    allDone.WaitOne();
                }
                catch(Exception err)
                {
                    listBoxStatus.Invoke(setListBoxCallback, err.Message);
                    break;
                }
            }
            listener.Stop();
        }

        //ar是IAsyncResult类型的接口，表示异步操作的状态
        //是由listener.BeginAcceptTcpClient(callback, listener)传递过来的
        private void AcceptTcpClientCallback(IAsyncResult ar)
        {
            try
            {
                //将事件状态设置为终止状态，语序一个或多个等待线程继续
                allDone.Set();
                TcpListener myListener = (TcpListener)ar.AsyncState;
                //异步接收传入的连接，并创建新的TcpClient对象处理远程主机通信
                TcpClient client = myListener.EndAcceptTcpClient(ar);
                listBoxStatus.Invoke(setListBoxCallback, "已接收客户连接：" + client.Client.RemoteEndPoint);
                comboBox1.Invoke(setComboBoxCallback, client.Client.RemoteEndPoint.ToString());
                ReadWriteObject readWriteObject = new ReadWriteObject(client);
                clientList.Add(readWriteObject);
                SendString(readWriteObject, "服务器已经接受连接，请通话！");
                readWriteObject.netStream.BeginRead(readWriteObject.readBytes, 0, readWriteObject.readBytes.Length, ReadCallback, readWriteObject);
            }
            catch(Exception err)
            {
                listBoxStatus.Invoke(setListBoxCallback, err.Message);
                return;
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            try
            {
                ReadWriteObject readWriteObject = (ReadWriteObject)ar.AsyncState;
                int count = readWriteObject.netStream.EndRead(ar);
                richTextBoxReceive.Invoke(setRichTextBoxCallback, string.Format("[来自{0}]{1}", readWriteObject.client.Client.RemoteEndPoint, System.Text.Encoding.UTF8.GetString(readWriteObject.readBytes, 0, count)));
                if(isExit==false)
                {
                    readWriteObject.InitReadArray();
                    readWriteObject.netStream.BeginRead(readWriteObject.readBytes, 0, readWriteObject.readBytes.Length, ReadCallback, readWriteObject);
                }
            }
            catch(Exception err)
            {
                listBoxStatus.Invoke(setListBoxCallback, err.Message);
            }
        }

        private void SendString(ReadWriteObject readWriteObject, string str)
        {
            try
            {
                readWriteObject.writeBytes = Encoding.UTF8.GetBytes(str + "\r\n");
                readWriteObject.netStream.BeginWrite(readWriteObject.writeBytes, 0, readWriteObject.writeBytes.Length, new AsyncCallback(SendCallback), readWriteObject);
                readWriteObject.netStream.Flush();
                listBoxStatus.Invoke(setListBoxCallback, string.Format("向{0}发送：{1}", readWriteObject.client.Client.RemoteEndPoint, str));
            }
            catch(Exception err)
            {
                listBoxStatus.Invoke(setListBoxCallback, err.Message);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            ReadWriteObject readWriteObject = (ReadWriteObject)ar.AsyncState;
            try
            {
                readWriteObject.netStream.EndWrite(ar);
            }
            catch(Exception err)
            {
                listBoxStatus.Invoke(setListBoxCallback, err.Message);
                comboBox1.Invoke(removeComboBoxItemsCallback, readWriteObject);
            }
        }

        private void RemoveComboBoxItems(ReadWriteObject readWriteObject)
        {
            int index = clientList.IndexOf(readWriteObject);
            comboBox1.Items.RemoveAt(index);
        }

        private void SetListBox(string str)
        {
            listBoxStatus.Items.Add(str);
            listBoxStatus.SelectedIndex = listBoxStatus.Items.Count - 1;
            listBoxStatus.ClearSelected();
        }

        private void SetReceiveText(string str)
        {
            richTextBoxReceive.AppendText(str);
        }

        private void setComboBox(object obj)
        {
            comboBox1.Items.Add(obj);
        }

        //【停止监听】按钮的Click事件
        private void buttonStop_Click(object sender, EventArgs e)
        {
            //是线程自动结束
            isExit = true;
            //将事件状态设置为终止状态，允许一个或者多个等待线程继续
            //从而使线程正常结束
            allDone.Set();
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
        }

        //【发送】按钮Click事件
        private void buttonSend_Click(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if(index==-1)
            {
                MessageBox.Show("请先选择接受方，然后再点击[发送]");
            }
            else
            {
                ReadWriteObject obj = (ReadWriteObject)clientList[index];
                SendString(obj, richTextBoxSend.Text);
                richTextBoxSend.Clear();
            }
        }

        //关闭窗体前触发的事件
        private void FormServer_FormClosing(object sender, FormClosingEventArgs e)
        {
            buttonStop_Click(null, null);
        }
    }
}
