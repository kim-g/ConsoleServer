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
        // Отладочные команды. Для релиза поставить FALSE
        const bool DEBUG = true;

        //Команды от клиента в нулевой строке
        const string Search_Mol = "<@Search_Molecule@>";
        const string Add_User = "<@Add_User@>";
        const string Add_Mol = "<@Add_Molecule@>";
        const string Login = "<@Login_User@>";
        const string Status = "<@Next_Status@>";
        const string GetStatuses = "<@Get_Status_List@>";
        const string QuitMsg = "<@*Quit*@>";
        const string FN_msg = "<@GetFileName@>";
        const string Show_My_mol = "<@Show my molecules@>";  // Команда показать все молекулы
        const string Increase_Status = "<@Increase status@>"; // Увеличеть значение статуса соединения
        const string Show_New_Mol = "<@Show new molecules@>";  // Команда показать все молекулы новые
        const string SendFileMsg = "<@*Send_File*@>";
        const string GetFileMsg = "<@*Get_File*@>";

        // Служебные команды
        const string All_Users = "<@Show_All_Users@>";
        const string ShowHash = "<@Show_Hash@>";
        const string Help = "help";      // Справка по консоли администратора
        // СК по базе
        const string HelpDB = "database";    // Справка по использованию БД
        const string LastID = "database.show_last_id";    // Показать последний использованный ID
        // СК по журналу
        const string HelpLog = "log";    // Справка по использованию журнала
        const string SessionLog = "log.sessions";    // Показать список сессий
        const string QueryLog = "log.queries";    // Показать список запросов.
        // СК по пользователям
        const string HelpUsers = "users";   // Справка по командам со списком пользователей.
        const string UsersList = "users.list";  // Вывод всех пользователей
        const string ActiveUsersList = "users.active";  // Вывод залогиненных пользователей
        const string UsersAdd = "users.add";  // Добавление нового пользователя через консоль
        const string UsersUpdate = "users.update";  // Изменение данных пользователя через консоль


        // Ответные команды
        const string LoginOK = "<@Login_OK@>";
        const string LoginExp = "<@Login_Expired@>";
        const string StartMsg = "<@Begin_Of_Session@>";
        const string EndMsg = "<@End_Of_Session@>";
        public const string Answer_Admin = "AdminOK";
        public const string Answer_Manager = "ManagerOK";

        // Параметры БД
        static string DB_Server = "127.0.0.1";
        static string DB_Name = "mol_base";
        static string DB_User = "Mol_Base";
        static string DB_Pass = "Yrjksorybakpetudomgztyu73ju96m";

        // Сама БД
        static DB DataBase;

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
            DataTable DT = DataBase.Query(Query);

            if (DT.Rows.Count > 0)  // Выводим результат
            {
                for (int i = 0; i < DT.Rows.Count; i++)
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

        // NotNull без пробелов элемент из БД
        static string FromBase(DataTable dt, int i, int j)
        {
            return NotNull(dt.Rows[i].ItemArray[j].ToString().Trim("\n"[0]));
        }

        // NotNull без пробелов элемент из БД
        static string FromBaseDec(DataTable dt, int i, int j)
        {
            return NotNull(CommonAES.DecryptStringFromBytes(
                dt.Rows[i].ItemArray[j] as byte[])).Trim(new char[] { "\n"[0], ' ' });
        }

        // Поиск по подструктуре из БД с расшифровкой
        static List<string> Get_Mol(User CurUser, string Sub_Mol = "", string Request = "Permission", int Status = 0)
        {
            //Создаём новые объекты
            List<string> Result = new List<string>(); //Список вывода

            //Создаём запрос на поиск
            string queryString = @"SELECT `id`, `name`, `laboratory`, `person`, `b_structure`, `state`,
`melting_point`, `conditions`, `other_properties`, `mass`, `solution`, `status` ";
            queryString += "\nFROM `molecules` \n";
            queryString += "WHERE (" + CurUser.GetPermissionsOrReqest(Request) + ")";
            if (Status > 0) queryString += " AND (`status` = " + Status.ToString() + ")"; // Добавляем статус в запрос

            DataTable dt = DataBase.Query(queryString);

            if (Sub_Mol == "")
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                    Result.Add(DataRow_To_Molecule_Transport(dt, i).ToXML());

            }

            else
            {
                // Сравнение каждой молекулы из запроса со стандартом
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    //Расшифровка
                    string Structure = CommonAES.DecryptStringFromBytes(dt.Rows[i].ItemArray[4] as byte[]);

                    if (CheckMol(Sub_Mol, Structure))
                        Result.Add(DataRow_To_Molecule_Transport(dt, i).ToXML());
                };
            }

            DataBase.ConClose();
            return Result;
        }

        private static Molecule_Transport DataRow_To_Molecule_Transport(DataTable dt, int i)
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
                                17 -> status
                                18+ -> Виды анализа
                                */

            // Наполнение транспортного класса
            Molecule_Transport MT = new Molecule_Transport();
            MT.ID = Convert.ToInt32(FromBase(dt, i, 0));
            MT.Name = FromBase(dt, i, 1);
            MT.Laboratory = new laboratory();
            MT.Laboratory.ID = Convert.ToInt32(FromBase(dt, i, 2));
            List<string> Lab = GetRows("SELECT `name`, `abbr` FROM `laboratory` WHERE `id`=" +
                dt.Rows[i].ItemArray[2].ToString() + " LIMIT 1");
            MT.Laboratory.Name = Lab[0];
            MT.Laboratory.Abb = Lab[1];
            List<string> Per = GetRows(@"SELECT `name`, `fathers_name`, `Surname`, `job` 
                        FROM `persons` 
                        WHERE `id`= " + dt.Rows[i].ItemArray[3].ToString() + @"
                        LIMIT 1");
            MT.Person = new person();
            MT.Person.ID = Convert.ToInt32(FromBase(dt, i, 3));
            MT.Person.Name = Per[0];
            MT.Person.FathersName = Per[1];
            MT.Person.Surname = Per[2];
            MT.Person.Job = Per[3];
            MT.Structure = FromBaseDec(dt, i, 4);
            MT.State = FromBaseDec(dt, i, 5);
            MT.Melting_Point = FromBaseDec(dt, i, 6);
            MT.Conditions = FromBaseDec(dt, i, 7);
            MT.Other_Properties = FromBaseDec(dt, i, 8);
            MT.Mass = FromBaseDec(dt, i, 9);
            MT.Solution = FromBaseDec(dt, i, 10);
            MT.Status = Convert.ToInt32(FromBase(dt, i, 11));
            MT.Analysis = GetRows(@"SELECT `analys`.`name`, `analys`.`name_whom` 
                        FROM `analys` 
                          INNER JOIN `analys_to_molecules` ON `analys_to_molecules`.`analys` = `analys`.`id`
                        WHERE `analys_to_molecules`.`molecule` = " + dt.Rows[i].ItemArray[0].ToString() + ";");

            // Добавляем список файлов
            MT.Files = new List<file>();
            //   Получаем файлы, имеющие отношение к данному соединению
            DataTable files = DataBase.Query(@"SELECT `file` 
                        FROM `files_to_molecules` 
                        WHERE `molecule` = " + dt.Rows[i].ItemArray[0].ToString() + ";");
            for (int f = 0; f < files.Rows.Count; f++)
            {
                file NF = new file();
                NF.ID = (int)files.Rows[f].ItemArray[0];
                DataTable NewFile = DataBase.Query(@"SELECT `name` FROM files WHERE `id`=" +
                    NF.ID.ToString() + @" LIMIT 1;");
                if (NewFile.Rows.Count == 0) { NF.Name = "Файл отсутствует"; }
                else { NF.Name = NewFile.Rows[0].ItemArray[0].ToString(); }

                MT.Files.Add(NF);
            }

            return MT;
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

        static void Search_Molecules(Socket handler, User CurUser, string Mol = "", string Request = "Permission",
            int Status = 0)
        {
            // Запрашиваем поиск по БД
            List<string> Result = Get_Mol(CurUser, Mol, Request, Status);

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
                User NewUser = new User(UserName, Password, "–", "–", UserName, 0, "1", "1", DataBase);
                SendMsg(handler, StartMsg);
                SendMsg(handler, "Add_User: done");
                SendMsg(handler, EndMsg);
                return;
            }
            SendMsg(handler, StartMsg);
            SendMsg(handler, "Add_User: No permissions");
            SendMsg(handler, EndMsg);
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



            string queryString = "SELECT `id`, `name`, `IUPAC`, `structure`\n";
            queryString += "FROM `molecules`;";

            DataTable dt = DataBase.Query(queryString);

            SendMsg(handler, StartMsg);
            SendMsg(handler, "OK");
            SendMsg(handler, EndMsg);

            for (int i = 0; i < dt.Rows.Count; i++)
            {

                byte[] EncryptData = CommonAES.EncryptStringToBytes(dt.Rows[i].ItemArray[3].ToString());

                string QS = @"UPDATE `molecules` 
SET `b_s_size` = @BS_Size, `b_structure` = @B_Strucrure 
WHERE `id` = " + dt.Rows[i].ItemArray[0].ToString();
                MySqlCommand UpdCom = DataBase.MakeCommandObject(QS);
                UpdCom.Parameters.AddWithValue("@BS_Size", EncryptData.Length);
                UpdCom.Parameters.AddWithValue("@B_Strucrure", EncryptData);
                UpdCom.ExecuteNonQuery();
            };
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

            MySqlCommand com = DataBase.MakeCommandObject(queryString);
            com.Parameters.AddWithValue("@Name", Data[3]);
            com.Parameters.AddWithValue("@Laboratory", Data[4]);
            com.Parameters.AddWithValue("@Person", Data[5]);
            com.Parameters.AddWithValue("@Structure", CommonAES.EncryptStringToBytes(Data[6]));
            com.Parameters.AddWithValue("@State", CommonAES.EncryptStringToBytes(Data[7]));
            com.Parameters.AddWithValue("@MeltingPoint", CommonAES.EncryptStringToBytes(Data[8]));
            com.Parameters.AddWithValue("@Conditions", CommonAES.EncryptStringToBytes(Data[9]));
            com.Parameters.AddWithValue("@OtherProperties", CommonAES.EncryptStringToBytes(Data[10]));
            com.Parameters.AddWithValue("@Mass", CommonAES.EncryptStringToBytes(Data[11]));
            com.Parameters.AddWithValue("@Solution", CommonAES.EncryptStringToBytes(Data[12]));

            com.ExecuteNonQuery();

            SendMsg(handler, StartMsg);
            SendMsg(handler, "Add_Molecule: done");
            SendMsg(handler, EndMsg);
        }

        // Проверка имени пользователя и пароля
        static void LoginMsg(Socket handler, string _User, string _Password, int LogID)
        {
            // Найдём уже залогиненных пользователей с таким же ником
            List<User> UserList = Active_Users.FindAll(x => x.GetLogin() == _User);
            foreach (User x in UserList)       // И всех их «выйдем»
            {
                x.Quit("User Relogin");
            }

            // ...удалив из списка
            if (UserList != null)
            {
                Active_Users.RemoveAll(x => x.GetLogin() == _User);
            };

            // Залогинемся. Здесь происходит обращение к БД. В случае ошибки UserID будет User.NoUserID
            User NewUser = new User(_User, _Password, DataBase,
                ((IPEndPoint)handler.RemoteEndPoint).Address.ToString());

            // Если такой пользователь есть, то добавим его в список
            if (NewUser.GetUserID() != User.NoUserID)
            { Active_Users.Add(NewUser); }
            else   // Если нет, то напишем об этом в журнал
            {
                DataBase.ExecuteQuery("UPDATE `queries` SET `comment` = '! User name and/or pasword invalid' " +
                                    "WHERE `id` = " + LogID.ToString() + ";");
            }

            SendMsg(handler, StartMsg);
            SendMsg(handler, LoginOK);
            SendMsg(handler, NewUser.GetUserID());
            SendMsg(handler, NewUser.GetID().ToString());
            SendMsg(handler, NewUser.GetFullName());
            if (NewUser.IsAdmin()) { SendMsg(handler, Answer_Admin); };
            if (NewUser.IsManager()) { SendMsg(handler, Answer_Manager); };
            SendMsg(handler, EndMsg);
        }

        static void SendStatusList(Socket handler)
        {
            List<string> Res = GetRows("SELECT * FROM `status`");
            SendMsg(handler, StartMsg);
            for (int i = 0; i < Res.Count; i++)
                SendMsg(handler, Res[i]);
            SendMsg(handler, EndMsg);
        }

        // Программа для передачи файла из БД клиенту
        static void SendFile(Socket handler, User CurUser, string FileID)
        {
            Files FileToSend = Files.Read_From_DB(DataBase, Convert.ToInt32(FileID), CurUser);
            SendFileSize(handler, FileToSend);

            int FtS_Size = FileToSend.Data.Count();
            for (int i = 0; i < FtS_Size; i += 1024)
            {

                int block;
                if (FtS_Size - i < 1024) { block = FtS_Size - i; }
                else { block = 1024; }

                byte[] buf = new byte[block];
                FileToSend.DataStream.Read(buf, 0, block);
                handler.Send(buf);
            }

            FileToSend.Save();
        }

        // Передаёт клиенту размер файла
        static void SendFileSize(Socket handler, Files FileToSend)
        {
            byte[] msg = Encoding.UTF8.GetBytes(StartMsg + "\n" + FileToSend.FileName + "\n" + FileToSend.Data.Count().ToString() + "\n");
            Console.WriteLine(msg.Length.ToString() + "; \"" + msg + "\"");
            handler.Send(BitConverter.GetBytes(msg.Length));
            handler.Send(msg);
        }

        // Отладочная программа для тестирования приёма файла от клиента
        static void GetFileTemp(Socket handler, string FileName, string FileSize)
        {
            byte[] ResFile = new byte[Convert.ToInt32(FileSize)];
            handler.Receive(ResFile);

            Files FileToAdd = new Files(FileName, FileName, ResFile);
            FileToAdd.Add_To_DB(DataBase, CommonAES, 1, 1);

            SendMsg(handler, StartMsg);
            SendMsg(handler, "OK");
            SendMsg(handler, EndMsg);
        }

        // Программа для приёма файла от клиента
        static void GetFile(Socket handler, User CurUser, string FileName, string Name, string FileSize,
            string MoleculeID)
        {
            int FileSizei = Convert.ToInt32(FileSize);
            byte[] ResFile = new byte[FileSizei];

            Stream ms = new MemoryStream();
            for (int i = 0; i < FileSizei; i += 1024)
            {
                int Size = 1024;
                if (FileSizei - i < 1024) Size = FileSizei - i;
                byte[] Block = new byte[Size];
                handler.Receive(Block, Size, SocketFlags.None);
                ms.Write(Block, 0, Size);
            }
            ms.Position = 0;
            ms.Read(ResFile, 0, FileSizei);

            Files FileToAdd = new Files(Name, FileName, ResFile);
            int FileID = FileToAdd.Add_To_DB(DataBase, CommonAES, CurUser.GetID(), CurUser.GetLaboratory());
            Console.WriteLine("NewID: {0}", FileID);

            DataBase.ExecuteQuery(@"INSERT INTO `files_to_molecules` (`file`, `molecule`)
VALUES (" + FileID + ", " + MoleculeID + ")");

            SendMsg(handler, StartMsg);
            SendMsg(handler, FileID.ToString());
            SendMsg(handler, EndMsg);
        }

        // Выход пользователя
        static void User_Quit(Socket handler, User CurUser)
        {
            CurUser.Quit("User Quited");
            Active_Users.Remove(CurUser);
            SendMsg(handler, StartMsg);
            SendMsg(handler, "OK");
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
                DataBase = new DB(DB_Server, DB_Name, DB_User, DB_Pass);

                Console.WriteLine("Подключение к MySQL: 127.0.0.1:3306");

                Console.WriteLine("Старт сервера");

                // Завершаем все сеансы сообщением о падении сервера.
                DataBase.ExecuteQuery(@"UPDATE `sessions` 
                    SET `quit_date` = CURRENT_TIMESTAMP(), `reason_quit` = 'Server Fail – quit date of restart'
                    WHERE `quit_date` IS NULL;");

                // Начинаем слушать соединения
                while (true)
                {
                    Console.WriteLine("Ожидаем соединение через порт {0}", ipEndPoint);

                    // Программа приостанавливается, ожидая входящее соединение
                    Socket handler = sListener.Accept();
                    string data = null;

                    // Мы дождались клиента, пытающегося с нами соединиться

                    // Получаем длину текстового сообщения
                    byte[] SL_Length_b = new byte[4];
                    handler.Receive(SL_Length_b);
                    int SL_Length = BitConverter.ToInt32(SL_Length_b, 0);

                    // Получаем текстовую часть сообщения
                    byte[] bytes = new byte[SL_Length];
                    int bytesRec = handler.Receive(bytes);

                    data += Encoding.UTF8.GetString(bytes, 0, bytesRec);

                    string[] data_parse = data.Split("\n"[0]);

                    // Показываем данные на консоли
                    Console.Write("Полученный текст: «" + data + "»\n\n");

                    // Очистка ото всех уже не активных пользователей, которые почему-то не удалены из списка активных
                    Active_Users.RemoveAll(x => x.Dead());

                    // Ищем пользователя по его логину и защитной записи.
                    // Если дана команда входа в систему, то поиск не производим.
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

                    // Записываем в журнал команду.
                    // Сохраняем все переданные параметры в одну строку через перенос каретки
                    string Params = "";
                    for (int i = 3; i < data_parse.Count(); i++)
                    {
                        if (i > 3) Params += "\n";
                        if ((data_parse[0].Trim() == Login) && (i == 4))
                            Params += "*****";
                        else Params += data_parse[i];
                    }

                    // И добавляем в лог
                    string LogQuery = data_parse[0].Trim() != Login
                        ? @"INSERT INTO `queries` (`user`, `session`,`ip`,`command`,`parameters`) 
VALUES (" + CurUser.GetID().ToString() + ", " + CurUser.GetSessionID().ToString() +
", '" + ((IPEndPoint)handler.RemoteEndPoint).Address.ToString() + "', '" + data_parse[0] +
"', '" + Params + "');"
                        : @"INSERT INTO `queries` (`ip`,`command`,`parameters`) 
VALUES ('" + ((IPEndPoint)handler.RemoteEndPoint).Address.ToString() + "', '" + data_parse[0] +
"', '" + Params + "');";
                    DataBase.ExecuteQuery(LogQuery);
                    int LogID = GetLastID(DataBase);


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
                                LoginMsg(handler, data_parse[3], data_parse[4], LogID);
                                break;
                            }

                        case All_Users:
                            {
                                AllUsersMsg(handler, CurUser);
                                break;
                            }
                        case ShowHash:
                            {
                                if (!CurUser.IsAdmin()) { break; }

                                string Password = data_parse[3].Trim(new char[] { "\n"[0], "\r"[0], ' ' });
                                SendMsg(handler, StartMsg);
                                SendMsg(handler, User.GetPasswordHash(Password));
                                SendMsg(handler, EndMsg);
                                break;
                            }
                        case GetStatuses:
                            {
                                SendStatusList(handler);
                                break;
                            }
                        case SendFileMsg:
                            {
                                SendFile(handler, CurUser, data_parse[3]);
                                break;
                            }
                        case GetFileMsg:
                            {
                                GetFile(handler, CurUser, data_parse[3], data_parse[4], data_parse[5],
                                    data_parse[6]);
                                break;
                            }
                        case QuitMsg:
                            {
                                User_Quit(handler, CurUser);
                                break;
                            }
                        case FN_msg:
                            {
                                GetFileName(handler, CurUser, data_parse[3]);
                                break;
                            }
                        case Show_My_mol:
                            {
                                Search_Molecules(handler, CurUser, "", "My");
                                break;
                            }
                        case Increase_Status:
                            {
                                IncreaseStatus(handler, CurUser, data_parse[3]);
                                break;
                            }
                        case Show_New_Mol:
                            {
                                Search_Molecules(handler, CurUser, "", "Permission", 1);
                                break;
                            }
                        case Help:
                            {
                                SendMsg(handler, StartMsg);
                                SendMsg(handler, @"Administrator's console. Gives the direct access to server. Possible comands:
 - log - direct access to server's logs;
 - database - direct access to server's database;
 - users - direct access to list of users.");
                                SendMsg(handler, EndMsg);
                                break;
                            }
                        case LastID:
                            {
                                SendMsg(handler, StartMsg);
                                SendMsg(handler, GetLastID(DataBase).ToString());
                                SendMsg(handler, EndMsg);
                                break;
                            }
                        case SessionLog:
                            {
                                ShowSessionLog(handler, CurUser, GetParameters(data_parse));
                                break;
                            }
                        case HelpLog:
                            {
                                SendMsg(handler, StartMsg);
                                SendMsg(handler, @"System logs. Shows informations aboute program usage. Possible comands:
 - log.sessions - shows sessions history
 - log.queries - shows query history.");
                                SendMsg(handler, EndMsg);
                                break;
                            }
                        case HelpDB:
                            {
                                SendMsg(handler, StartMsg);
                                SendMsg(handler, @"System database. Makes the direct access to DB commands. Possible comands:
 - database.show_last_id - shows ID key of the last inserted row.");
                                SendMsg(handler, EndMsg);
                                break;
                            }
                        case QueryLog:
                            {
                                ShowQueryLog(handler, CurUser, GetParameters(data_parse));
                                break;
                            }

                        case HelpUsers:
                            {
                                SendMsg(handler, StartMsg);
                                SendMsg(handler, @"List of users. Helps to manage user list. Possible comands:
 - users.list - shows all users;
 - users.active - shows currently logged in users;
 - users.add - Adds new user
 - users.update - changes user information
 - users.password - changes user's password
 - users.remove - delete user. May be reversible;
 - users.rmrf - delete the user's record. Irreversable.");
                                SendMsg(handler, EndMsg);
                                break;
                            }
                        case UsersList:
                            {
                                ShowUsersList(handler, CurUser, GetParameters(data_parse));
                                break;
                            }
                        case ActiveUsersList:
                            {
                                ShowActiveUsersList(handler, CurUser, GetParameters(data_parse));
                                break;
                            }
                        case UsersAdd:
                            {
                                AddUser(handler, CurUser, GetParameters(data_parse));
                                break;
                            }
                        case UsersUpdate:
                            {
                                UpdateUser(handler, CurUser, GetParameters(data_parse));
                                break;
                            }
                        default:
                            {
                                DataBase.ExecuteQuery("UPDATE `queries` SET `comment` = '! Unknown command' " +
                                    "WHERE `id` = " + LogID.ToString() + ";");

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

        private static string[] GetParameters(string[] data_parse)
        {
            string[] ShowParams = new string[data_parse.Count() - 3];
            for (int i = 3; i < data_parse.Count(); i++)
                ShowParams[i - 3] = data_parse[i];
            return ShowParams;
        }

        private static User GetCurUser(string UserName, string UserID)
        {
            User CurUser = Active_Users.Find(x => x.GetLogin() == UserName);
            if (CurUser == null) { return null; };
            if (CurUser.GetUserID() != UserID) { return null; };

            if ((DateTime.Now - CurUser.GetLastUse()).TotalSeconds > UserTimeOut)
            {
                CurUser.Quit("Time out");
                Active_Users.Remove(CurUser);
                return null;
            }

            // Удалим все устаревшие записи
            List<User> LU = Active_Users.FindAll(x => (DateTime.Now - x.GetLastUse()).TotalSeconds > UserTimeOut);
            foreach (User U in LU)
            {
                U.Quit("Time out");
                Active_Users.Remove(U);
            }

            // Продлим срок жизни пользователя. (Хе-хе-хе!)
            CurUser.Use();

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
            foreach (User U in Active_Users)
            {
                SendMsg(handler, U.GetLogin() + " -> " + U.GetUserID());
            }
            SendMsg(handler, EndMsg);
        }

        private static void GetFileName(Socket handler, User CurUser, string FileID)
        {
            DataTable NewFile = DataBase.Query(@"SELECT `file_name` FROM files WHERE `id`=" +
                            FileID + @" LIMIT 1;");
            string Out;
            if (NewFile.Rows.Count == 0) { Out = "Файл отсутствует"; }
            else { Out = NewFile.Rows[0].ItemArray[0].ToString(); }

            SendMsg(handler, StartMsg);
            SendMsg(handler, Out);
            SendMsg(handler, EndMsg);
        }

        // Изменить статус на 1
        private static void IncreaseStatus(Socket handler, User CurUser, string MolID)
        {
            DataTable MolStatus = DataBase.Query(@"SELECT `status` FROM `molecules` WHERE (`id`=" +
                            MolID + @") AND (" + CurUser.GetSearchRermissions() + @") LIMIT 1;");
            if (MolStatus.Rows.Count == 0)
            {
                SendMsg(handler, StartMsg);
                SendMsg(handler, "ERROR 101 – Not found or access denied");
                SendMsg(handler, EndMsg);
                return;
            }
            DataTable NewStatus = DataBase.Query(@"SELECT `next` FROM `status` WHERE (`id`=" +
                            MolStatus.Rows[0].ItemArray[0].ToString() + @") LIMIT 1;");
            if (NewStatus.Rows.Count == 0)
            {
                SendMsg(handler, StartMsg);
                SendMsg(handler, "ERROR 102 – Status not found");
                SendMsg(handler, EndMsg);
                return;
            }
            if (NewStatus.Rows[0].ItemArray[0].ToString() == "-1")
            {
                SendMsg(handler, StartMsg);
                SendMsg(handler, "ERROR 103 – Maximum status");
                SendMsg(handler, EndMsg);
                return;
            }
            if (DEBUG) Console.WriteLine(NewStatus.Rows[0].ItemArray[0]);

            // Если ни одной ошибки не обнаружено, увеличиваем статус
            DataBase.ExecuteQuery(@"UPDATE `molecules` SET `status` = " +
                NewStatus.Rows[0].ItemArray[0].ToString() + @" WHERE `id` = " + MolID + @" LIMIT 1;");
            SendMsg(handler, StartMsg);
            SendMsg(handler, "OK");
            SendMsg(handler, EndMsg);
        }

        public static int GetLastID(DB DataBase)
        {
            DataTable LR = DataBase.Query("SELECT LAST_INSERT_ID()");
            return Convert.ToInt32(LR.Rows[0].ItemArray[0]);
        }

        // Показать вывод журнала сессий
        public static void ShowSessionLog(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!IsAdmin(handler, CurUser)) return;

            // Взять всё из журнала и...
            string Query = @"SELECT `sessions`.`id`, `enter_date`,`quit_date`, `persons`.`login`, `ip`, `reason_quit` 
FROM `sessions`
INNER JOIN `persons` ON (`persons`.`id` = `sessions`.`user`)";

            // Инициация переменных, чтобы из if(){} нормально вышли
            string Person = "";
            string Date = "";
            string DateRangeBegin = "";
            string DateRangeEnd = "";
            string Limit = "";
            string Reason = "";
            string Active = "";
            string IP = "";

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "person") Person = Param[1].Trim('\r'); // Для конкретного пользователя
                if (Param[0] == "date") Date = Param[1].Trim('\r');     // Для конкретной даты
                if (Param[0] == "period")                               // Для периода
                {
                    DateRangeBegin = Param[1].Trim('\r');
                    DateRangeEnd = Param[2].Trim('\r');
                }
                if (Param[0] == "limit") Limit = Param[1].Trim('\r'); else Limit = "100";   // Сколько показать
                if (Param[0] == "reason") // Для конкретной причины выхода
                {
                    Reason = Param[1].Trim('\r');
                    for (int j = 2; j < Param.Count(); j++)
                        Reason += " " + Param[j].Trim('\r');
                }
                if (Param[0] == "active") Active = "TRUE";              // Только активных
                if (Param[0] == "ip") IP = Param[1].Trim('\r');     // Для конкретной даты

                // Служебные
                if (Param[0] == "help")     // Помощь
                {
                    SendMsg(handler, StartMsg);
                    SendMsg(handler, @"log.sessions shows list of sessions. There are several filter parameters:

 - person [login] - Show only person's sessions;
 - date YYYY-MM-DD - Shows sessions that started or ended in this day;
 - perood YYYY-MM-DD YYYY-MM-DD - Shows sessions in that period of time;
 - ip - Shows sessions from certain IP address or range. Examples: '127.0.0.1', .192.168.'
 - limit [Number] - How many sessions to show. Default is 100;
 - reason [Reason] - Shows sessions, that ended with definite reason;
 - active - Shows only current working sessions.

Parameters may be combined.");
                    SendMsg(handler, EndMsg);
                    return;
                }
            }

            // Если есть условие выборки, добавим WHERE
            if (Person != "" || Date != "" || DateRangeBegin != "" || Reason != "" || Active != "" || IP != "")
                Query += " WHERE TRUE";

            //Выберем отдельного человека
            if (Person != "")
            {
                string Pers = PersonID(handler, Person);
                if (Pers == "NoUser") return;
                Query += " AND (`user` = " + Pers + ")";
            }

            // Выберем конкретный день
            if (Date != "")
            {
                Query += @" AND (
    (DATE(`enter_date`) BETWEEN '" + Date + @"  00:00:00' AND '" + Date + @"  23:59:59') OR
    (DATE(`quit_date`) BETWEEN '" + Date + @"  00:00:00' AND '" + Date + @"  23:59:59')
)";
            }

            // Выберем диапазон дат
            if (DateRangeBegin != "")
            {
                Query += @" AND (
    (DATE(`enter_date`) BETWEEN '" + DateRangeBegin + @"  00:00:00' AND '" + DateRangeEnd + @"  23:59:59') OR
    (DATE(`quit_date`) BETWEEN '" + DateRangeBegin + @"  00:00:00' AND '" + DateRangeEnd + @"  23:59:59')
)";
            }

            // Выберем причину выхода из системы. Зачем может понадобиться – не знаю.
            if (Reason != "")
            {
                Query += " AND (`reason_quit` LIKE '%" + Reason + "%')";
            }

            // Выберем IP адрес
            if (IP != "")
            {
                Query += " AND (`ip` LIKE '%" + IP + "%')";
            }

            // Выберем активных пользователей.
            if (Active != "")
            {
                Query += " AND (`quit_date` IS NULL)";
            }

            // Добавим обратную сортировку и лимит
            Query += " ORDER BY `id` DESC LIMIT " + Limit + ";";

            DataTable Res = DataBase.Query(Query);

            // И отошлём всё.
            SendMsg(handler, StartMsg);
            SendMsg(handler, Query);
            SendMsg(handler, "| ID\t | Start date   time  \t | End   date   time  \t | User            | IP              | Reason");
            SendMsg(handler, "|--------|-----------------------|-----------------------|-----------------|-----------------|-----------------------------------");

            //Server Fail – quit date of restart
            if (Res.Rows.Count == 0) SendMsg(handler, "Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = "| " + Res.Rows[i].ItemArray[0].ToString() + "\t | ";
                msg += Res.Rows[i].ItemArray[1].ToString().Replace("\n","").Replace("\r", "") + "\t | ";
                msg += Res.Rows[i].ItemArray[2].ToString() != ""
                    ? Res.Rows[i].ItemArray[2].ToString() + "\t | "
                    : "---------- --:--:--\t | ";
                msg += Res.Rows[i].ItemArray[3].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[3].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[4].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[4].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[5].ToString() + "\t";
                SendMsg(handler, msg);
            }
            SendMsg(handler, EndMsg);
        }

        // Показ журнала запросов
        static void ShowQueryLog(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!IsAdmin(handler, CurUser)) return;

            // Взять всё из журнала и...
            string Query = @"SELECT `queries`.`id`, `persons`.`login`, `session`, `ip`, `date`, `command`, `parameters`, `comment` 
FROM mol_base.queries
INNER JOIN persons ON(persons.id = queries.user)";

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string UserName = "";
            string Session = "";
            string IP = "";
            string Date = "";
            string DateBegin = "";
            string DateEnd = "";
            string Command = "";
            string Parameters = "";
            string Comment = "";
            string Limit = "100";

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "person") UserName = Param[1];      // Показать запросы конкретного человека
                if (Param[0] == "session") Session = Param[1];      // Показать запросы в конкретной сессии
                if (Param[0] == "ip") IP = Param[1];                // Показать запросы c конкретного IP
                if (Param[0] == "date") Date = Param[1];            // Показать запросы в конкретный день
                if (Param[0] == "period")                           // Показать запросы в конкретный день
                {
                    DateBegin = Param[1];
                    DateEnd = Param[2];
                }
                if (Param[0] == "command") Command = Param[1];            // Показать запросы с конкретной командой
                if (Param[0] == "parameter") Parameters = Param[1];       // Показать запросы с конкретным параметром (нафиг надо?!)
                if (Param[0] == "comment") Comment = Param[1];            // Показать запросы с конкретым комментарием
                if (Param[0] == "limit") Limit = Param[1];          // Показать конкретное число запросов

                // Служебные
                if (Param[0] == "help")     // Помощь
                {
                    SendMsg(handler, StartMsg);
                    SendMsg(handler, @"log.queries shows list of users' queries to server. All queries are logged. There are several filter parameters:

 - person [login] - Show only person's queries;
 - date YYYY-MM-DD - Shows queries that were in this day;
 - perood YYYY-MM-DD YYYY-MM-DD - Shows queries in that period of time;
 - limit [Number] - How many queries to show. Default is 100;
 - session [Number] - queries in session with ID=[Number];
 - ip [IP address or range] - Shows queries from this IP. Examples: '127.0.0.1', '192.168.';
 - command [Command] - Shows queries with certain main command;
 - parameter - Shows command with definite parameter. Only one of them.
 - comment - Shoqs queries with certain comment. Commens starts with symbols '!' - Not important, '!!' - important, '!!!' - very important. You may filter by this symbols.

Parameters may be combined.");
                    SendMsg(handler, EndMsg);
                    return;
                }
            }

            // Если есть условие выборки, добавим WHERE
            if (UserName != "" || Session != "" || IP != "" || Date != "" || DateBegin != ""
                || Command != "" || Parameters != "")
                Query += "\nWHERE TRUE";

            //Выберем отдельного человека
            if (UserName != "")
            {
                string Pers = PersonID(handler, UserName);
                if (Pers == "NoUser") return;
                Query += " AND (`user` = " + Pers + ")";
            }

            //Выберем сессию
            if (Session != "") Query += " AND (`session` = " + Session + ")";

            // Выберем конкретный день
            if (Date != "")
            {
                Query += @" AND (DATE(`date`) BETWEEN '" + Date + @"  00:00:00' AND '" + Date + @"  23:59:59')";
            }

            // Выберем диапазон дат
            if (DateBegin != "")
            {
                Query += @" AND (DATE(`date`) BETWEEN '" + DateBegin + @"  00:00:00' AND '" + DateEnd + @"  23:59:59')";
            }

            //Выберем IP
            if (IP != "") Query += " AND (`ip` = '" + IP + "')";

            //Выберем команду
            if (Command != "") Query += " AND (`command` = '" + Command + "')";

            //Выберем параметры
            if (Parameters != "") Query += " AND (`paremeters` LIKE '%" + Parameters + "%')";

            //Выберем коммент
            if (Comment != "") Query += " AND (`comment` LIKE '%" + Comment + "%')";

            // Добавим обратную сортировку и лимит
            Query += "\nORDER BY `id` DESC\nLIMIT " + Limit + ";";

            DataTable Res = DataBase.Query(Query);

            // И пошлём всё пользователю.
            SendMsg(handler, StartMsg);
            SendMsg(handler, "| ID     | user            | session | IP              | Date                | Command (Parameters) – Comment");
            SendMsg(handler, "|--------|-----------------|---------|-----------------|---------------------|-----------------------------------");

            //Server Fail – quit date of restart
            if (Res.Rows.Count == 0) SendMsg(handler, "Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = "| " + Res.Rows[i].ItemArray[0].ToString() + "\t | ";
                msg += Res.Rows[i].ItemArray[1].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[1].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[2].ToString() +
                    new String(' ', 7 - Res.Rows[i].ItemArray[2].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[3].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[3].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[4].ToString() +
                    new String(' ', 19 - Res.Rows[i].ItemArray[4].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[5].ToString() + " (";
                msg += Res.Rows[i].ItemArray[6].ToString().Replace('\n',' ').Replace('\r',';') + ") – ";
                msg += Res.Rows[i].ItemArray[7].ToString() + "";
                SendMsg(handler, msg);
            }
            SendMsg(handler, EndMsg);
        }

        public static bool IsAdmin(Socket handler, User CurUser)
        {
            // Если не админ, то ничего не покажем!
            if (!CurUser.IsAdmin())
            {
                ErrorMsg(handler, "Access denied");
                return false;
            }
            return true;
        }

        public static string PersonID(Socket handler, string Name)
        {
            int Num = User.GetIDByLogin(DataBase, Name);
            if (Num == -1)
            {
                SendMsg(handler, StartMsg);
                SendMsg(handler, "ERROR – UNKNOWN USER '" + Name + "'");
                SendMsg(handler, EndMsg);
                return "NoUser";
            }
            return Num.ToString();
        }

        // Показ всех пользователей
        static void ShowUsersList(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!IsAdmin(handler, CurUser)) return;

            // Взять всё из журнала и...
            string Query = @"SELECT `persons`.`id`, `Surname`, `persons`.`name`, `fathers_name`, `laboratory`.`abbr`, `job`, `Permissions`, `login`  
FROM `persons` 
INNER JOIN `laboratory` ON (`laboratory`.`id` = `persons`.`laboratory`)";

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string Surname = "";
            string Laboratory = "";
            string Permissions = "";
            string Login = "";
            string Limit = "100";

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "surname") Surname = Param[1].Replace("\n", "").Replace("\r", "");          // Показать пользователей по фамилии
                if (Param[0] == "laboratory") Laboratory = Param[1].Replace("\n", "").Replace("\r", "");    // Показать пользователей по лаборатории
                if (Param[0] == "permissions") Permissions = Param[1].Replace("\n", "").Replace("\r", "");   // Показать пользователей по правам
                if (Param[0] == "login") Login = Param[1].Replace("\n", "").Replace("\r", "");              // Показать пользователей по логину
                if (Param[0] == "limit") Limit = Param[1].Replace("\n", "").Replace("\r", "");              // Показать конкретное число пользователей

                // Служебные
                if (Param[0] == "help")     // Помощь
                {
                    SendMsg(handler, StartMsg);
                    SendMsg(handler, @"user.list shows list of all users on the server. There are several filter parameters:

 - surname [Name] - Show persons with surname containings [Name];
 - laboratory [ABB] - Shows persons of this laboratory;
 - permissions [Number] - Shows persons with certain permissions;
 - limit [Number] - How many persons to show. Default is 100;
 - login [Name] - Show persons with login containings [Name];

Parameters may be combined.");
                    SendMsg(handler, EndMsg);
                    return;
                }
            }

            // WHERE будет всегда – показываем только неудалённых
                Query += "\nWHERE (`active` = 1)";

            //Выберем по фамилии
            if (Surname != "") Query += " AND (`Surname` LIKE '%" + Surname + "%')";

            //Выберем по лаборатории
            if (Laboratory != "") Query += " AND (`laboratory`.`abbr` LIKE '%" + Laboratory + "%')";

            //Выберем по разрешениям
            if (Permissions != "") Query += " AND (`permissions` = " + Permissions + ")";

            //Выберем по логину
            if (Login != "") Query += " AND (`login` LIKE '%" + Login + "%')";


            // Добавим обратную сортировку и лимит
            Query += "\nORDER BY `Surname`\nLIMIT " + Limit + ";";

            DataTable Res = DataBase.Query(Query);

            // И пошлём всё пользователю.
            SendMsg(handler, StartMsg);
            SendMsg(handler, "| ID     | Surname              | Name                 | Second Name          | login           | Lab.  | Job        | Perm. |");
            SendMsg(handler, "|--------|----------------------|----------------------|----------------------|-----------------|-------|------------|-------|");

            //Server Fail – quit date of restart
            if (Res.Rows.Count == 0) SendMsg(handler, "Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = "| " + Res.Rows[i].ItemArray[0].ToString() + "\t | ";
                msg += Res.Rows[i].ItemArray[1].ToString() +
                    new String(' ', 20 - Res.Rows[i].ItemArray[1].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[2].ToString() +
                    new String(' ', 20 - Res.Rows[i].ItemArray[2].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[3].ToString() +
                    new String(' ', 20 - Res.Rows[i].ItemArray[3].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[7].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[7].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[4].ToString() +
                    new String(' ', 5 - Res.Rows[i].ItemArray[4].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[5].ToString() +
                    new String(' ', 10 - Res.Rows[i].ItemArray[5].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[6].ToString() +
                    new String(' ', 5 - Res.Rows[i].ItemArray[6].ToString().Length) + " | ";
                SendMsg(handler, msg);
            }
            SendMsg(handler, EndMsg);
        }

        // Показать всех залогиненых пользователей
        static void ShowActiveUsersList(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!IsAdmin(handler, CurUser)) return;

            SendMsg(handler, StartMsg);
            foreach (User U in Active_Users)
            {
                string msg = "| " + U.GetID().ToString() +
                    new string(' ', 5 - U.GetID().ToString().Length) + " | ";
                msg += U.GetSurname() +
                    new string(' ', 20 - U.GetSurname().Length) + " | ";
                msg += U.GetName() +
                   new string(' ', 20 - U.GetName().Length) + " | ";
                msg += U.GetFathersName() +
                   new string(' ', 20 - U.GetFathersName().Length) + " | ";
                msg += U.GetLogin() +
                   new string(' ', 15 - U.GetLogin().Length) + " | ";
                string Lab = DataBase.Query("SELECT `abbr` FROM `laboratory` WHERE `id` = " +
                    U.GetLaboratory().ToString() + " LIMIT 1;").Rows[0].ItemArray[0].ToString();
                msg += Lab + new string(' ', 5 - Lab.Length) + " | ";
                msg += U.GetJob() +
                   new string(' ', 10 - U.GetJob().Length) + " | ";
                msg += U.GetPermissionsInt().ToString() +
                   new string(' ', 5 - U.GetPermissionsInt().ToString().Length) + " | ";
                msg += U.GetUserID().ToString() +
                   new string(' ', 20 - U.GetUserID().Length) + " | ";

                SendMsg(handler, msg);
            }
            SendMsg(handler, EndMsg);
        }

        // Добавить нового пользователя через командную строку
        static void AddUser(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                ErrorMsg(handler, "Access denied");
                return;
            }

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string Name = "";
            string FName = "";
            string Surname = "";
            string LoginN = "";
            string Password = "";
            string CPassword = "";
            string Permissions = "";
            string Laboratory = "";
            string Job = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');

                if (Param[0] == "name") Name = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "second.name") FName = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "surname") Surname = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "login") LoginN = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "password") Password = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "confirm") CPassword = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "permissions") Permissions = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "laboratory") Laboratory = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "job") Job = Param[1].Replace("\n", "").Replace("\r", "");

                // Помощь
                if (Param[0] == "help")
                {
                    SendMsg(handler, StartMsg);
                    SendMsg(handler, @"Command to add new user. Please, enter all information about the user. Parameters must include:
 - name [Name] - person's first name
 - second.name [Name] - person's second name. May be empty.
 - surname [Name] - person's surname.
 - login [Name] - person's login/ Must be unique.
 - password [Phrase] - person's password.
 - confirm [Phrase] - password confirmation. Must be the same as password.
 - permissions [Number]. 
   - 0 - Able to add and get only his/her own molecules
   - 1 - Able to add only his/her own molecules and to get all laboratory's molecules
   - 2 - Able to add and get all laboratory's molecules
   - 3 - Able to add only his/her own molecules and to get all institute's molecules
   - 4 - Able to add all laboratory's molecules and to get all institute's molecules
   - 5 - Able to add and get all institute's molecules
   - 10 - Administrator's right. Able to add and get all institute's molecules. Able to rewind queries statuses. Able to work with user list. Able to direct work with database. Able to work with console.
   - 11 - Manager. Able to add and get all institute's molecules. Able to rewind queries statuses.
 - laboratory [Abbr.] - laboratory's abbreviation.
 - job [Name] - person's job.");
                    SendMsg(handler, EndMsg);
                }

            }

            // Проверяем, все ли нужные данные есть
            if (Name == "") { ErrorMsg(handler, "Error: No name entered"); return; }
            if (Surname == "") { ErrorMsg(handler, "Error: No surname entered"); return; }
            if (LoginN == "") { ErrorMsg(handler, "Error: No login entered"); return; }
            if (Password == "") { ErrorMsg(handler, "Error: No password entered"); return; }
            if (CPassword == "") { ErrorMsg(handler, "Error: No password conformation entered"); return; }
            if (Permissions == "") { ErrorMsg(handler, "Error: No permissions entered"); return; }
            if (Laboratory == "") { ErrorMsg(handler, "Error: No laboratory number entered"); return; }

            DataTable DT = DataBase.Query("SELECT `id` FROM `laboratory` WHERE `abbr`='" + Laboratory + "' LIMIT 1;");
            if (DT.Rows.Count == 0) { ErrorMsg(handler, "Error: Laboratory not found"); return; };
            string LabNum = DT.Rows[0].ItemArray[0].ToString();

            // Проверка корректности введённых данных
            if (DataBase.RecordsCount("persons", "`login`='" + LoginN + "'") > 0)
                { ErrorMsg(handler, "Error: Login exists"); return; };
            if (Password != CPassword)
                { ErrorMsg(handler, "Error: \"password\" and \"confirm\" should be the similar"); return; }
            if (DataBase.RecordsCount("laboratory", "`id`=" + LabNum + "") == 0)
                { ErrorMsg(handler, "Error: Laboratory not found"); return; };

            // Добавление пользователя в БД
            new User(LoginN, Password, Name, FName, Surname, Convert.ToInt32(Permissions), LabNum, Job, 
                DataBase);
            ErrorMsg(handler, "User added");

            /*          
                        /\
                       /o \
                      /o  o\
                      --/\--
                       /O \
                      /    \
                     /o   O \
                    /o   o   \
                    ---/  \---
                      /O  o\
                     /      \
                    /o  O   O\
                   /     o    \
                  /  O      O  \
                  -----|  |-----
                       |  |
                       |  |
                       ----

            */

        }

        // Изменить данные пользователя через командную строку
        static void UpdateUser(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                ErrorMsg(handler, "Access denied");
                return;
            }

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string Name = "";
            string FName = "";
            string Surname = "";
            string LoginN = "";
            string Password = "";
            string CPassword = "";
            string Permissions = "";
            string Laboratory = "";
            string Job = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');

                if (Param[0] == "name") Name = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "second.name") FName = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "surname") Surname = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "login") LoginN = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "password") Password = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "confirm") CPassword = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "permissions") Permissions = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "laboratory") Laboratory = Param[1].Replace("\n", "").Replace("\r", "");
                if (Param[0] == "job") Job = Param[1].Replace("\n", "").Replace("\r", "");

                // Помощь
                if (Param[0] == "help")
                {
                    SendMsg(handler, StartMsg);
                    SendMsg(handler, @"Command to update user's information. Parameters may include:
 - name [Name] - person's first name
 - second.name [Name] - person's second name. May be empty.
 - surname [Name] - person's surname.
 - login [Name] - person's login/ Must be unique.
 - password [Phrase] - person's password.
 - confirm [Phrase] - password confirmation. Must be the same as password.
 - permissions [Number]. 
   - 0 - Able to add and get only his/her own molecules
   - 1 - Able to add only his/her own molecules and to get all laboratory's molecules
   - 2 - Able to add and get all laboratory's molecules
   - 3 - Able to add only his/her own molecules and to get all institute's molecules
   - 4 - Able to add all laboratory's molecules and to get all institute's molecules
   - 5 - Able to add and get all institute's molecules
   - 10 - Administrator's right. Able to add and get all institute's molecules. Able to rewind queries statuses. Able to work with user list. Able to direct work with database. Able to work with console.
   - 11 - Manager. Able to add and get all institute's molecules. Able to rewind queries statuses.
 - laboratory [Abbr.] - laboratory's abbreviation.
 - job [Name] - person's job.");
                    SendMsg(handler, EndMsg);
                }

            }

            // Проверяем, все ли нужные данные есть
            string LabNum = "";
            if (Laboratory != "")
            {
                DataTable DT = DataBase.Query("SELECT `id` FROM `laboratory` WHERE `abbr`='" + Laboratory + "' LIMIT 1;");
                if (DT.Rows.Count == 0) { ErrorMsg(handler, "Error: Laboratory not found"); return; };
                LabNum = DT.Rows[0].ItemArray[0].ToString();
            }

            // Проверка корректности введённых данных
            if (LoginN != "")
                if (DataBase.RecordsCount("persons", "`login`='" + LoginN + "'") > 0)
                { ErrorMsg(handler, "Error: Login exists"); return; };
            if (Password != "")
                if (Password != CPassword)
                { ErrorMsg(handler, "Error: \"password\" and \"confirm\" should be the similar"); return; }
            if (LabNum != "")
                if (DataBase.RecordsCount("laboratory", "`id`=" + LabNum + "") == 0)
                { ErrorMsg(handler, "Error: Laboratory not found"); return; };

            // Добавление пользователя в БД
            new User(LoginN, Password, Name, FName, Surname, Convert.ToInt32(Permissions), LabNum, Job,
                DataBase);
            ErrorMsg(handler, "User added");

            /*          
                        /\
                       /o \
                      /o  o\
                      --/\--
                       /O \
                      /    \
                     /o   O \
                    /o   o   \
                    ---/  \---
                      /O  o\
                     /      \
                    /o  O   O\
                   /     o    \
                  /  O      O  \
                  -----|  |-----
                       |  |
                       |  |
                       ----

            */

        }


        private static void ErrorMsg(Socket handler, string Error)
        {
            SendMsg(handler, StartMsg);
            SendMsg(handler, Error);
            SendMsg(handler, EndMsg);
        }
    }
}