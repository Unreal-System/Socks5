using System;
using System.Net.Sockets;

namespace Socks5
{
    /// <summary>
    /// 当客户端与服务器断开连接时调用的方法
    /// </summary>
    /// <param name="client">客户端</param>
    internal delegate void DestroyDelegate(Socks5Client client);
    internal class Socks5Client : IDisposable
    {

        #region 变量
        /// <summary>
        /// 记录客户端的Socket连接
        /// </summary>
        public Socket _ClientSocket;
        /// <summary>
        /// 记录转发目的地的Socket连接
        /// </summary>
        public Socket _DestinationSocket;
        /// <summary>
        /// 当客户端销毁时,保存调用方法的地址
        /// </summary>
        public DestroyDelegate _Destroyer;
        /// <summary>
        /// 用来存储客户端所有传入数据的缓冲区
        /// </summary>
        public Byte[] _Buffer = new Byte[4096];
        /// <summary>
        /// 获取或设置必须使用身份验证
        /// </summary>
        public Boolean _MustAuthenticate = false;
        /// <summary>
        /// 代理服务器上的身份验证集合
        /// </summary>
        public AuthenticationList _AuthList;
        /// <summary>
        /// 获取或设置在客户端通信时要使用的Socks处理程序
        /// </summary>
        public Socks5Handler _Handler;
        /// <summary>
        /// 存储所有代理时访问服务器返回的数据
        /// </summary>
        private Byte[] _RemoteBuffer = new Byte[1024];

        private static LogMain Log = null;
        #endregion

        #region 方法
        internal Socks5Client(Socket clientSocket, DestroyDelegate destroy, AuthenticationList authList, ref LogMain log)
        {
            _ClientSocket = clientSocket;
            _Destroyer = destroy;
            _AuthList = authList;
            Log = log;
        }

        internal void StartHandshake()
        {
            try
            {
                _ClientSocket.BeginReceive(_Buffer, 0, 1, SocketFlags.None, new AsyncCallback(this.OnStartSocksProtocol), _ClientSocket);
            } catch (Exception ex) { Log.AddLog(ex.Message); Dispose(); }
        }

        /// <summary>
        /// 当Socks5协议结束时使用,如果Socks5认证成功就开始中继数据
        /// </summary>
        /// <param name="Success"></param>
        /// <param name="Remote"></param>
        private void OnEndSocksProtocol(Boolean Success, Socket Remote)
        {
            _DestinationSocket = Remote;
            if (Success) StartRelay();
            else Dispose();
        }

        /// <summary>
        /// 开始中继数据
        /// </summary>
        private void StartRelay()
        {
            try
            {
                _ClientSocket.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnClientReceive), _ClientSocket);
                _DestinationSocket.BeginReceive(_RemoteBuffer, 0, _RemoteBuffer.Length, SocketFlags.None, new AsyncCallback(this.OnRemoteReceive), _DestinationSocket);
            } catch (Exception ex) { Log.AddLog("完成Socket5认证后开始中继数据时出错|" + ex.Message); Dispose(); }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try { _ClientSocket.Shutdown(SocketShutdown.Both); } catch { }
            try { _DestinationSocket.Shutdown(SocketShutdown.Both); } catch { }
            if (_ClientSocket != null) _ClientSocket.Close();
            if (_DestinationSocket != null) _DestinationSocket.Close();
            //Clean Up
            _ClientSocket = null;
            _DestinationSocket = null;
            if (_Destroyer != null) _Destroyer(this);
            GC.Collect(0);
        }
        #endregion

        #region 委托
        /// <summary>
        /// 异步处理从客户端接收的数据
        /// </summary>
        /// <param name="ar"></param>
        private void OnStartSocksProtocol(IAsyncResult ar)
        {
            int Ret;
            try
            {
                Ret = _ClientSocket.EndReceive(ar);
                if (Ret <= 0) { Dispose(); return; }
                if (_Buffer[0] == 5)
                {
                    if (_MustAuthenticate && _AuthList == null) { Dispose(); return; }
                    _Handler = new Socks5Handler(_ClientSocket, new NegotiationCompleteDelegate(this.OnEndSocksProtocol), _AuthList, ref Log);
                } else { Dispose(); return; }
                _Handler.StartNegotiating();
            } catch (Exception ex) { Log.AddLog("Socks5Client异步处理客户端发来的数据时出错|" + ex.Message); Dispose(); }
        }

        /// <summary>
        /// 当收到客户端的数据时调用
        /// </summary>
        /// <param name="ar"></param>
        private void OnClientReceive(IAsyncResult ar)
        {
            try
            {
                int Ret = _ClientSocket.EndReceive(ar);
                if (Ret <= 0) { Dispose(); return; }
                _DestinationSocket.BeginSend(_Buffer, 0, Ret, SocketFlags.None, new AsyncCallback(this.OnRemoteSent), _DestinationSocket);
            } catch (Exception ex) { Log.AddLog("Socks5Client处理数据调用时出错|" + ex.Message); Dispose(); }
        }

        /// <summary>
        /// 数据转发到目标时调用(这里的目标是客户端要访问的xxx)
        /// </summary>
        /// <param name="ar"></param>
        private void OnRemoteSent(IAsyncResult ar)
        {
            try
            {
                int Ret = _DestinationSocket.EndSend(ar);
                if (Ret > 0)
                {
                    _ClientSocket.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnClientReceive), _ClientSocket);
                    return;
                }
            } catch (Exception ex) { Log.AddLog("Socks5Client处理数据转发到目标的调用时出错|" + ex.Message); } Dispose();
        }

        /// <summary>
        /// 当收到目标返回的数据时调用
        /// </summary>
        /// <param name="ar"></param>
        private void OnRemoteReceive(IAsyncResult ar)
        {
            try
            {
                int Ret = _DestinationSocket.EndReceive(ar);
                if (Ret <= 0) { Dispose(); return; }
                _ClientSocket.BeginSend(_RemoteBuffer, 0, Ret, SocketFlags.None, new AsyncCallback(this.OnClientSent), _ClientSocket);
            } catch (Exception ex) { Log.AddLog("Socks5Client处理目标数据返回调用时出错|" + ex.Message); }
            Dispose();
        }

        /// <summary>
        /// 当我们向客户端发送数据时调用
        /// </summary>
        /// <param name="ar"></param>
        private void OnClientSent(IAsyncResult ar)
        {
            try
            {
                int Ret = _ClientSocket.EndSend(ar);
                if (Ret > 0)
                {
                    _DestinationSocket.BeginReceive(_RemoteBuffer, 0, _RemoteBuffer.Length, SocketFlags.None, new AsyncCallback(this.OnRemoteReceive), _DestinationSocket); return;
                }
            } catch (Exception ex) { Log.AddLog("Socks5Client处理向客户端发送数据时调用出错|" + ex.Message); } Dispose();
        }
        #endregion

    }
}