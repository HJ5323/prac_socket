using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;

class Program
{
    private const int MaxSize = 1024;
    private const int Enter = 13;
    private const int BackSpace = 8;
    private static Socket clientSocket;
    private static string myId;
    private static MySqlConnection conn;

    static void Main(string[] args)
    {
        // 데이터베이스 서버 연결
        try
        {
            string connStr = "server=127.0.0.1;user=;database=chatting_project;port=3306;password=1234";
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

        // 로그인 또는 회원가입
        while (true)
        {
            bool isSignup = false;

            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("---------------------------로그인-------------------------");
            Console.WriteLine("----------------------------------------------------------");

            while (true)
            {
                string id = "", pw = "";
                Console.Write("아이디를 입력하세요: (아이디가 없을 경우 signup 입력) ");
                id = Console.ReadLine();
                if (id == "signup")
                {
                    isSignup = true;
                    break;
                }

                Console.Write("비밀번호를 입력하세요: ");
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter) break;
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (pw.Length > 0)
                        {
                            pw = pw.Substring(0, pw.Length - 1);
                            Console.Write("\b \b");
                        }
                    }
                    else
                    {
                        pw += key.KeyChar;
                        Console.Write("*");
                    }
                }
                Console.WriteLine();

                if (Login(id, pw))
                {
                    myId = id;
                    break;
                }
                else
                {
                    Console.WriteLine("아이디 또는 비밀번호가 틀렸습니다.");
                    Console.WriteLine("----------------------------------------------------------");
                }
            }

            if (isSignup)
            {
                Signup();
                continue;
            }
            break;
        }

        // 콘솔 창 클리어
        Console.Clear();

        // 채팅 내역 불러오기
        ChatHistory();
        Console.WriteLine("----------------------------------------------------------");

        // 소켓 초기화 및 연결
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 7777);

        while (true)
        {
            try
            {
                clientSocket.Connect(endPoint);
                byte[] idBuffer = Encoding.UTF8.GetBytes(myId);
                clientSocket.Send(idBuffer);
                break;
            }
            catch (SocketException)
            {
                Console.WriteLine("Connecting...");
            }
        }

        Thread recvThread = new Thread(ChatRecv);
        recvThread.Start();

        while (true)
        {
            string text = Console.ReadLine();
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            clientSocket.Send(buffer);
        }
    }

    // 로그인
    static bool Login(string id, string pw)
    {
        string query = "SELECT * FROM user WHERE id = @id AND pw = @pw";
        MySqlCommand cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pw", pw);

        MySqlDataReader rdr = cmd.ExecuteReader();
        bool isSuccess = rdr.HasRows;
        rdr.Close();
        return isSuccess;
    }

    // 아이디 중복 체크
    static bool IdCheck(string id)
    {
        string query = "SELECT * FROM user WHERE id = @id";
        MySqlCommand cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", id);

        MySqlDataReader rdr = cmd.ExecuteReader();
        bool isExist = rdr.HasRows;
        rdr.Close();
        return isExist;
    }

    // 회원가입
    static void Signup()
    {
        string id, pw;
        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine("--------------------------회원가입------------------------");
        Console.WriteLine("----------------------------------------------------------");

        while (true)
        {
            Console.Write("아이디를 입력하세요: ");
            id = Console.ReadLine();
            if (!IdCheck(id)) break;
            Console.WriteLine("이미 존재하는 아이디입니다.");
        }
        Console.Write("비밀번호를 입력하세요: ");
        pw = Console.ReadLine();

        string query = "INSERT INTO user(id, pw) VALUES(@id, @pw)";
        MySqlCommand cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pw", pw);
        cmd.ExecuteNonQuery();

        Console.WriteLine("회원가입이 완료되었습니다.");
    }

    // 채팅 내역 불러오기
    static void ChatHistory()
    {
        string query = "SELECT * FROM chat";
        MySqlCommand cmd = new MySqlCommand(query, conn);
        MySqlDataReader rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            string from = rdr.GetString(1);
            string to = rdr.GetString(2);
            string msg = rdr.GetString(3);

            if (string.IsNullOrEmpty(to))
                Console.WriteLine($"{from} : {msg}");
            else
                Console.WriteLine($"{from}({to}) : {msg}");
        }

        rdr.Close();
    }

    // 서버로부터 받은 메세지 출력
    static void ChatRecv()
    {
        byte[] buffer = new byte[MaxSize];
        while (true)
        {
            try
            {
                int received = clientSocket.Receive(buffer);
                if (received > 0)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, received);
                    if (!msg.StartsWith(myId))
                    {
                        Console.WriteLine(msg);
                    }
                }
                else
                {
                    Console.WriteLine("Server Off");
                    clientSocket.Close();
                    break;
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Server Off");
                clientSocket.Close();
                break;
            }
        }
    }
}

