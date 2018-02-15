using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;

namespace Socks5
{
    internal class Socks5Listener : IDisposable
    {

        #region 变量
        /// <summary>
        /// 服务器监听的Socket对象
        /// </summary>
        private Socket _ListenSocket = null;
        /// <summary>
        /// 服务器监听的地址
        /// </summary>
        private IPAddress _Address = null;
        /// <summary>
        /// 服务器监听的端口
        /// </summary>
        private Int32 _Port;
        /// <summary>
        /// 客户端列表
        /// </summary>
        private ArrayList _Clients = new ArrayList();
        /// <summary>
        /// 资源占用状态
        /// </summary>
        private Boolean _IsDisposed = false;
        /// <summary>
        /// 记录用户名和密码集合的对象
        /// </summary>
        private AuthenticationList _AuthList = null;
        /// <summary>
        /// 最大用户数
        /// </summary>
        private Int32 _MaxUser;

        private static LogMain Log = null;
        #endregion

        #region 方法
        /// <summary>
        /// 初始化服务器
        /// </summary>
        /// <param name="ipAddress">监听IP地址</param>
        /// <param name="port">监听的端口</param>
        /// <param name="maxUser">最大用户数</param>
        internal Socks5Listener(String ipAddress, Int32 port, Int32 maxUser, ref LogMain log)
        {
            try
            {
                if (Log == null) Log = log;
                if (_AuthList == null) _AuthList = new AuthenticationList();
                this._Address = IPAddress.Parse(ipAddress);
                this._Port = port;
                this._MaxUser = maxUser;
            } catch (Exception ex) { Log.AddLog("在初始化监听对象时出错|" + ex.Message); }
        }

        /// <summary>
        /// 开始监听服务器
        /// </summary>
        internal void StartListener()
         {
            try
            {
                _ListenSocket = new Socket(_Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _ListenSocket.Bind(new IPEndPoint(_Address, _Port));
                _ListenSocket.Listen(_MaxUser);
                _ListenSocket.BeginAccept(new AsyncCallback(this.OnAccept), _ListenSocket);
            } catch (Exception ex) { Log.AddLog("监听服务器时出错|" + ex.Message); _ListenSocket = null; return; }
        }

        /// <summary>
        /// 重启服务器
        /// </summary>
        internal void Restart() { if (_ListenSocket == null) return; _ListenSocket.Close(); StartListener(); }

        internal void Stop() { if (_ListenSocket == null) return; _ListenSocket.Close(); }

        /// <summary>
        /// 添加客户端
        /// </summary>
        /// <param name="client">客户端</param>
        internal void AddClient(Socks5Client client)
        {
            if (_Clients.IndexOf(client) == -1) _Clients.Add(client);
        }

        /// <summary>
        /// 删除客户端
        /// </summary>
        /// <param name="client">客户端</param>
        internal void DelClient(Socks5Client client)
        {
            _Clients.Remove(client);
        }

        /// <summary>
        /// 返回此计算机的外部地址
        /// </summary>
        /// <returns></returns>
        internal static IPAddress GetLocalExternalIP()
        {
            try
            {
                IPHostEntry IPHE = Dns.Resolve(Dns.GetHostName());
                for (int Cnt = 0; Cnt < IPHE.AddressList.Length; Cnt++)
                {
                    if (IsRemoteIP(IPHE.AddressList[Cnt])) return IPHE.AddressList[Cnt];
                }
                return IPHE.AddressList[0];
            }
            catch (Exception ex) { Log.AddLog(LogType.Error, ex.Message); return IPAddress.Any; }
        }

        /// <summary>
        /// 检查是否为远程IP
        /// </summary>
        /// <param name="IP">IPixz</param>
        /// <returns></returns>
        private static Boolean IsRemoteIP(IPAddress IP)
        {
            Byte First = (Byte)Math.Floor(IP.Address % 256d);
            Byte Second = (Byte)Math.Floor((IP.Address % 65536d) / 256);
            //Not 10.x.x.x And Not 172.16.x.x <-> 172.31.x.x And Not 192.168.x.x
            //而不是任何而不是环回而不是广播
            return (First != 10) &&
            (First != 172 || (Second < 16 || Second > 31)) &&
            (First != 192 || Second != 168) &&
            (!IP.Equals(IPAddress.Any)) &&
            (!IP.Equals(IPAddress.Loopback)) &&
            (!IP.Equals(IPAddress.Broadcast));
        }

        public void Dispose()
        {
            if (_IsDisposed) return;
            while (_Clients.Count > 0) ((Socks5Client)_Clients[0]).Dispose();
            try { _ListenSocket.Shutdown(SocketShutdown.Both); }
            catch (Exception ex) { Log.AddLog("释放资源时出错|" + ex.Message); }
            if (_ListenSocket != null) _ListenSocket.Close();
            _IsDisposed = true;
        }

        ~Socks5Listener() { Dispose(); }
        #endregion

        #region 委托
        /// <summary>
        /// 异步接收处理新连接的客户端
        /// </summary>
        /// <param name="ar"></param>
        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                var NewSocket = _ListenSocket.EndAccept(ar);
                if (NewSocket != null)
                {
                    var NewClient = new Socks5Client(NewSocket, new DestroyDelegate(this.DelClient), _AuthList, ref Log);
                    AddClient(NewClient);
                    NewClient.StartHandshake();
                }
            } catch (Exception ex) { Log.AddLog("新客户端接入处理时出错|" + ex.Message); }
            try { _ListenSocket.BeginAccept(new AsyncCallback(this.OnAccept), _ListenSocket); }
            catch (Exception ex) { Log.AddLog("新客户端接入处理时出错2|" + ex.Message); Dispose(); }
        }
        #endregion

    }
}