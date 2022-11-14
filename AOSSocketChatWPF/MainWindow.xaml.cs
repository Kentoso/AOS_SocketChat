using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AOSSocketChatWPF
{
    public partial class MainWindow : Window
    {
        private Socket Listener;
        private List<Socket> Clients;
        private List<string> Nicknames;
        private IPEndPoint _endPoint;
        private FileStream _logFile;
        public MainWindow()
        {
            Clients = new List<Socket>();
            Nicknames = new List<string>();
            var path = @$"{Directory.GetCurrentDirectory()}\log\log.txt";
            _logFile = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            InitializeComponent();
        }

        private void SendButton_OnClick(object sender, RoutedEventArgs e)
        {
            string messageBoxText = MessageBox.Text;
            if (messageBoxText.ToLower() == "who")
            {
                PrintToChat($"Виконавець лабораторної роботи - студент групи К-25 Самусь Дем'ян. Варіант 4 - Обмін репліками.");
            }
            else
            {
                PrintToChat(messageBoxText);
            }
            MessageBox.Text = "";
        }

        private async void StartServerButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (Listener != null && Listener.IsBound) Listener.Close();
            Nicknames = new List<string>();
            Clients = new List<Socket>();
            IPAddress ipAddress;
            if (ServerIpBox.Text.Trim() == "")
                ipAddress = IPAddress.Parse("127.0.0.1");
            else
            {
                if (!IPAddress.TryParse(ServerIpBox.Text, out ipAddress)) return;
            }
            int variant = 4;
            _endPoint = new(ipAddress, 1025 + variant);
            Listener = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Listener.Bind(_endPoint);
            Listener.Listen(100);
            await ReportToLog($"Bound the server to {ipAddress}:{1025 + variant}");
            await StartServer();
        }
        
        private async Task Listen()
        {
            var buffer = new byte[256];
            await ReportToLog("Listening...");
            var tasks = Clients.Select(c => c.ReceiveAsync(buffer, SocketFlags.None)).ToList();
            while (true)
            {
                for (int i = 0; i < tasks.Count(); i++)
                {
                    var task = tasks[i];
                    if (task.IsCompleted)
                    {
                        tasks[i] = Clients[i].ReceiveAsync(buffer, SocketFlags.None);
                    }
                }
                var received = await Task.WhenAny(tasks);
                var response = Encoding.UTF8.GetString(buffer, 0, received.Result);
                await ReportToLog($"Got \"{response}\"");
                if (response.Length == 0) // client disconnected
                {
                    var userIndex = tasks.IndexOf(received);
                    AnnounceToChat($"{Nicknames[userIndex]} disconnected. Closing server.");
                    await ReportToLog($"Got empty message from {Nicknames[userIndex]}. Closing server");
                    var sendZeroBytePacketTasks =
                        Clients.Where(c => Clients.IndexOf(c) != userIndex).Select(c => c.SendAsync(new byte[]{(byte)'d'}, SocketFlags.None));
                    await Task.WhenAll(sendZeroBytePacketTasks);
                    await ReportToLog($"Sent disable command to clients");
                    ServerStartContainer.Visibility = Visibility.Visible;
                    return;
                }
                if (response[0] == 'c') // message Count
                {
                    var messageCountResponse = response;
                    int messageCount;
                    string messageCountStr = new string(messageCountResponse.Skip(1).ToArray());
                    if (!Int32.TryParse(messageCountStr, out messageCount)) continue;
                    var userIndex = tasks.IndexOf(received);

                    var sendMessageCountTasks = Clients.Select(c => c.SendAsync(buffer, SocketFlags.None));
                    await Task.WhenAll(sendMessageCountTasks);
                    await ReportToLog($"Sent message count to clients : ({messageCountStr}) ");
                    for (int i = 0; i < messageCount; i++)
                    {
                        var messageContentBytes = new byte[21];
                        var n = await Clients[userIndex].ReceiveAsync(messageContentBytes, SocketFlags.None);
                        var message = Encoding.UTF8.GetString(messageContentBytes.Skip(1).ToArray());
                        await ReportToLog($"Got message ({i + 1}) : {message}");
                        var sendMessagesTasks = Clients.Select(c => c.SendAsync(messageContentBytes, SocketFlags.None));
                        await Task.WhenAll(sendMessagesTasks);
                        await ReportToLog($"Sent message ({i + 1}) to clients : {message}");
                    }

                    List<byte> nicknameBytes = new List<byte>() {(byte) 'n'}; // Nickname
                    nicknameBytes.AddRange(Encoding.UTF8.GetBytes(Nicknames[userIndex]));
                    var nicknameBytesArr = nicknameBytes.ToArray();
                    var sendNicknameTasks = Clients.Select(c => c.SendAsync(nicknameBytesArr, SocketFlags.None));
                    await Task.WhenAll(sendNicknameTasks);
                    await ReportToLog($"Sent nickname to clients : {Nicknames[userIndex]}");
                }
            }
        }

        private async Task StartServer()
        {
            int clientNumber = 0;
            if (!Int32.TryParse(ClientNumberBox.Text, out clientNumber)) return;
            ServerStartContainer.Visibility = Visibility.Collapsed;
            AnnounceToChat("Waiting for connections...");
            await ReportToLog("Waiting for connections...");
            for (int i = 0; i < clientNumber; i++)
            {
                var client = await Listener.AcceptAsync();
                await ReportToLog($"Got connection {i + 1}");
                var buffer = new byte[256];
                var received = await client.ReceiveAsync(buffer, SocketFlags.None);
                var clientNickname = Encoding.UTF8.GetString(buffer.Skip(1).ToArray(), 0, received - 1);
                await ReportToLog($"Got nickname {clientNickname}");
                Clients.Add(client);
                Nicknames.Add(clientNickname);
                PrintToChat($"{i + 1} / {clientNumber}");
            }
            AnnounceToChat("Listening...");
            await Listen();
        }

        private void PrintToChat(string message)
        {
            ChatBox.Text += message + '\n';
        }

        private void AnnounceToChat(string message)
        {
            ChatBox.Text += "____________________________________________" + '\n';
            ChatBox.Text += message + '\n';
            ChatBox.Text += "____________________________________________" + '\n';
        }

        private async Task ReportToLog(string message)
        {
            var bytes = Encoding.UTF8.GetBytes($"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.ffff")}]: {message}\n");
            await _logFile.WriteAsync(bytes, 0, bytes.Length);
            await _logFile.FlushAsync();
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            ReportToLog("Closing server");
        }
    }
}