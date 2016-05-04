// SocketServer.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using OpenBabel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Xml.Serialization;
using ConsoleServer;

namespace SocketServer
{
    class Program
    {
        //Команды от клиента в нулевой строке
        const string Search_Mol = "<@Search_Molecule@>";
        const string Add_User = "<@Add_User@>";
        const string Add_Mol = "<@Add_Molecule@>";
        const string Login = "<@Login_User@>";
        // Служебные команды
        const string All_Users = "<@Show_All_Users@>";

        // Ответные команды
        const string LoginOK = "<@Login_OK@>";
        const string LoginExp = "<@Login_Expired@>";
        const string StartMsg = "<@Begin_Of_Session@>";
        const string EndMsg = "<@End_Of_Session@>";

        // Параметры БД
        static string DB_Server = "127.0.0.1";
        static string DB_Name = "mol_base";
        static string DB_User = "Mol_Base";
        static string DB_Pass = "Yrjksorybakpetudomgztyu73ju96m";

        //Объекты БД
        public static MySqlConnectionStringBuilder mysqlCSB;
        public static MySqlConnection con;

        // Ключ и вектор для шифрования
        static AES_Data CommonAES;

        // Список активных пользователей
        static List<User> Active_Users = new List<User>();

        // Время бездействия пользователя до принудительного выхода
        const int UserTimeOut = 3600;

        static void SendMsg(Socket handler, string Msg)
        {
            byte[] msg = Encoding.UTF8.GetBytes(Msg + "\n");
            handler.Send(msg);
        }

        static void ConnectToDB()
        {
            mysqlCSB = new MySqlConnectionStringBuilder();
            mysqlCSB.Server = DB_Server;
            mysqlCSB.Database = DB_Name;
            mysqlCSB.UserID = DB_User;
            mysqlCSB.Password = DB_Pass;

            using (con = new MySqlConnection())
            {
                con.ConnectionString = mysqlCSB.ConnectionString;
            }

        }

        public static void ConOpen()
        {
            if (con.State == ConnectionState.Closed) { con.Open(); };
        }

        // Определение прав на выдачу результатов для пользователя
        static string GetQueryRights()
        {
            return "TRUE";
        }


        // Поиск элементов в БД
        static List<string> GetRows(string Query)
        {
            List<string> Result = new List<string>();

            // Получение данных из БД по запросу
            MySqlCommand Data = new MySqlCommand(Query, con);
            DataTable DT = new DataTable();           //Таблица БД

            try     // Получаем таблицу
            {
                using (MySqlDataReader dr = Data.ExecuteReader())
                { if (dr.HasRows) { DT.Load(dr); } }
            }
            catch (Exception ex) { Result.Add(ex.Message); } // Выводим комментарий ошибки

            if (DT.Rows.Count > 0)  // Выводим результат
            {
                for (int i=0; i<DT.Rows.Count; i++)
                {
                    for (int j = 0; j < DT.Columns.Count; j++)
                    {
                        Result.Add(NotNull(DT.Rows[i].ItemArray[j].ToString().Trim("\n"[0])));
                    }
                }
                
            }
            else
            {
                for (int j = 1; j < DT.Columns.Count; j++)
                {
                    Result.Add("ERROR 2 – Data not found");
                }
            }

            return Result;
        }

