using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Socks5
{

    /// <summary>
    /// 定义认证完成时要调用的方法的签名
    /// </summary>
    /// <param name="Success"></param>
    internal delegate void AuthenticationCompleteDelegate(Boolean Success);

    internal class AuthBase
    {

        #region 变量
        private Socket _Connection;
        private AuthenticationCompleteDelegate _Callback;
        private Byte[] _Bytes;
        private Byte[] _Buffer = new Byte[1024];
        private AuthenticationList _AuthList;
        private StringDictionary _List;

        private static LogMain Log = null;
        #endregion

        #region 方法
        internal AuthBase(ref LogMain log) { Log = log; }

        internal AuthBase(AuthenticationList authList, ref LogMain log)
        {
            this._AuthList = authList;
            Log = log;
        }

        /// <summary>
        /// 启动身份验证过程
        /// </summary>
        /// <param name="Connection"></param>
        /// <param name="Callback"></param>
        internal void StartAuthentication(Socket Connection, AuthenticationCompleteDelegate Callback)
        {
            this._Connection = Connection;
            this._Callback = Callback;
            try {
                _Bytes = null;
                Connection.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnRecvRequest), Connection);
            } catch (Exception ex) { Log.AddLog ("AuthBase处理身份验证过程时出错|" + ex.Message); Callback(false); }
        }

        internal void StartAuthentication(Socket Connection, AuthenticationCompleteDelegate Callback, Boolean a)
        {
            //_Connection = Connection;
            //_Callback = Callback;
            //try
            //{
            //    _Bytes = null;
            //    Connection.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnRecvRequest), Connection);
            //}
            //catch (Exception ex) { Log.AddLog("AuthBase处理身份验证过程2时出错|" + ex.Message); Callback(false); } // 这都是错误的代码
            Callback(a);
        }

        /// <summary>
        /// 将字节增加到Bytes返回的数组中
        /// </summary>
        /// <param name="NewBytes"></param>
        /// <param name="Cnt"></param>
        internal void AddBytes(Byte[] NewBytes, Int32 Cnt)
        {
            if (Cnt <= 0 || NewBytes == null || Cnt > NewBytes.Length) return;
            if (_Bytes == null) _Bytes = new Byte[Cnt];
            else
            {
                Byte[] tmp = _Bytes;
                _Bytes = new Byte[_Bytes.Length + Cnt];
                Array.Copy(tmp, 0, _Bytes, 0, tmp.Length);
            } Array.Copy(NewBytes, 0, _Bytes, _Bytes.Length - Cnt, Cnt);
        }

        /// <summary>
        /// 处理身份验证查询
        /// </summary>
        /// <param name="Query"></param>
        private void ProcessQuery(byte[] Query)
        {///!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            _Callback(true);//////////////////////处理身份验证核心代码
            //try
            //{
            //    string User = Encoding.ASCII.GetString(Query, 2, Query[1]);
            //    string Pass = Encoding.ASCII.GetString(Query, Query[1] + 3, Query[Query[1] + 2]);
            //    byte[] ToSend;
            //    if (AuthList == null || AuthList.IsItemPresent(User, Pass))
            //    {
            //        ToSend = new byte[] { 5, 0 };
            //        Connection.BeginSend(ToSend, 0, ToSend.Length, SocketFlags.None, new AsyncCallback(this.OnOkSent), Connection);
            //    }
            //    else {
            //        ToSend = new byte[] { 5, 1 };
            //        Connection.BeginSend(ToSend, 0, ToSend.Length, SocketFlags.None, new AsyncCallback(this.OnUhohSent), Connection);
            //    }
            //} catch (Exception ex) { Console.WriteLine("Error:" + DateTime.Now + "|" + ex.Message); Callback(false); }
        }

        /// <summary>
        /// 检查集合中是否存在用户密码组合
        /// </summary>
        /// <param name="Username"></param>
        /// <param name="Password"></param>
        /// <returns></returns>
        internal Boolean IsItemPresent(String Username, String Password)
        {
            return IsHashPresent(Username, Convert.ToBase64String(new MD5CryptoServiceProvider().ComputeHash(Encoding.ASCII.GetBytes(Password))));
        }

        /// <summary>
        /// 检查集合中是否存在用户密码哈希组合
        /// </summary>
        /// <param name="Username"></param>
        /// <param name="PassHash"></param>
        /// <returns></returns>
        internal Boolean IsHashPresent(String Username, String PassHash)
        {
            return _List.ContainsKey(Username) && _List[Username].Equals(PassHash);
        }

        /// <summary>
        /// 检查指定的认证查询是否是有效的
        /// </summary>
        /// <param name="Query"></param>
        /// <returns></returns>
        private Boolean IsValidQuery(Byte[] Query)
        {
            try
            { return (Query.Length == Query[1] + Query[Query[1] + 2] + 3); }
            catch (Exception ex) { Log.AddLog("AuthBase检查认证查询是否有效时出错|" + ex.Message); return false; }
        }
        #endregion

        #region 委托
        /// <summary>
        /// 当收到客户端的请求时调用
        /// </summary>
        /// <param name="ar"></param>
        private void OnRecvRequest(IAsyncResult ar)
        {
            try
            {
                int Ret = _Connection.EndReceive(ar);
                if (Ret <= 0) { _Callback(false); return; }
                AddBytes(_Buffer, Ret);
                if (IsValidQuery(_Bytes)) ProcessQuery(_Bytes);
                else _Connection.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnRecvRequest), _Connection);
            } catch (Exception ex) { Log.AddLog("AuthBase处理客户端请求调用时出错|" + ex.Message); _Callback(false); }
        }

        /// <summary>
        /// 当OK答复发送给客户端时的处理调用
        /// </summary>
        /// <param name="ar"></param>
        private void OnOkSent(IAsyncResult ar)
        {
            try
            {
                if (_Connection.EndSend(ar) <= 0) _Callback(false);
                else _Callback(true);
            }
            catch (Exception ex) { Log.AddLog("AuthBase在答复客户端时出错|" +  ex.Message); _Callback(false); }
        }

        /// <summary>
        /// 当客户端发送了否定回复时处理调用
        /// </summary>
        /// <param name="ar"></param>
        private void OnUhohSent(IAsyncResult ar)
        {
            try { _Connection.EndSend(ar); }
            catch (Exception ex) { Log.AddLog("AuthBase在处理否定回复时出错|" + ex.Message); } _Callback(false);
        }
        #endregion

    }
}
