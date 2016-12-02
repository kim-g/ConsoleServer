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

        // Служебные команды
        const string All_Users = "<@Show_All_Users@>";
        const string ShowHash = "<@Show_Hash@>";
        const string SendFileMsg = "<@*Send_File*@>";
        const string GetFileMsg = "<@*Get_File*@>";

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
        static List<string> Get_Mol(User CurUser, string Sub_Mol="", string Request = "Permission", int Status = 0)
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

        static void Search_Molecules(Socket handler, User CurUser,  string Mol="", string Request="Permission",
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
        static void LoginMsg(Socket handler, string _User, string _Password)
        {
            if (Active_Users.Find(x => x.GetLogin() == _User) != null )
            {
                Active_Users.RemoveAll(x => x.GetLogin() == _User);
            };

            User NewUser = new User(_User, _Password, DataBase);
            if (NewUser.GetUserID() != User.NoUserID)
            {
                Active_Users.Add(NewUser);
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
            for (int i=0; i < FileSizei; i += 1024)
            {
                int Size = 1024;
                if (FileSizei - i < 1024) Size = FileSizei - i;
                byte[] Block = new byte[Size];
                handler.Receive(Block,Size,SocketFlags.None);
                ms.Write(Block, 0, Size);
            }
            ms.Position = 0;
            ms.Read(ResFile, 0, FileSizei);

            Files FileToAdd = new Files(Name, FileName, ResFile);
            int FileID = FileToAdd.Add_To_DB(DataBase, CommonAES, CurUser.GetID(), CurUser.GetLaboratory());
            Console.WriteLine("NewID: {0}", FileID);

            DataBase.ExecuteQuery(@"INSERT INTO `files_to_molecules` (`file`, `molecule`)
VALUES ("+ FileID + ", "+ MoleculeID + ")");

            SendMsg(handler, StartMsg);
            SendMsg(handler, FileID.ToString());
            SendMsg(handler, EndMsg);
        }

        // Выход пользователя
        static void User_Quit(Socket handler, User CurUser)
        {
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

            // Удалим все устаревшие записи
            Active_Users.RemoveAll(x => (DateTime.Now - x.GetLastUse()).TotalSeconds > UserTimeOut);

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
            foreach(User U in Active_Users)
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
                            MolID + @") AND (" + CurUser.GetSearchRermissions() +@") LIMIT 1;");
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

    }
}