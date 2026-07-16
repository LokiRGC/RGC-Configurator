using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RGC.Services
{
    public class RConService : IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private Thread? _listenThread;
        private int _nextId = 1;

        public bool IsConnected => _client?.Connected == true;
        public ObservableCollection<LogEntry> Log { get; } = new();

        public void Connect(string host, int port, string password)
        {
            Disconnect();

            try
            {
                _client = new TcpClient();
                _client.Connect(host, port);
                _stream = _client.GetStream();

                // Source RCON login: SERVERDATA_AUTH (Type=3)
                var authId = _nextId++;
                var passBytes = Encoding.UTF8.GetBytes(password);
                var packet = BuildPacket(authId, 3, passBytes);
                _stream.Write(packet, 0, packet.Length);
                _stream.Flush();

                // Read response: first response packet (SERVERDATA_RESPONSE_VALUE, empty body)
                var resp1 = ReadPacket();
                // Read second response packet (SERVERDATA_AUTH_RESPONSE)
                var resp2 = ReadPacket();

                // Check which one is the auth response
                int authResultId;
                if (resp1 != null && resp1.Value.type == 2)
                    authResultId = resp1.Value.id;
                else if (resp2 != null && resp2.Value.type == 2)
                    authResultId = resp2.Value.id;
                else
                    authResultId = -2;

                if (authResultId == authId)
                {
                    Log.Add(new LogEntry($"[RCon] Подключено к {host}:{port}", Colors.LimeGreen));
                    StartListening();
                }
                else
                {
                    Log.Add(new LogEntry($"[RCon] Ошибка авторизации (неверный пароль)", Colors.Red));
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                Log.Add(new LogEntry($"[RCon] Ошибка: {ex.Message}", Colors.Red));
            }
        }

        public void Disconnect()
        {
            _listenThread?.Join(500);
            _listenThread = null;
            _stream?.Close();
            _client?.Close();
            _stream = null;
            _client = null;

            if (Log.Count == 0 || Log[^1].Text != "[RCon] Отключено")
                Log.Add(new LogEntry("[RCon] Отключено", Colors.Orange));
        }

        public void SendCommand(string command)
        {
            if (_stream == null || !IsConnected)
            {
                Log.Add(new LogEntry("[RCon] Нет подключения", Colors.Red));
                return;
            }

            try
            {
                var cmdId = _nextId++;
                var cmdBytes = Encoding.UTF8.GetBytes(command);
                var packet = BuildPacket(cmdId, 2, cmdBytes);
                _stream.Write(packet, 0, packet.Length);
                _stream.Flush();
                Log.Add(new LogEntry($"> {command}", Colors.Silver));
            }
            catch (Exception ex)
            {
                Log.Add(new LogEntry($"[RCon] Ошибка отправки: {ex.Message}", Colors.Red));
            }
        }

        private static byte[] BuildPacket(int id, int type, byte[] body)
        {
            // Size = 4 (id) + 4 (type) + body.Length + 2 (null terminators)
            var size = 4 + 4 + body.Length + 2;
            var packet = new byte[size + 4]; // +4 for the size field itself

            WriteInt32(packet, 0, size);          // Size field
            WriteInt32(packet, 4, id);             // ID
            WriteInt32(packet, 8, type);           // Type
            Buffer.BlockCopy(body, 0, packet, 12, body.Length); // Body
            // packet[size - 2] and [size - 1] are already 0x00 0x00

            return packet;
        }

        private (int id, int type, byte[] body)? ReadPacket()
        {
            if (_stream == null) return null;

            try
            {
                // Read size (4 bytes)
                var sizeBuf = new byte[4];
                var read = ReadExact(_stream, sizeBuf, 0, 4);
                if (read < 4) return null;

                var size = BitConverter.ToInt32(sizeBuf, 0);
                if (size < 8 || size > 65536) return null;

                // Read rest of packet (id + type + body + 2 null bytes)
                var data = new byte[size];
                read = ReadExact(_stream, data, 0, size);
                if (read < size) return null;

                var id = BitConverter.ToInt32(data, 0);
                var type = BitConverter.ToInt32(data, 4);
                var bodyLen = size - 10; // minus id(4) - type(4) - nulls(2)
                var body = new byte[Math.Max(0, bodyLen)];
                if (bodyLen > 0)
                    Buffer.BlockCopy(data, 8, body, 0, bodyLen);

                return (id, type, body);
            }
            catch
            {
                return null;
            }
        }

        private static int ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (total < count)
            {
                var read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0) break;
                total += read;
            }
            return total;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private void StartListening()
        {
            _listenThread = new Thread(() =>
            {
                try
                {
                    while (IsConnected && _stream != null)
                    {
                        var pkt = ReadPacket();
                        if (pkt == null) break;

                        var body = Encoding.UTF8.GetString(pkt.Value.body).TrimEnd('\0');
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                Log.Add(new LogEntry(body, Colors.DodgerBlue));
                            });
                        }
                    }
                }
                catch { }
                finally
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (IsConnected) Disconnect();
                    });
                }
            })
            { IsBackground = true };
            _listenThread.Start();
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
