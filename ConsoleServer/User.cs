using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;
using System.Data;
using SocketServer;

namespace ConsoleServer
{
    class User
    {

        string Name;
        string FName;
        string Surname;
        string IP;
        string UserID="";
        int ID;
        int Rights;
        string Login;
        string Password;
        int Laboratory;
        string Job;
        DateTime LastUsed;

        public const string NoUserID = "NoUserID";

        public User(string UserName, string UserPassword)
        {
            Login = UserName.Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Password = UserPassword.Trim(new char[] { "\n"[0], "\r"[0], ' ' });

            Program.ConOpen();
            MySqlCommand Data = new MySqlCommand("SELECT * FROM `persons` WHERE (`login` = \"" +
                Login + "\") AND (`password` = \"" + GetPasswordHash() + "\") LIMIT 1",
                Program.con);
            DataTable DT = new DataTable();           //Таблица БД

            try     // Получаем таблицу
            {
                using (MySqlDataReader dr = Data.ExecuteReader())
                {
                    if (dr.HasRows) { DT.Load(dr); }
                    else
                    {
                        UserID = NoUserID;
                        return;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); } // Выводим комментарий ошибки

            if (DT.Rows.Count == 0)  // Выводим результат
            {
                UserID = NoUserID;
                return;
            }

            Random Rnd = new Random();
            for (int i = 0; i<20; i++)
            {
                UserID += Char.ToString((Char)Rnd.Next(33, 122));
            }

            ID = Convert.ToInt32(DT.Rows[0].ItemArray[0]);
            Name = (DT.Rows[0].ItemArray[1] as string).Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            FName = (DT.Rows[0].ItemArray[2] as string).Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Surname = (DT.Rows[0].ItemArray[3] as string).Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Laboratory = Convert.ToInt32(DT.Rows[0].ItemArray[4]);
            Rights = Convert.ToInt32(DT.Rows[0].ItemArray[5]);
            Job = DT.Rows[0].ItemArray[8] as string;
            LastUsed = DateTime.Now;

            Program.con.Close();
        }

        public User(string UserName, string UserPassword, string _Name, string _FName, string _Surname,
            int _Permissions, string _Laboratory, string _Job, MySqlConnection con)
        {
            Name = _Name.Trim(new char[] { "\n"[0], "\r"[0] });
            FName = _FName.Trim(new char[] { "\n"[0], "\r"[0] });
            Surname = _Surname.Trim(new char[] { "\n"[0], "\r"[0] });
            Login = UserName.Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Password = UserPassword.Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Laboratory = Convert.ToInt32(_Laboratory);
            Rights = _Permissions;
            Job = _Job.Trim(new char[] { "\n"[0], "\r"[0], ' ' });

            string queryString = "INSERT INTO `persons` (`name`, `fathers_name`, `surname`, `laboratory`, `permissions`, `login`, `password`)\n";
            queryString += "VALUES ('" + Name + "', '" + FName + "', '" + Surname + "', " + _Laboratory +
                ", " + Rights + ", '" + UserName + "', '" + GetPasswordHash() + "');";

            MySqlCommand com = new MySqlCommand(queryString, con);
            Program.ConOpen();
            com.ExecuteNonQuery();
            con.Close();
            LastUsed = DateTime.Now;
        }

        public string GetPasswordHash()
        {
            return getMd5Hash(Password + Salt);
        }

        string getMd5Hash(string input)
        {
            // создаем объект этого класса. Отмечу, что он создается не через new, а вызовом метода Create
            MD5 md5Hasher = MD5.Create();

            // Преобразуем входную строку в массив байт и вычисляем хэш
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));

            // Создаем новый Stringbuilder (Изменяемую строку) для набора байт
            StringBuilder sBuilder = new StringBuilder();

            // Преобразуем каждый байт хэша в шестнадцатеричную строку
            for (int i = 0; i < data.Length; i++)
            {
                //указывает, что нужно преобразовать элемент в шестнадцатиричную строку длиной в два символа
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }

        public string GetUserID()
        {
            return UserID;
        }

        public string GetLogin()
        {
            return Login;
        }

        public DateTime GetLastUse()
        {
            return LastUsed;
        }

        public void Use()
        {
            LastUsed = DateTime.Now;
        }

        public string GetSearchRermissions()
        {
            switch (Rights)
            {
                case 0:
                    return "`person` = " + ID.ToString();
                case 1:
                case 2:
                    return "`laboratory` = " + Laboratory.ToString();
                case 3:
                case 4:
                case 5:
                case 10:
                    return "TRUE";
                default:
                    return "`person` = " + ID.ToString();
            }
        }

        public bool GetUserAddRermissions()
        {
            if (Rights == 10)
            { return true; }
            return false;
        }

        public bool GetAdminRermissions()
        {
            if (Rights == 10)
            { return true; }
            return false;
        }


        const string Salt = @"ДжОнатан Билл, 
                                который убил 
                                медведя 
                                в Чёрном Бору, 
                                Джонатан Билл, 
                                который купил 
                                в прошлом году 
                                кенгуру, 
                                Джонатан Билл, 
                                который скопил 
                                пробок 
                                два сундука, 
                                Джонатан Билл, 
                                который кормил финиками 
                                быка, 
                                Джонатан Билл, 
                                который лечил 
                                ячмень 
                                на левом глазу, 
                                Джонатан Билл, 
                                который учил 
                                петь по нотам 
                                козу, 
                                Джонатан Билл, 
                                который уплыл 
                                в Индию 
                                к тётушке Трот, — 
                                ТАК ВОТ 
                                этот самый Джонатан Билл 
                                очень любил компот. ";

    }


        
}