        // Поиск по подструктуре из БД с расшифровкой
        static List<string> Get_Mol(string Sub_Mol, User CurUser)
        {
            //Создаём новые объекты
            List<string> Result = new List<string>(); //Список вывода
            DataTable dt = new DataTable();           //Таблица БД

            //Создаём запрос на поиск
            string queryString = @"SELECT `id`, `name`, `laboratory`, `person`, `b_structure`, `state`,
`melting_point`, `conditions`, `other_properties`, `mass`, `solution` ";
            queryString += "\nFROM `molecules` \n";
            queryString += "WHERE " + CurUser.GetSearchRermissions();

            // Создание команды MySQL
            MySqlCommand com = new MySqlCommand(queryString, con);

            // Выполнение запроса
            try
            {
                con.Open();

                using (MySqlDataReader dr = com.ExecuteReader())
                {
                    if (dr.HasRows)
                    {
                        dt.Load(dr);
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            // Сравнение каждой молекулы из запроса со стандартом
            for (int i=0; i < dt.Rows.Count; i++)
            {
                //Расшифровка
                string Structure = DecryptStringFromBytesAes(dt.Rows[i].ItemArray[4] as byte[], 
                    CommonAES.AesKey, CommonAES.AesIV);

                if (CheckMol(Sub_Mol, Structure))
                {
                    /*
                    Структура данных: (-> - Открытый, => - закодированный)
                    -> New molecule
                    00 -> id
                    01 -> name
                    02 -> laboratory
                    03 -> person
                    04 => b_structure
                    05 => state
                    06 => melting_point
                    07 => conditions
                    08 => other_properties
                    09 => mass
                    10 => solution
                    11 -> laboratory_name
                    12 -> laboratory_Abb
                    13 -> name (person)
                    14 -> father's name
                    15 -> surname
                    16 -> job
                    */

                    // Начинаем передачу данных
                    Result.Add("New molecule");
                    // Пересылаем открытые данные
                    for (int j = 0; j < 4; j++)
                    {
                        Result.Add(NotNull(dt.Rows[i].ItemArray[j].ToString().Trim("\n"[0])));
                    }
                    // Расшифровываем и пересылаем закодированные данные
                    for (int j = 4; j < 11; j++)
                    {
                        Result.Add(
                            NotNull(
                                 DecryptStringFromBytesAes(
                                     dt.Rows[i].ItemArray[j] as byte[],
                                     CommonAES.AesKey,
                                     CommonAES.AesIV)).Trim(new char[] { "\n"[0], ' ' }));
                    }


                    // Получение имени и аббривеатуры лаборатории из БД
                    Result.AddRange(GetRows("SELECT `name`, `abbr` FROM `laboratory` WHERE `id`=" +
                        dt.Rows[i].ItemArray[2].ToString() + " LIMIT 1"));

                    // Получение ФИО и должности сотрудника из БД
                    Result.AddRange(GetRows(@"SELECT `name`, `fathers_name`, `Surname`, `job` 
                        FROM `persons` 
                        WHERE `id`= " +
                        dt.Rows[i].ItemArray[3].ToString() + @"
                        LIMIT 1"));

                    // Получение элементов анализа
                    List<string> analys = GetRows(@"SELECT `analys`.`name`, `analys`.`name_whom` 
                        FROM `analys` 
                          INNER JOIN `analys_to_molecules` ON `analys_to_molecules`.`analys` = `analys`.`id`
                        WHERE `analys_to_molecules`.`molecule` = " + dt.Rows[i].ItemArray[0].ToString() + ";");
                    Result.Add((analys.Count() / 2).ToString());
                    Result.AddRange(analys);
                }
            };

            /*for (int i = 0; i < Result.Count; i++)
            {
                Console.WriteLine(Result[i]);
            }*/

            con.Close();
            return Result;
        }

        static string NotNull(string Text)
        {
            return Text != "" ? Text : "<@None@>";
        }

        static bool CheckMol(string Mol, string DB_Mol)
        {
            // Создаём объекты OpenBabel
            OBSmartsPattern SP = new OBSmartsPattern();
            OBConversion obconv = new OBConversion();
            OBMol mol = new OBMol();
            obconv.SetInFormat("smi");  //У нас всё в формате SMILES
            obconv.ReadString(mol, Mol);
            if (!mol.DeleteHydrogens()) { Console.WriteLine("DeleteHidrogens() failed!"); };  //Убираем все водороды
            obconv.SetOutFormat("smi");

            string SubMol = System.Text.RegularExpressions.Regex.Replace(obconv.WriteString(mol), "[Hh ]", ""); //Убираем все водороды
            SP.Init(SubMol);  //Задаём структуру поиска в SMARTS

            obconv.SetInFormat("smi");
            obconv.ReadString(mol, DB_Mol); //Добавляем структуру из БД
            SP.Match(mol); //Chfdybdftv
            VectorVecInt Vec = SP.GetUMapList();
            if (Vec.Count > 0) { return true; } else { return false; }; //Возвращаем результат
        }

        static void Search_Molecules(Socket handler, User CurUser,  string Mol)
        {
            // Запрашиваем поиск по БД
            List<string> Result = Get_Mol(Mol, CurUser);

            // Отправляем ответ клиенту\
            SendMsg(handler, StartMsg);
            for (int i = 0; i < Result.Count(); i++)
            {
                SendMsg(handler, Result[i]);
            }
            SendMsg(handler, EndMsg);
        }

        static void Add_User_to_DB(Socket handler, User CurUser, string UserName, string Password)
        {
            if (CurUser.GetUserAddRermissions())
            {
                User NewUser = new User(UserName, Password, "–", "–", UserName, 0, "1", "1", con);
                SendMsg(handler, StartMsg);
                SendMsg(handler, "Add_User: done");
                SendMsg(handler, EndMsg);
            }
            SendMsg(handler, StartMsg);
            SendMsg(handler, "Add_User: No permissions");
            SendMsg(handler, EndMsg);
        }

        static byte[] EncryptStringToBytesAes(string plainText, byte[] Key, byte[] IV)
        {
            // Проверка аргументов
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");
            byte[] encrypted;

            // Создаем объект класса AES
            // с определенным ключом and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Создаем объект, который определяет основные операции преобразований.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Создаем поток для шифрования.
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Записываем в поток все данные.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }


            //Возвращаем зашифрованные байты из потока памяти.
            return encrypted;

        }

        // Дешифрует массив байтов в строку
        static string DecryptStringFromBytesAes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Проверяем аргументы
            if (cipherText == null || cipherText.Length <= 0)
            {
                return "";
            };
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");

            // Строка, для хранения расшифрованного текста
            string plaintext;

            // Создаем объект класса AES,
            // Ключ и IV
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Создаем объект, который определяет основные операции преобразований.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Создаем поток для расшифрования.
                using (var msDecrypt = new MemoryStream(cipherText))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Читаем расшифрованное сообщение и записываем в строку
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }

            return plaintext;

        }

        // Кодирует структуры заданным ключом. ADMIN ONLY
        static void EncryptAll(Socket handler, User CurUser)
        {
            if (!CurUser.GetAdminRermissions())
            {
                SendMsg(handler, StartMsg);
                SendMsg(handler, "ERROR: Access denied!");
                SendMsg(handler, EndMsg);
                return;
            }

            DataTable dt = new DataTable();

            string queryString = "SELECT `id`, `name`, `IUPAC`, `structure`\n";
            queryString += "FROM `molecules`;";

            MySqlCommand com = new MySqlCommand(queryString, con);

            try
            {
                con.Open();

                using (MySqlDataReader dr = com.ExecuteReader())
                {
                    if (dr.HasRows)
                    {
                        dt.Load(dr);
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            SendMsg(handler, StartMsg);
            SendMsg(handler, "OK");
            SendMsg(handler, EndMsg);

            for (int i = 0; i < dt.Rows.Count; i++)
            {

                byte[] EncryptData = EncryptStringToBytesAes(dt.Rows[i].ItemArray[3].ToString(),
                    CommonAES.AesKey, CommonAES.AesIV);

                string QS = @"UPDATE `molecules` 
SET `b_s_size` = @BS_Size, `b_structure` = @B_Strucrure 
WHERE `id` = " + dt.Rows[i].ItemArray[0].ToString();
                MySqlCommand UpdCom = new MySqlCommand(QS, con);
                UpdCom.Parameters.AddWithValue("@BS_Size", EncryptData.Length);
                UpdCom.Parameters.AddWithValue("@B_Strucrure", EncryptData);
                UpdCom.ExecuteNonQuery();
            };

            con.Close();
        }

        static string NotNullSQL(string Text)
        {
            if (Text != "") { return Text; }
            return "NULL";
        }

        static void AddMolecule(Socket handler, User CurUser, string[] Data)
        {
            string queryString = @"INSERT INTO `molecules` 
(`name`, `laboratory`, `person`, `b_structure`, `state`, `melting_point`, `conditions`, `other_properties`, `mass`, `solution`)
VALUES (@Name, @Laboratory, @Person, @Structure, @State, @MeltingPoint, @Conditions, @OtherProperties, @Mass, @Solution);";

            MySqlCommand com = new MySqlCommand(queryString, con);
            con.Open();
            com.Parameters.AddWithValue("@Name", Data[3]);
            com.Parameters.AddWithValue("@Laboratory", Data[4]);
            com.Parameters.AddWithValue("@Person", Data[5]);
            com.Parameters.AddWithValue("@Structure", EncryptStringToBytesAes(Data[6],
                    CommonAES.AesKey, CommonAES.AesIV));
            com.Parameters.AddWithValue("@State", EncryptStringToBytesAes(Data[7],
                    CommonAES.AesKey, CommonAES.AesIV));
            com.Parameters.AddWithValue("@MeltingPoint", EncryptStringToBytesAes(Data[8],
                    CommonAES.AesKey, CommonAES.AesIV));
            com.Parameters.AddWithValue("@Conditions", EncryptStringToBytesAes(Data[9],
                    CommonAES.AesKey, CommonAES.AesIV));
            com.Parameters.AddWithValue("@OtherProperties", EncryptStringToBytesAes(Data[10],
                    CommonAES.AesKey, CommonAES.AesIV));
            com.Parameters.AddWithValue("@Mass", EncryptStringToBytesAes(Data[11],
                    CommonAES.AesKey, CommonAES.AesIV));
            com.Parameters.AddWithValue("@Solution", EncryptStringToBytesAes(Data[12],
                    CommonAES.AesKey, CommonAES.AesIV));

            com.ExecuteNonQuery();
            con.Close();
            SendMsg(handler, StartMsg);
            SendMsg(handler, "Add_Molecule: done");
            SendMsg(handler, EndMsg);
        }

        // Проверка имени пользователя и пароля
        static void LoginMsg(Socket handler, string _User, string _Password)
        {
            if (Active_Users.Find(x => x.GetLogin() == _User) != null )
            {
                Active_Users.RemoveAll(x => x.GetLogin() == _User);
            };

            User NewUser = new User(_User, _Password);
            if (NewUser.GetUserID() != User.NoUserID)
            {
                Active_Users.Add(NewUser);
            }
            SendMsg(handler, StartMsg);
            SendMsg(handler, LoginOK); 
            SendMsg(handler, NewUser.GetUserID());
            SendMsg(handler, EndMsg);
        }

        static void Main(string[] args)
        {
            // Открываем файл-ключ
            CommonAES = AES_Data.LoadFromFile("vector.bin");

            // Устанавливаем для сокета локальную конечную точку
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 11000);

            // Создаем сокет Tcp/Ip
            Socket sListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Назначаем сокет локальной конечной точке и слушаем входящие сокеты
            try
            {
                sListener.Bind(ipEndPoint);
                sListener.Listen(10);


                //Подключаемся к БД
                ConnectToDB();
                Console.WriteLine("Подключение к MySQL: 127.0.0.1:3306");

                Console.WriteLine("Старт сервера");

                // Начинаем слушать соединения
                while (true)
                {
                    Console.WriteLine("Ожидаем соединение через порт {0}", ipEndPoint);

                    // Программа приостанавливается, ожидая входящее соединение
                    Socket handler = sListener.Accept();
                    string data = null;

                    // Мы дождались клиента, пытающегося с нами соединиться

                    byte[] bytes = new byte[1024];
                    int bytesRec = handler.Receive(bytes);

                    data += Encoding.UTF8.GetString(bytes, 0, bytesRec);

                    string[] data_parse = data.Split("\n"[0]);

                    // Показываем данные на консоли
                    Console.Write("Полученный текст: «" + data + "»\n\n");

                    User CurUser = null;
                    if (data_parse[0].Trim() != Login)
                    {
                        CurUser = GetCurUser(data_parse[1], data_parse[2]);
                        if (CurUser == null)
                        {
                            SendMsg(handler, StartMsg);
                            SendMsg(handler, LoginExp);
                            SendMsg(handler, EndMsg);

                            handler.Shutdown(SocketShutdown.Both);
                            handler.Close();

                            GC.Collect();
                            continue;
                        }
                    }


                    // Обрабатываем запрос
                    switch (data_parse[0].Trim())
                    {
                        case Search_Mol:
                            {
                                Search_Molecules(handler, CurUser, data_parse[3]);
                                break;
                            }
                        case Add_User:
                            {
                                Add_User_to_DB(handler, CurUser, data_parse[3], data_parse[4]);
                                break;
                            }
                        case "<@Encrypt_All@>":
                            {
                                EncryptAll(handler, CurUser);
                                break;
                            }
                        case Add_Mol:
                            {
                                AddMolecule(handler, CurUser, data_parse);
                                break;
                            }
                        case Login:
                            {
                                LoginMsg(handler, data_parse[3], data_parse[4]);
                                break;
                            }

                        case All_Users:
                            {
                                AllUsersMsg(handler, CurUser);
                                break;
                            }
                        default:
                            {
                                SendMsg(handler, StartMsg);
                                SendMsg(handler, "Error 1: Unknown command in line 0");
                                SendMsg(handler, EndMsg);
                                break;
                            }
                    } 
                    
                    if (data.IndexOf("<TheEnd>") > -1)
                    {
                        Console.WriteLine("Сервер завершил соединение с клиентом.");
                        break;
                    }

                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();

                    GC.Collect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                Console.ReadLine();
            }
        }

        private static User GetCurUser(string UserName, string UserID)
        {
            User CurUser = Active_Users.Find(x => x.GetLogin() == UserName);
            if (CurUser == null) { return null; };
            if (CurUser.GetUserID() != UserID) { return null; };

            if ((DateTime.Now - CurUser.GetLastUse()).TotalSeconds > UserTimeOut)
            {
                Active_Users.RemoveAll(x => x.GetLogin() == UserName);
                return null;
            }

            return CurUser;
        }

        private static void AllUsersMsg(Socket handler, User CurUser)
        {
            if (!CurUser.GetAdminRermissions())
            {
                SendMsg(handler, StartMsg);
                SendMsg(handler, "ERROR: Access denied!");
                SendMsg(handler, EndMsg);
                return;
            }

            SendMsg(handler, StartMsg);
            foreach(User U in Active_Users)
            {
                SendMsg(handler, U.GetLogin() + " -> " + U.GetUserID());
            }
            SendMsg(handler, EndMsg);
        }
    }
}