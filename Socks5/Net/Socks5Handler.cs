using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Socks5
{
    /// <summary>
    /// 定义Socks5协商完成时调用的方法
    /// </summary>
    /// <param name="Success">指示协商是否成功</param>
    /// <param name="Remote">与远程服务器的连接</param>
    internal delegate void NegotiationCompleteDelegate(Boolean Success, Socket Remote);
    internal class Socks5Handler
    {

        #region 变量
        /// <summary>
        /// 获取和设置与客户端的连接
        /// </summary>
        private Socket _Connection;
        /// <summary>
        /// 从客户端接收字节时使用的缓冲区
        /// </summary>
        private Byte[] _Buffer = new Byte[1024];
        /// <summary>
        /// 获取或设置用于接受传入连接的套接字
        /// </summary>
        private Socket _AcceptSocket;
        /// <summary>
        /// 在Socks5验证完成时进行保存要调用的方法的地址
        /// </summary>
        private NegotiationCompleteDelegate _Signaler;
        /// <summary>
        /// 获取或设置与远程主机的连接
        /// </summary>
        private Socket _RemoteConnection;
        /// <summary>
        /// 获取或设置可用于存储客户端接收到的字节的内容数组
        /// </summary>
        private Byte[] _Bytes;
        /// <summary>
        /// 获取或设置在尝试验证Socks5客户端时使用的字典集合
        /// </summary>
        private AuthenticationList _AuthList;
        /// <summary>
        /// 验证Socks客户端时使用的AuthBase认证类
        /// </summary>
        private AuthBase _AuthMethod;
        private Boolean _NoAuth = false; // ???
        private LogMain Log = null;
        #endregion

        #region 方法
        internal Socks5Handler(Socket ClientConnection, NegotiationCompleteDelegate Callback, AuthenticationList AuthList, ref LogMain log)
        {
            if (Callback == null) return;
            this._Connection = ClientConnection;
            this._Signaler = Callback;
            this._AuthList = AuthList;
            this.Log = log;
        }

        /// <summary>
        /// 开始从客户端接收字节
        /// </summary>
        internal void StartNegotiating()
        {
            try
            {
                _Connection.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveBytes), _Connection);
            } catch (Exception ex) { Log.AddLog("Socks5Handler在处理接收字节时出错|" + ex.Message); Dispose(false); }
        }

        /// <summary>
        /// 添加字节到字节数组
        /// </summary>
        /// <param name="NewBytes"></param>
        /// <param name="Cnt"></param>
        private void AddBytes(Byte[] NewBytes, Int32 Cnt)
        {
            if (Cnt <= 0 || NewBytes == null || Cnt > NewBytes.Length) return;
            if (_Bytes == null) _Bytes = new Byte[Cnt];
            else
            {
                Byte[] tmp = _Bytes;
                _Bytes = new Byte[_Bytes.Length + Cnt];
                Array.Copy(tmp, 0, _Bytes, 0, tmp.Length);
            }
            Array.Copy(NewBytes, 0, _Bytes, _Bytes.Length - Cnt, Cnt);
        }

        /// <summary>
        /// 检查特定的请求是否是有效的Socks5请求
        /// </summary>
        /// <param name="Request"></param>
        /// <returns></returns>
        private Boolean IsValidRequest(Byte[] Request)
        {
            try { return (Request.Length == Request[0] + 1); }
            catch (Exception ex) { Log.AddLog("Socks5Handler在检查特定请求是否有效时出错|" + ex.Message); return false; }
        }

        /// <summary>
        /// 处理客户端发出的Socks5请求并确定认证方法
        /// </summary>
        /// <param name="Request"></param>
        private void ProcessRequest(Byte[] Request)
        {
            try
            {
                byte Ret = 255;
                for (int Cnt = 1; Cnt < Request.Length; Cnt++)//验证账户逻辑
                {
                    if (Request[Cnt] == 0)
                    {
                        if (_AuthMethod == null) _AuthMethod = new AuthBase(ref Log);
                        Ret = 0;
                        _NoAuth = true;
                        break;
                    }
                    //if (Request[Cnt] == 0 && AuthList == null)
                    //{//从不无认证 (就是永远不认证(Socks4))
                    //    //Ret = 0;
                    //    //AuthMethod = new AuthNone();//????
                    //    break;
                    //}
                    else if (Request[Cnt] == 2 && _AuthList != null)
                    {
                        Ret = 2;
                        _AuthMethod = new AuthBase(_AuthList, ref Log);
                        if (_AuthList != null) break;
                    } //else { break; }
                }
                _Connection.BeginSend(new Byte[] { 5, Ret }, 0, 2, SocketFlags.None, new AsyncCallback(this.OnAuthSent), _Connection);
            } catch (Exception ex) { Log.AddLog("Socks5Handler在处理确定认证时出错|"  + ex.Message); Dispose(false); }
        }

        /// <summary>
        /// 检查认证是否成功
        /// </summary>
        /// <param name="Success"></param>
        private void OnAuthenticationComplete(Boolean Success)
        {
            try
            {
                if (Success)
                {
                    _Bytes = null;
                    _Connection.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnRecvRequest), _Connection); // 收发代理中的数据
                } else { Dispose(false); }
            }
            catch (Exception ex) { Console.WriteLine("Socks5Handler在检查认证是否成功时出错|" + ex.Message); Dispose(false); }
        }

        /// <summary>
        /// 检查查询是否有效
        /// </summary>
        /// <param name="Query"></param>
        /// <returns></returns>
        private bool IsValidQuery(Byte[] Query)
        {
            try
            {
                switch (Query[3])
                {
                    case 1: return (Query.Length == 10);
                    case 3: return (Query.Length == Query[4] + 7);
                    case 4: Dispose(8); return false;
                    default: Dispose(false); return false;
                }
            }
            catch (Exception ex) { Log.AddLog("Socks5Handler在检查查询是否有效时出错|" + ex.Message); return false; }
        }

        /// <summary>
        /// 处理查询
        /// </summary>
        /// <param name="Query"></param>
        private void ProcessQuery(Byte[] Query)
        {
            try
            {
                switch (Query[1])
                {
                    case 1: //CONNECT
                        IPAddress RemoteIP = null;
                        int RemotePort = 0;
                        if (Query[3] == 1)
                        {
                            RemoteIP = IPAddress.Parse(Query[4].ToString() + "." + Query[5].ToString() + "." + Query[6].ToString() + "." + Query[7].ToString());
                            RemotePort = Query[8] * 256 + Query[9];
                        }
                        else if (Query[3] == 3)
                        {
                            RemoteIP = Dns.Resolve(Encoding.ASCII.GetString(Query, 5, Query[4])).AddressList[0];
                            RemotePort = Query[4] + 5;
                            RemotePort = Query[RemotePort] * 256 + Query[RemotePort + 1];
                        }
                        _RemoteConnection = new Socket(RemoteIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        _RemoteConnection.BeginConnect(new IPEndPoint(RemoteIP, RemotePort), new AsyncCallback(this.OnConnected), _RemoteConnection);//0
                        break;
                    case 2: //BIND
                        byte[] Reply = new byte[10];
                        long LocalIP = Socks5Listener.GetLocalExternalIP().Address;
                        _AcceptSocket = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        _AcceptSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                        _AcceptSocket.Listen(50);
                        Reply[0] = 5;  //Version 5
                        Reply[1] = 0;  //Everything is ok :)
                        Reply[2] = 0;  //Reserved
                        Reply[3] = 1;  //We're going to send a IPv4 address
                        Reply[4] = (byte)(Math.Floor((LocalIP % 256d)));  //IP Address/1
                        Reply[5] = (byte)(Math.Floor((LocalIP % 65536d) / 256));  //IP Address/2
                        Reply[6] = (byte)(Math.Floor((LocalIP % 16777216d) / 65536));  //IP Address/3
                        Reply[7] = (byte)(Math.Floor(LocalIP / 16777216d));  //IP Address/4
                        Reply[8] = (byte)(Math.Floor(((IPEndPoint)_AcceptSocket.LocalEndPoint).Port / 256d));  //Port/1
                        Reply[9] = (byte)(((IPEndPoint)_AcceptSocket.LocalEndPoint).Port % 256d);  //Port/2
                        _Connection.BeginSend(Reply, 0, Reply.Length, SocketFlags.None, new AsyncCallback(this.OnStartAccept), _Connection);//0
                        break;
                    case 3: //ASSOCIATE
                            //ASSOCIATE is not implemented (yet?)
                        Dispose(7);
                        break;
                    default:
                        Dispose(7);
                        break;
                }
            }
            catch (Exception ex) { Log.AddLog("Socks5Handler在处理查询时出错|" + ex.Message); Dispose(1); }
        }

        private void Dispose(Boolean Success)
        {
            if (_AcceptSocket != null) _AcceptSocket.Close();
            _Signaler(Success, _RemoteConnection); GC.Collect(0);
        }

        private void Dispose(Byte Value)
        {
            byte[] ToSend;
            try
            {
                ToSend = new byte[]{5, Value, 0, 1,
                        (byte)(((IPEndPoint)_RemoteConnection.LocalEndPoint).Address.Address % 256),
                        (byte)(Math.Floor((((IPEndPoint)_RemoteConnection.LocalEndPoint).Address.Address % 65536d) / 256)),
                        (byte)(Math.Floor((((IPEndPoint)_RemoteConnection.LocalEndPoint).Address.Address % 16777216d) / 65536)),
                        (byte)(Math.Floor(((IPEndPoint)_RemoteConnection.LocalEndPoint).Address.Address / 16777216d)),
                        (byte)(Math.Floor(((IPEndPoint)_RemoteConnection.LocalEndPoint).Port / 256d)),
                        (byte)(((IPEndPoint)_RemoteConnection.LocalEndPoint).Port % 256d)};
            }
            catch { ToSend = new byte[] { 5, 1, 0, 1, 0, 0, 0, 0, 0, 0 }; }
            try
            {
                _Connection.BeginSend(ToSend, 0, ToSend.Length, SocketFlags.None, (AsyncCallback)(ToSend[1] == 0 ? new AsyncCallback(this.OnDisposeGood) : new AsyncCallback(this.OnDisposeBad)), _Connection);
            } catch (Exception ex) { Log.AddLog("Socks5Handler在释放资源(第二个)时出错|" + ex.Message); Dispose(false); }
        }
        #endregion

        #region 委托
        /// <summary>
        /// 处理接收字节
        /// </summary>
        /// <param name="ar"></param>
        private void OnReceiveBytes(IAsyncResult ar)
        {
            try
            {
                int Ret = _Connection.EndReceive(ar);
                if (Ret <= 0) Dispose(false);
                AddBytes(_Buffer, Ret);
                if (IsValidRequest(_Bytes)) ProcessRequest(_Bytes);
                else _Connection.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveBytes), _Connection);
            } catch (Exception ex) { Log.AddLog("Socks5Handler在处理接收字节时出错|" + ex.Message); Dispose(false); }
        }

        /// <summary>
        /// 处理客户端请求认证
        /// </summary>
        /// <param name="ar"></param>
        private void OnAuthSent(IAsyncResult ar)
        {
            try
            {
                if (_NoAuth == true)
                {
                    if (_Connection.EndSend(ar) <= 0) { Dispose(false); return; }
                    _AuthMethod.StartAuthentication(_Connection, new AuthenticationCompleteDelegate(this.OnAuthenticationComplete), _NoAuth);
                }
                else
                {
                    if (_Connection.EndSend(ar) <= 0 || _AuthMethod == null) { Dispose(false); return; }
                    _AuthMethod.StartAuthentication(_Connection, new AuthenticationCompleteDelegate(this.OnAuthenticationComplete));
                }
            }
            catch (Exception ex) { Log.AddLog("Socks5Handler在处理客户端请求认证时出错|" + ex.Message); Dispose(false); }
        }

        /// <summary>
        /// 处理客户端请求
        /// </summary>
        /// <param name="ar"></param>
        private void OnRecvRequest(IAsyncResult ar)
        {
            try
            {
                int Ret = _Connection.EndReceive(ar);
                if (Ret <= 0) { Dispose(false); return; }
                AddBytes(_Buffer, Ret);
                if (IsValidQuery(_Bytes)) ProcessQuery(_Bytes);
                else _Connection.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnRecvRequest), _Connection);
            } catch (Exception ex) { Log.AddLog("Socks5Handler在处理客户端请求时出错3|" + ex.Message); Dispose(false); }
        }

        /// <summary>
        /// 答复客户端时调用清理资源?
        /// </summary>
        /// <param name="ar"></param>
        private void OnDisposeGood(IAsyncResult ar)
        {
            try { if (_Connection.EndSend(ar) > 0) { Dispose(true); return; } }
            catch (Exception ex) { Log.AddLog("Socks5Handler在答复客户端时调用清理资源时出错|" + ex.Message); } Dispose(false);
        }

        /// <summary>
        /// 当客户端否定了回复时处理
        /// </summary>
        /// <param name="ar"></param>
        private void OnDisposeBad(IAsyncResult ar)
        {
            try { _Connection.EndSend(ar); }
            catch (Exception ex) { Console.WriteLine("Socks5Handler在处理否定回复时出错|" + ex.Message); }
            Dispose(false);
        }

        /// <summary>
        /// 成功连接到远程主机时调用
        /// </summary>
        /// <param name="ar"></param>
        private void OnConnected(IAsyncResult ar)
        {
            try { _RemoteConnection.EndConnect(ar); { Dispose(0); } }
            catch (Exception ex) { Log.AddLog("Socks5Handler在成功连接到原创主机时调用出错|" + ex.Message);/*Console.WriteLine("Socks5Handler:347:Error:" + DateTime.Now + "|" + ex.Message);*/ Dispose(1); }
        }

        /// <summary>
        /// 当AcceptSocket应该传入连接时的处理
        /// </summary>
        /// <param name="ar"></param>
        private void OnStartAccept(IAsyncResult ar)
        {
            try
            {
                if (_Connection.EndSend(ar) <= 0) Dispose(false);
                else _AcceptSocket.BeginAccept(new AsyncCallback(this.OnAccept), _AcceptSocket);//0
            }
            catch (Exception ex) { Log.AddLog("Socks5Handler在处理传入连接时出错|" + ex.Message); Dispose(false); }
        }

        /// <summary>
        /// 处理传入到AcceptSocket队列中的连接
        /// </summary>
        /// <param name="ar"></param>
        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                _RemoteConnection = _AcceptSocket.EndAccept(ar);
                _AcceptSocket.Close();
                _AcceptSocket = null;
                Dispose(0);
            }
            catch (Exception ex) { Log.AddLog("Socks5Handler在处理传入到AcceptSocket队列时出错|" + ex.Message); Dispose(1); }
        }
        #endregion

    }
}