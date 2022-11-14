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

namespace AOSSocketChatWPF.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket Client;
        private IPEndPoint _endPoint;
        private FileStream _logFile;
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectToServerButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (Client != null && Client.Connected) return;
            IPAddress ipAddress;
            if (ServerIpBox.Text.Trim() == "")
                ipAddress = IPAddress.Parse("127.0.0.1");
            else
            {
                if (!IPAddress.TryParse(ServerIpBox.Text, out ipAddress)) return;
            }

            int variant = 4;
            _endPoint = new(ipAddress, 1025 + variant);
            try
            {
                Client = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                AnnounceToChat("Connecting...");
                await Client.ConnectAsync(_endPoint);
                AnnounceToChat("Connected");
                
                Directory.CreateDirectory($@"{Directory.GetCurrentDirectory()}\log");
                var path = @$"{Directory.GetCurrentDirectory()}\log\{NicknameBox.Text}.txt";
                _logFile = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                ReportToLog($"Connected to {ipAddress}:{1025 + variant} with nickname: {NicknameBox.Text}");
                
                var nicknameBytes = Encoding.UTF8.GetBytes(NicknameBox.Text);
                var nicknameCommand = new List<byte> {(byte) 'n'}; // Nickname
                nicknameCommand.AddRange(nicknameBytes);
                await Client.SendAsync(nicknameCommand.ToArray(), SocketFlags.None);
                ReportToLog($"Sent nickname");
                ConnectionContainer.Visibility = Visibility.Collapsed;
                await ReceiveMessages();
            }
            catch (Exception excp)
            {
                AnnounceToChat(excp.Message);
            }
        }
        
        private async void SendButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (Client == null || !Client.Connected) return;
            
            var messageBoxText = MessageBox.Text;
            var messageBytes = Encoding.UTF8.GetBytes(messageBoxText);
            
            var messageCount = (messageBytes.Length - 1) / 20 + 1;
            var messageCountBytes = Encoding.UTF8.GetBytes(messageCount.ToString());
            var messageCountCommand = new List<byte>{(byte)'c'}; // Count
            messageCountCommand.AddRange(messageCountBytes);
            
            await Client.SendAsync(messageCountCommand.ToArray(), SocketFlags.None);
            await ReportToLog($"Sent message count : ({messageCount})");
            for (int i = 0; i < messageCount; i++)
            {
                var mBytes = messageBytes.Skip(i * 20).Take(20).ToArray();
                List<byte> shortMessageBytes = new List<byte>() {(byte) 'm'}; // Message
                shortMessageBytes.AddRange(mBytes);
                await Client.SendAsync(shortMessageBytes.ToArray(), SocketFlags.None);
                await ReportToLog($"Sent message ({i + 1}) : {Encoding.UTF8.GetString(mBytes)}");
            }
            MessageBox.Text = "";
        }

        private async Task ReceiveMessages()
        {
            await ReportToLog("Listening for messages");
            while (true)
            {
                var buffer = new byte[256];
                var received = await Client.ReceiveAsync(buffer, SocketFlags.None);
                if (buffer[0] == 'd') // Disable
                {
                    AnnounceToChat("Server closed.");
                    await ReportToLog("Got disable command, server was closed");
                    Client.Disconnect(false);
                    ConnectionContainer.Visibility = Visibility.Visible;
                    return;
                }
                if (buffer[0] == 'c') // message Count
                {
                    var messageCountResponse = Encoding.UTF8.GetString(buffer, 0, received);
                    int messageCount;
                    if (!Int32.TryParse(new string(messageCountResponse.Skip(1).ToArray()), out messageCount))
                        continue;
                    await ReportToLog($"Got message count : ({messageCount})");
                    List<string> messageContents = new List<string>();
                    for (int i = 0; i < messageCount; i++)
                    {
                        var messageContentBytes = new byte[21];
                        var n = await Client.ReceiveAsync(messageContentBytes, SocketFlags.None);
                        var messageContent = Encoding.UTF8.GetString(messageContentBytes, 0, n);
                        await ReportToLog($"Got message ({i + 1}) : {messageContent.Substring(1)}");
                        messageContents.Add(messageContent);
                    }
                    var nicknameBytesLength = await Client.ReceiveAsync(buffer, SocketFlags.None);
                    var nickname = Encoding.UTF8.GetString(buffer.Skip(1).ToArray(), 0, nicknameBytesLength - 1);
                    await ReportToLog($"Got nickname : {nickname}");
                    foreach (var messageContent in messageContents)
                    {
                        var message = $"{nickname}: {messageContent.Substring(1)}";
                        PrintToChat(message);
                    }
                }
            }
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
        
        private async void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            await ReportToLog("Client closed");
            await Client.DisconnectAsync(false);
        }
    }
}