using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;

public class Server
{
    private static MySqlConnection conn;
    private static List<SocketInfo> sckList = new List<SocketInfo>();
    private static SocketInfo serverSock;
    private static int clientCount = 0;
    private const int MaxSize = 1024;
    private const int MaxClient = 3;

    public struct SocketInfo
    {
        public Socket Sck;
        public string User;
    }

    public static void Main(string[] args)
    {
        // 데이터베이스 서버 연결
        try
        {
            string connStr = "server=127.0.0.1;user=user;database=chatting_project;port=3306;password=1234";
            conn = new MySqlConnection(connStr);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand("set names euckr", conn);
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Console.WriteLine("Could not connect to server. Error message: " + ex.Message);
            Environment.Exit(1);
        }

        // 소켓 서버 초기화
        serverSock = new SocketInfo
        {
            Sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
            User = "server"
        };

        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
        serverSock.Sck.Bind(endPoint);
        serverSock.Sck.Listen(MaxClient);

        Console.WriteLine("Server On");

        // 클라이언트 연결 관리
        Thread[] clientThreads = new Thread[MaxClient];
        for (int i = 0; i < MaxClient; i++)
        {
            clientThreads[i] = new Thread(AddClient);
            clientThreads[i].Start();
        }

        for (int i = 0; i < MaxClient; i++)
        {
            clientThreads[i].Join();
        }

        serverSock.Sck.Close();
        conn.Close();
    }

    // DB 저장
    private static void Insert(string from, string to, string msg)
    {
        try
        {
            string query = "INSERT INTO chat(id_from, id_to, message) VALUES(@from, @to, @msg)";
            MySqlCommand cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);
            cmd.Parameters.AddWithValue("@msg", msg);

            //Console.WriteLine($"Inserting: from='{from}', to='{to}', msg='{msg}'");
            cmd.ExecuteNonQuery();
            //Console.WriteLine("Insert successful");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine("DB Insert Error: " + ex.Message);
            Console.WriteLine("Query: " + ex.ToString());
        }
    }

    private static void AddClient()
    {
        byte[] buffer = new byte[MaxSize];
        SocketInfo newClient = new SocketInfo();

        newClient.Sck = serverSock.Sck.Accept();
        int received = newClient.Sck.Receive(buffer);
        newClient.User = Encoding.UTF8.GetString(buffer, 0, received);

        lock (sckList)
        {
            sckList.Add(newClient);
        }

        string msg = "[공지] " + newClient.User + " 님이 입장했습니다.";
        Console.WriteLine(msg);
        SendMsg(msg, "");

        Thread recvThread = new Thread(() => RecvMsg(sckList.IndexOf(newClient)));
        recvThread.Start();

        lock (sckList)
        {
            clientCount++;
        }

        Console.WriteLine("[공지] 현재 접속자 수 : " + clientCount + "명");
    }

    // 모든 클라이언트에게 메세지 보내기
    private static void SendMsg(string msg, string to)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);

        lock (sckList)
        {
            foreach (SocketInfo client in sckList)
            {
                if (string.IsNullOrEmpty(to) || client.User == to)
                {
                    client.Sck.Send(data);
                }
            }
        }
    }

    private static void RecvMsg(int idx)
    {
        byte[] buffer = new byte[MaxSize];

        while (true)
        {
            try
            {
                int received = sckList[idx].Sck.Receive(buffer);
                if (received == 0) break;

                string msg = Encoding.UTF8.GetString(buffer, 0, received);
                string from = sckList[idx].User;
                string to = "";
                if (msg.StartsWith("->"))
                {
                    string[] parts = msg.Split(' ');
                    to = parts[1];
                    msg = string.Join(" ", parts, 2, parts.Length - 2);
                }

                if (msg.StartsWith(" "))
                {
                    msg = msg.TrimStart();
                }

                Insert(from, to, msg);

                msg = from + " : " + msg;
                SendMsg(msg, to);
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Socket Error: " + ex.Message);
                break;
            }
        }

        string leaveMsg = "[공지] " + sckList[idx].User + " 님이 퇴장했습니다.";
        Console.WriteLine(leaveMsg);
        SendMsg(leaveMsg, "");
        DelClient(idx);
    }

    private static void DelClient(int idx)
    {
        lock (sckList)
        {
            sckList[idx].Sck.Close();
            sckList.RemoveAt(idx);
            clientCount--;
        }
    }
}
