using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Specialized;

namespace Socks5
{
    internal class AuthenticationList
    {

        #region 变量
        private StringDictionary _List = null;
        #endregion

        #region 方法
        /// <summary>
        /// 检查集合中是否存在用户密码组合
        /// </summary>
        /// <param name="Username"></param>
        /// <param name="Password"></param>
        /// <returns></returns>
        internal Boolean IsItemPresent(String userName, String passWord)
        {
            return IsHashPresent(userName, Convert.ToBase64String(new MD5CryptoServiceProvider().ComputeHash(Encoding.ASCII.GetBytes(passWord))));
        }

        /// <summary>
        /// 检查集合中是否存在用户密码哈希组合
        /// </summary>
        /// <param name="Username"></param>
        /// <param name="Passhash"></param>
        /// <returns></returns>
        public bool IsHashPresent(string Username, string Passhash)
        {
            return _List.ContainsKey(Username) && _List[Username].Equals(Passhash);
        }
        #endregion

    }
}