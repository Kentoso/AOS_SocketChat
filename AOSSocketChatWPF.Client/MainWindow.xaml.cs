using System;
using System.Collections.Generic;
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
        public MainWindow()
        {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            int variant = 4;
            _endPoint = new(ipAddress, 1025 + variant);
            Client = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            InitializeComponent();
        }

        private async void SendButton_OnClick(object sender, RoutedEventArgs e)
        {
            var messageBoxText = MessageBox.Text;
            var messageBytes = Encoding.UTF8.GetBytes(messageBoxText);
            await Client.SendAsync(messageBytes, SocketFlags.None);
        }

        private async void ConnectToServerButton_OnClick(object sender, RoutedEventArgs e)
        {
            AnnounceToChat("Connecting...");
            await Client.ConnectAsync(_endPoint);
            AnnounceToChat("Connected");
            var nicknameBytes = Encoding.UTF8.GetBytes(NicknameBox.Text);
            await Client.SendAsync(nicknameBytes, SocketFlags.None);
            await ReceiveMessages();
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

        private async Task ReceiveMessages()
        {
            while (true)
            {
                var buffer = new byte[256];
                var received = await Client.ReceiveAsync(buffer, SocketFlags.None);
                var response = Encoding.UTF8.GetString(buffer, 0, received);
                PrintToChat(response);
            }
        }
    }
}