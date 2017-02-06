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
    public class User
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
        int SessionID;
        bool Active = true;
        DB DataBase;

        public const string NoUserID = "NoUserID";

        //Создание пользователя по его ID
        public User(string UserID, DB _DataBase)
        {
            // Запишем DB
            DataBase = _DataBase;

            // Сохраняем ID
            ID = Convert.ToInt32(UserID.Trim(new char[] { "\n"[0], "\r"[0], ' ' }).ToLower());

            // Получаем остальную инфу из БД
            DataTable DT = DataBase.Query("SELECT * FROM `persons` WHERE `id` = \"" +
                ID.ToString() + "\" LIMIT 1");

            if (DT.Rows.Count == 0)  // Выводим результат
            {
                Login = NoUserID;
                UserID = NoUserID;
                return;
            }

            // Присваиваем параметрам значения из БД
            Name = (DT.Rows[0].ItemArray[1] as string).Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            FName = (DT.Rows[0].ItemArray[2] as string).Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Surname = (DT.Rows[0].ItemArray[3] as string).Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Laboratory = Convert.ToInt32(DT.Rows[0].ItemArray[4]);
            Rights = Convert.ToInt32(DT.Rows[0].ItemArray[5]);
            Login = (DT.Rows[0].ItemArray[6] as string).Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Job = DT.Rows[0].ItemArray[8] as string;
            Active = Convert.ToInt32(DT.Rows[0].ItemArray[9]) == 1;

        }

        public User(string UserName, string UserPassword, DB _DataBase, string _IP = "NOT SET")
        {
            // Запишем DB
            DataBase = _DataBase;

            Login = UserName.Trim(new char[] { "\n"[0], "\r"[0], ' ' }).ToLower();
            Password = UserPassword.Trim(new char[] { "\n"[0], "\r"[0], ' ' });

            DataTable DT = DataBase.Query("SELECT * FROM `persons` WHERE (`login` = \"" +
                Login + "\") AND (`password` = \"" + GetPasswordHash() + "\") AND `active`=1 LIMIT 1");

            if (DT.Rows.Count == 0)  // Выводим результат
            {
                Login = NoUserID;
                UserID = NoUserID;
                return;
            }

            // Добавим в БД запись о входе с компьютера с указанным IP
            DataBase.Query("INSERT INTO `sessions` (`user`, `ip`) VALUES (" + DT.Rows[0].ItemArray[0] as string + 
                ", '" + _IP + "')");

            SessionID = Program.GetLastID(DataBase);

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
            IP = _IP;
        }

        public User(string UserName, string UserPassword, string _Name, string _FName, string _Surname,
            int _Permissions, string _Laboratory, string _Job, DB _DataBase)
        {
            // Запишем DB
            DataBase = _DataBase;

            Name = _Name.Trim(new char[] { "\n"[0], "\r"[0] });
            FName = _FName.Trim(new char[] { "\n"[0], "\r"[0] });
            Surname = _Surname.Trim(new char[] { "\n"[0], "\r"[0] });
            Login = UserName.Trim(new char[] { "\n"[0], "\r"[0], ' ' }).ToLower();
            Password = UserPassword.Trim(new char[] { "\n"[0], "\r"[0], ' ' });
            Laboratory = Convert.ToInt32(_Laboratory);
            Rights = _Permissions;
            Job = _Job.Trim(new char[] { "\n"[0], "\r"[0], ' ' });

            string queryString = "INSERT INTO `persons` (`name`, `fathers_name`, `surname`, `laboratory`, `permissions`, `login`, `password`, `job`)\n";
            queryString += "VALUES ('" + Name + "', '" + FName + "', '" + Surname + "', " + _Laboratory +
                ", " + Rights + ", '" + Login + "', '" + GetPasswordHash() + "', '" + _Job + "');";
            DataBase.ExecuteQuery(queryString);
           
            LastUsed = DateTime.Now;
        }

        public string GetPasswordHash()
        {
            return getMd5Hash(Password + Salt);
        }

        public bool IsManager()
        {
            return Rights == 11;
        }

        public static string GetPasswordHash(string Password)
        {
            return getMd5Hash(Password + Salt);
        }

        static string getMd5Hash(string input)
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

        public int GetLaboratory()
        {
            return Laboratory;
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
                case 11:
                    return "TRUE";
                default:
                    return "`person` = " + ID.ToString();
            }
        }

        // Поиск своих и только своих соединений.
        public string GetMyMolecules()
        {
            return "`person` = " + ID.ToString();
        }

        // Вернуть в зависимости и от разрешений, и от запроса.
        public string GetPermissionsOrReqest(string Request)
        {
            if (Request == "Permission") return GetSearchRermissions();
            if (Request == "My") return GetMyMolecules();

            //Если ничего не нашли, возвращаем только свои
            return GetMyMolecules();
        }

        public bool GetUserAddRermissions()
        {
            return Rights > 9;
        }

        public bool GetAdminRermissions()
        {
            return Rights == 10;
        }

        public string GetFullName()
        {
            return Name + " " + FName + " " + Surname;
        }

        public int GetID()
        {
            return ID;
        }

        public bool IsAdmin()
        {
            return Rights == 10;
        }

        public void Quit(string Reason)
        {
            DataBase.Query(@"UPDATE `sessions` 
                SET `quit_date`=CURRENT_TIMESTAMP(), `reason_quit` = '" + Reason + @"' 
                WHERE `id` = " + SessionID.ToString());
            Active = false;
        }

        public int GetSessionID()
        {
            return SessionID;
        }

        public bool Dead()
        {
            return !Active;
        }

        public static int GetIDByLogin(DB DataBase, string Login)
        {
            DataTable Name = DataBase.Query("SELECT `id` FROM `persons` WHERE (`login` = '" + 
                Login.Trim('\r').Trim('\n').Trim() + "') AND `active`=1;");

            // Проверим, есть ли человек с таким ником и найдём его ID
            if (Name.Rows.Count == 0) return -1;
            return Convert.ToInt32(Name.Rows[0].ItemArray[0]);
        }

        public string GetSurname()
        {
            return Surname != null ? Surname : "";
        }

        public string GetName()
        {
            return Name != null ? Name : "";
        }

        public string GetFathersName()
        {
            return FName != null ? FName : ""; 
        }

        public string GetJob()
        {
            return Job != null ? Job : "";
        }

        public int GetPermissionsInt()
        {
            return Rights;
        }

        void SetParam(string Param, string Value)
        {
            DataBase.ExecuteQuery("UPDATE `persons`");
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
